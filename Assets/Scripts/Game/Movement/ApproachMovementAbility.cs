using UnityEngine;

namespace Game
{
    // 移動タイプ「敵に近づく」。一番近くの敵に向かって直進する。
    // 技(スキル)とは完全に独立しており、このコンポーネント自体は攻撃や効果を一切持たない。
    [RequireComponent(typeof(CharacterIdentity))]
    public class ApproachMovementAbility : MonoBehaviour, IMovementIntentSource
    {
        public int MovementPriority => 10;

        CharacterIdentity identity;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            intent = new MovementIntent { DesiredDirection = dir.normalized, Move = true };
            return true;
        }
    }
}
