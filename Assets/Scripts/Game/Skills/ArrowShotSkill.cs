using UnityEngine;

namespace Game
{
    // スキル「矢攻撃」。移動ロジックとは無関係に、射程内の一番近い敵へクールダウンで矢を放つ。
    // 汎用の近距離弾攻撃スキルとして、回復スキル(HealSkill)を持つキャラの手持ち無沙汰対策にも使える。
    [RequireComponent(typeof(CharacterIdentity))]
    public class ArrowShotSkill : MonoBehaviour
    {
        [Header("射程・威力")]
        public float AttackRange = 8f;
        [Range(1, 3)] public int AttackCooldownTier = 2;
        public GameObject ArrowPrefab;
        public float ArrowSpeed = 45f;
        public float ArrowDamage = 8f;
        public float ArrowLifetime = 3f;
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

            FireArrow(target);
            var cfg = GameBalanceConfig.Instance;
            cooldownTimer = cfg != null ? cfg.ArrowShotCooldown.Get(AttackCooldownTier) : 2.5f;
        }

        void FireArrow(CharacterIdentity target)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;

            GameObject arrowGO = ArrowPrefab != null
                ? Instantiate(ArrowPrefab, transform.position, Quaternion.LookRotation(dir))
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            if (ArrowPrefab == null)
            {
                arrowGO.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(dir));
                arrowGO.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            }

            var arrow = arrowGO.GetComponent<ArrowProjectile>();
            if (arrow == null) arrow = arrowGO.AddComponent<ArrowProjectile>();
            arrow.Initialize(dir, ArrowSpeed, ArrowDamage, ArrowLifetime, identity);

            SfxUtil.PlayAt(FireSound, transform.position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 0.3f);
            TargetingUtility.DrawGizmoCircle(transform.position, AttackRange);
        }
    }
}
