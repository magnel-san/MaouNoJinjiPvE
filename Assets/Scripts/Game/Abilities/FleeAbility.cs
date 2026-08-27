using UnityEngine;

namespace Game
{
    // Dタイプ: 逃げる。感知距離内に味方が1体でもいれば、最も近い敵から固定距離(最大〜最小)を保つように離れる。
    // 味方がいなければ最も近い敵へ直進する。Cタイプと併用可能で、その場合はこちらが移動を担う。
    [RequireComponent(typeof(CharacterIdentity))]
    public class FleeAbility : MonoBehaviour, IMovementIntentSource
    {
        [Header("感知")]
        public float AllySenseDistance = 6f;

        [Header("距離維持 (逃げる)")]
        public float MaxDistance = 10f;
        public float MinDistance = 5f;

        public int MovementPriority => 20;

        CharacterIdentity identity;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            TargetingUtility.CreateRangeGizmoCollider(transform, "AllySenseRange", AllySenseDistance);
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

            int allyCount = TargetingUtility.CountAlliesInRange(transform.position, identity.Team, AllySenseDistance, identity);
            if (allyCount > 0)
            {
                Vector3 moveDir;
                bool move;
                if (dist > MaxDistance) { moveDir = flatDirToTarget; move = true; }
                else if (dist < MinDistance) { moveDir = -flatDirToTarget; move = true; }
                else { moveDir = Vector3.zero; move = false; }

                intent = new MovementIntent { DesiredDirection = moveDir, FaceOverride = flatDirToTarget, Move = move };
                return true;
            }

            intent = new MovementIntent { DesiredDirection = flatDirToTarget, Move = true };
            return true;
        }

        // デバッグ表示用: 選択時のみ味方感知距離と維持距離をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            TargetingUtility.DrawGizmoCircle(transform.position, AllySenseDistance);
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MaxDistance);
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistance);
        }
    }
}
