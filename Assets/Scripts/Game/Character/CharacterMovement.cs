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

        [Header("切り返し・操作性向上")]
        [Tooltip("進行方向と「逆」に進んでいる際に、前の慣性を打ち消すブレーキの強さ（高いほどキレのある切り返しになります）")]
        public float ReverseBrakePower = 15f;

        [Header("勝利演出設定")]
        [Tooltip("勝利時に跳ねる力")]
        private float BounceForce = 10f;
        [Tooltip("跳ねる間隔(秒)")]
        public float BounceInterval = 0.6f;

        Rigidbody rb;
        CharacterStats stats;
        CharacterPosture posture;
        BoundaryAvoidance boundaryAvoidance;
        CharacterIdentity identity;
        readonly List<IMovementIntentSource> sources = new List<IMovementIntentSource>();

        float bounceTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<CharacterStats>();
            posture = GetComponent<CharacterPosture>();
            boundaryAvoidance = GetComponent<BoundaryAvoidance>();
            identity = GetComponent<CharacterIdentity>();
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
            // 自分のチームが勝利しているか判定
            if (IsVictoryConditionMet())
            {
                // レイヤーが "Yusha" ではない場合のみ勝利演出を行う
                int yushaLayer = LayerMask.NameToLayer("Yusha");
                bool isYushaLayer = (yushaLayer != -1 && gameObject.layer == yushaLayer);

                if (!isYushaLayer)
                {
                    PerformVictoryAction();
                    return; // 勝利演出中は通常の移動処理を行わない
                }
            }

            // --- 以下、通常の移動処理 ---
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

                Vector3 currentVelFlat = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                Vector3 desiredDirFlat = new Vector3(desiredDir.x, 0f, desiredDir.z);

                if (desiredDirFlat.sqrMagnitude > 0.0001f)
                {
                    desiredDirFlat.Normalize();

                    Vector3 lateralVel = currentVelFlat - Vector3.Dot(currentVelFlat, desiredDirFlat) * desiredDirFlat;
                    rb.AddForce(-lateralVel * LateralDamping, ForceMode.Acceleration);

                    float forwardSpeed = Vector3.Dot(currentVelFlat, desiredDirFlat);
                    if (forwardSpeed < 0f)
                    {
                        Vector3 reverseVel = forwardSpeed * desiredDirFlat;
                        rb.AddForce(-reverseVel * ReverseBrakePower, ForceMode.Acceleration);
                    }
                }

                float alignment = Vector3.Dot(transform.forward, desiredDir);
                rb.AddForce(transform.forward * (stats.MoveSpeed * speedMultiplier * alignment), ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// カメラを向いてぴょんぴょんと跳ねる勝利演出
        /// </summary>
        private void PerformVictoryAction()
        {
            // 1. メインカメラの方を向く
            var mainCam = Camera.main;
            if (mainCam != null && posture != null)
            {
                Vector3 lookDir = mainCam.transform.position - transform.position;
                lookDir.y = 0f; // 水平方向のみ向きを変える
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    posture.DesiredFacingDirection = lookDir.normalized;
                }
            }

            // 2. ぴょんぴょんと跳ねる (一定時間ごとにジャンプ)
            bounceTimer += Time.fixedDeltaTime;
            if (bounceTimer >= BounceInterval)
            {
                bounceTimer = 0f;

                // 地面に近い場合のみ跳ねる（連打で空中へ飛び上がるのを防ぐ）
                if (Mathf.Abs(rb.linearVelocity.y) < 0.5f)
                {
                    rb.AddForce(Vector3.up * BounceForce, ForceMode.Impulse);
                }
            }
        }

        /// <summary>
        /// 自分のチームが勝利したかどうかの判定処理
        /// </summary>
        private bool IsVictoryConditionMet()
        {
            if (identity == null) return false;

            // 1. 最終決戦(勇者)を撃破してゲームクリア中・リザルト表示中であれば無条件で勝利
            if (Game.Flow.GameFlowManager.IsGameCleared)
            {
                return true;
            }

            // 2. 通常ラウンド用の敵全滅チェック
            bool hasLivingEnemy = false;
            foreach (var character in CharacterRegistry.All)
            {
                if (character != null && character.Team != identity.Team && character.IsAlive)
                {
                    hasLivingEnemy = true;
                    break;
                }
            }

            return !hasLivingEnemy;
        }
    }
}
