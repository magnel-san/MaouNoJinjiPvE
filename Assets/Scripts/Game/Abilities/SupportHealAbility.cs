using UnityEngine;

namespace Game
{
    // Hタイプ: 支援。敵からは距離を取りつつ(Dタイプに近い自衛)、最も減っている味方の近くへ寄って
    // 定期的に回復する。回復対象が居ない間(全員フルHP等)は簡易な弾を放って攻撃もする
    // (回復キャラのみの編成でも手持ち無沙汰にならないようにするため)。
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

        [Header("攻撃(回復対象が居ない間だけ、ある程度は自分でも攻撃する)")]
        [Tooltip("この距離以内の敵にのみ攻撃する")]
        public float AttackRange = 7f;
        [Tooltip("攻撃の再発動間隔(秒)")]
        public float AttackCooldown = 3.5f;
        [Tooltip("他の攻撃キャラと同程度の威力にしてある")]
        public float AttackDamage = 10f;
        public float BoltSpeed = 30f;
        [Tooltip("未設定なら簡易的な球体を代わりに使う")]
        public GameObject BoltPrefab;
        [Tooltip("攻撃のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip AttackSound;

        public int MovementPriority => 10;

        static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);

        CharacterIdentity identity;
        float cooldownTimer;
        float attackCooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            attackCooldownTimer -= Time.deltaTime;

            var target = FindMostWoundedAllyInRange();
            if (target != null)
            {
                if (cooldownTimer <= 0f) HealTarget(target);
                return;
            }

            // 回復対象が居ない間(全員フルHP等)は、ある程度は自分でも攻撃して手持ち無沙汰にならないようにする。
            TryAttackNearestEnemy();
        }

        void HealTarget(CharacterIdentity target)
        {
            var health = target.GetComponent<CharacterHealth>();
            if (health == null) return;

            var cfg = GameBalanceConfig.Instance;
            var amount = cfg != null ? cfg.SupportHealAmount.Get(HealAmountTier) : 15f;
            health.Heal(amount);

            HealBeamEffect.Spawn(transform.position + Vector3.up, target.transform.position + Vector3.up, HealColor);
            SfxUtil.PlayAt(HealSound, target.transform.position);

            cooldownTimer = cfg != null ? cfg.SupportHealCooldown.Get(HealCooldownTier) : 4f;
        }

        void TryAttackNearestEnemy()
        {
            if (attackCooldownTimer > 0f) return;

            var enemy = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (enemy == null) return;
            if (Vector3.Distance(transform.position, enemy.transform.position) > AttackRange) return;

            FireBolt(enemy);
            attackCooldownTimer = AttackCooldown;
        }

        void FireBolt(CharacterIdentity target)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;

            GameObject go = BoltPrefab != null
                ? Instantiate(BoltPrefab, transform.position, Quaternion.LookRotation(dir))
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (BoltPrefab == null)
            {
                go.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(dir));
                go.transform.localScale = Vector3.one * 0.3f;
            }

            var bolt = go.GetComponent<ArrowProjectile>();
            if (bolt == null) bolt = go.AddComponent<ArrowProjectile>();
            bolt.Initialize(dir, BoltSpeed, AttackDamage, 3f, identity);

            SfxUtil.PlayAt(AttackSound, transform.position);
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
