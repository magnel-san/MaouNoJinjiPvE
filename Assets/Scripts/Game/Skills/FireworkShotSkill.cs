using UnityEngine;

namespace Game
{
    // スキル「花火攻撃」。移動ロジックとは無関係に、射程内の一番近い敵へクールダウンで花火を放つ。
    // 花火は矢と同じ直進ロジックだが、最初に触れたキャラの位置で爆発して範囲ダメージ・吹き飛ばしを与える。
    [RequireComponent(typeof(CharacterIdentity))]
    public class FireworkShotSkill : MonoBehaviour
    {
        [Header("射程・威力")]
        public float AttackRange = 8f;
        [Range(1, 3)] public int AttackCooldownTier = 2;
        public GameObject FireworkPrefab;
        public float FireworkSpeed = 30f;
        public float ExplosionDamage = 12f;
        public float ExplosionRadius = 2.5f;
        public float KnockbackVector = 20f;
        public float FireworkLifetime = 4f;
        [Tooltip("発射のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip FireSound;

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return;
            if (Vector3.Distance(transform.position, target.transform.position) > AttackRange) return;

            FireFirework(target);
            var cfg = GameBalanceConfig.Instance;
            cooldownTimer = cfg != null ? cfg.FireworkAttackCooldown.Get(AttackCooldownTier) : 3f;
        }

        void FireFirework(CharacterIdentity target)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;

            GameObject go = FireworkPrefab != null
                ? Instantiate(FireworkPrefab, transform.position, Quaternion.LookRotation(dir))
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (FireworkPrefab == null)
            {
                go.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(dir));
                go.transform.localScale = Vector3.one * 0.3f;
            }

            var firework = go.GetComponent<FireworkProjectile>();
            if (firework == null) firework = go.AddComponent<FireworkProjectile>();
            firework.Initialize(dir, FireworkSpeed, ExplosionDamage, ExplosionRadius, KnockbackVector, FireworkLifetime, identity);

            SfxUtil.PlayAt(FireSound, transform.position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, AttackRange);
        }
    }
}
