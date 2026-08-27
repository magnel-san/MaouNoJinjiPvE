using UnityEngine;

namespace Game
{
    // Aタイプ: 直進型。一番近くの敵に向かって直進する。HPを高めに設定した上で使う想定。
    // ダメージを軽減率(%)でカットする。EタイプとはMovementIntentの形が同じため併用できる。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth))]
    public class RushAttackAbility : MonoBehaviour, IMovementIntentSource
    {
        [Range(1, 5)] public int DamageReductionTier = 3;

        public int MovementPriority => 10;

        CharacterIdentity identity;
        CharacterHealth health;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            health = GetComponent<CharacterHealth>();
        }

        void Start()
        {
            var cfg = GameBalanceConfig.Instance;
            if (cfg != null) health.DamageReductionPercent = cfg.DamageReductionPercent.Get(DamageReductionTier);
        }

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
