using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // 前後の概念: 移動は常にtransform.forward方向へのベクトルで行う。
    // alignmentはtransform.forwardと目的方向の内積(-1〜1)で、正なら前進・負なら後退方向の推進力になる。
    // これにより、C/Dのように「敵の方を向きながら後退する(狙いを保ったまま逃げる)」動きが可能になる。
    //
    // 場外回避(BoundaryAvoidance)は行動方向を完全に上書きするのではなく、緊急度に応じてベクトル合成する。
    // 完全上書きだと「敵から逃げたい方向」と「境界から離れたい方向」が正反対のときに
    // 毎フレーム勝者が入れ替わり、境界付近で往復して動けなくなることがあったため。
    //
    // また「毎フレーム相手の現在位置を向いて前進する」純粋な追跡は、旋回速度が有限だと
    // 相手をかすめて円軌道(公転)に入ったまま収束しないことがある(2体が追いかけ合うと中心を軸に回り続ける現象)。
    // これは見た目の旋回ではなく実際の速度ベクトルが目的方向からズレていることが原因なので、
    // 目的方向に対して横向きの速度成分を毎ステップ積極的に打ち消し、直接ぶつかりやすくする。
    [RequireComponent(typeof(Rigidbody), typeof(CharacterStats))]
    public class CharacterMovement : MonoBehaviour
    {
        [Tooltip("目的方向に対して横向きの速度(公転・オーバーシュートの原因)を打ち消す強さ")]
        public float LateralDamping = 4f;

        Rigidbody rb;
        CharacterStats stats;
        CharacterPosture posture;
        BoundaryAvoidance boundaryAvoidance;
        readonly List<IMovementIntentSource> sources = new List<IMovementIntentSource>();

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<CharacterStats>();
            posture = GetComponent<CharacterPosture>();
            boundaryAvoidance = GetComponent<BoundaryAvoidance>();
        }

        void Start()
        {
            sources.Clear();
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is IMovementIntentSource source) sources.Add(source);
            }
        }

        void FixedUpdate()
        {
            IMovementIntentSource best = null;
            MovementIntent bestIntent = default;
            int bestPriority = int.MinValue;

            foreach (var source in sources)
            {
                if (source is MonoBehaviour mb && !mb.enabled) continue;
                if (source.TryGetMovementIntent(out var intent) && source.MovementPriority > bestPriority)
                {
                    bestPriority = source.MovementPriority;
                    best = source;
                    bestIntent = intent;
                }
            }

            Vector3 behaviorDir = best != null ? bestIntent.DesiredDirection : Vector3.zero;
            Vector3 faceDir = best != null ? (bestIntent.FaceOverride ?? bestIntent.DesiredDirection) : Vector3.zero;
            bool wantsMove = best != null && bestIntent.Move;
            float speedMultiplier = best != null && bestIntent.SpeedMultiplier > 0f ? bestIntent.SpeedMultiplier : 1f;

            Vector3 finalDir = behaviorDir;

            if (boundaryAvoidance != null && boundaryAvoidance.TryGetAvoidance(out var awayDir, out var urgency))
            {
                Vector3 combined = behaviorDir.normalized * (1f - urgency) + awayDir * urgency;
                if (combined.sqrMagnitude < 0.01f)
                {
                    // 行動方向と回避方向がほぼ正反対で打ち消し合う場合、境界に沿った横方向へ逃がす。
                    combined = Vector3.Cross(Vector3.up, awayDir);
                }
                finalDir = combined;
                wantsMove = true;
                if (faceDir.sqrMagnitude < 0.0001f || urgency > 0.5f) faceDir = finalDir;
            }

            if (posture != null && faceDir.sqrMagnitude > 0.0001f)
            {
                posture.DesiredFacingDirection = faceDir;
            }

            if (wantsMove && finalDir.sqrMagnitude > 0.0001f)
            {
                Vector3 desiredDir = finalDir.normalized;

                // 目的方向(水平面)に対して横向きの速度成分を減衰させ、公転・オーバーシュートを防ぐ。
                // Y成分は触らない(Bタイプの揚力・落下など垂直方向の制御を妨げないため)。
                Vector3 desiredDirFlat = new Vector3(desiredDir.x, 0f, desiredDir.z);
                if (desiredDirFlat.sqrMagnitude > 0.0001f)
                {
                    desiredDirFlat.Normalize();
                    Vector3 velFlat = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    Vector3 lateralVel = velFlat - Vector3.Dot(velFlat, desiredDirFlat) * desiredDirFlat;
                    rb.AddForce(-lateralVel * LateralDamping, ForceMode.Acceleration);
                }

                float alignment = Vector3.Dot(transform.forward, desiredDir);
                rb.AddForce(transform.forward * (stats.MoveSpeed * speedMultiplier * alignment), ForceMode.Acceleration);
            }
        }
    }
}
