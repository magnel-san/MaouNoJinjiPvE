using UnityEngine;

namespace Game
{
    // Hタイプ: 支援。敵からは距離を取りつつ(Dタイプに近い自衛)、最も減っている味方の近くへ寄って
    // 定期的に回復する。攻撃能力は持たない、味方を援護する専門のキャラ。
    [RequireComponent(typeof(CharacterIdentity))]
    public class SupportHealAbility : MonoBehaviour, IMovementIntentSource
    {
        [Header("自衛(敵からの距離維持)")]
        public float MinDistanceFromEnemy = 6f;

        [Header("回復")]
        public float HealRange = 5f;
        [Range(1, 5)] public int HealCooldownTier = 3;
        [Range(1, 5)] public int HealAmountTier = 3;
        [Tooltip("回復のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip HealSound;

        public int MovementPriority => 10;

        static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var target = FindMostWoundedAllyInRange();
            if (target == null) return;

            var health = target.GetComponent<CharacterHealth>();
            if (health == null) return;

            var cfg = GameBalanceConfig.Instance;
            var amount = cfg != null ? cfg.SupportHealAmount.Get(HealAmountTier) : 15f;
            health.Heal(amount);

            HealBeamEffect.Spawn(transform.position + Vector3.up, target.transform.position + Vector3.up, HealColor);
            SfxUtil.PlayAt(HealSound, target.transform.position);

            cooldownTimer = cfg != null ? cfg.SupportHealCooldown.Get(HealCooldownTier) : 4f;
        }

        CharacterIdentity FindMostWoundedAllyInRange()
        {
            CharacterIdentity best = null;
            var bestMissingRatio = 0f;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c == identity || c.Team != identity.Team || !c.IsAlive) continue;
                if (Vector3.Distance(transform.position, c.transform.position) > HealRange) continue;

                var health = c.GetComponent<CharacterHealth>();
                var stats = c.GetComponent<CharacterStats>();
                if (health == null || stats == null || stats.MaxHP <= 0f) continue;
                if (health.CurrentHP >= stats.MaxHP) continue;

                var missingRatio = 1f - health.CurrentHP / stats.MaxHP;
                if (missingRatio > bestMissingRatio)
                {
                    bestMissingRatio = missingRatio;
                    best = c;
                }
            }
            return best;
        }

        CharacterIdentity FindNearestAlly()
        {
            CharacterIdentity best = null;
            var bestDistSqr = float.MaxValue;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c == identity || c.Team != identity.Team || !c.IsAlive) continue;
                var d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistSqr) { bestDistSqr = d; best = c; }
            }
            return best;
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;

            // 敵が間近に迫っている間は回復より自衛(離脱)を優先する。
            var nearestEnemy = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (nearestEnemy != null)
            {
                var toEnemy = nearestEnemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.magnitude < MinDistanceFromEnemy && toEnemy.sqrMagnitude > 0.0001f)
                {
                    var away = -toEnemy.normalized;
                    intent = new MovementIntent { DesiredDirection = away, Move = true, FaceOverride = away };
                    return true;
                }
            }

            var ally = FindMostWoundedAllyInRange() ?? FindNearestAlly();
            if (ally == null) return false;

            var toAlly = ally.transform.position - transform.position;
            toAlly.y = 0f;
            var dist = toAlly.magnitude;
            if (dist < HealRange * 0.6f) return false; // 十分近いので待機して回復を待つ

            intent = new MovementIntent { DesiredDirection = toAlly.normalized, Move = true };
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = HealColor;
            TargetingUtility.DrawGizmoCircle(transform.position, HealRange);
            Gizmos.color = new Color(1f, 0.4f, 0.4f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistanceFromEnemy);
        }
    }
}
