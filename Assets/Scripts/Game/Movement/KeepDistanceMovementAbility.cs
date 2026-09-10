using UnityEngine;

namespace Game
{
    // 移動タイプ「敵と一定距離を保つ」。一番近くの敵から固定距離(Max〜Min)を保つように移動する。
    // 技(スキル)とは完全に独立しており、このコンポーネント自体は攻撃や効果を一切持たない。
    [RequireComponent(typeof(CharacterIdentity))]
    public class KeepDistanceMovementAbility : MonoBehaviour, IMovementIntentSource, IDistanceHoldingAbility
    {
        [Header("距離維持")]
        public float MaxDistance = 8f;
        public float MinDistance = 4f;

        public int MovementPriority => 10;
        public bool IsHoldingDistance { get; private set; }

        CharacterIdentity identity;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null)
            {
                IsHoldingDistance = false;
                return;
            }

            var dist = Vector3.Distance(transform.position, target.transform.position);
            IsHoldingDistance = dist <= MaxDistance && dist >= MinDistance;
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.0001f) return false;
            Vector3 flatDirToTarget = toTarget / dist;

            Vector3 moveDir;
            bool move;
            if (dist > MaxDistance) { moveDir = flatDirToTarget; move = true; }
            else if (dist < MinDistance) { moveDir = -flatDirToTarget; move = true; }
            else { moveDir = Vector3.zero; move = false; }

            intent = new MovementIntent { DesiredDirection = moveDir, FaceOverride = flatDirToTarget, Move = move };
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MaxDistance);
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistance);
        }
    }
}
