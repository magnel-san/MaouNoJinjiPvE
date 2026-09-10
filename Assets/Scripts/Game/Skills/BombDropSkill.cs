using UnityEngine;

namespace Game
{
    // スキル「爆弾投下」。移動ロジック(FlyMovementAbility等)とは無関係に、射程内の敵を検知したら
    // クールダウンで足元へ爆弾を投下する。かつては飛翔移動と連動して投下時に強制着地していたが、
    // 移動とスキルを完全に分離する方針のため非連動にしてある。
    [RequireComponent(typeof(CharacterIdentity))]
    public class BombDropSkill : MonoBehaviour
    {
        [Header("射程・威力")]
        public float AttackRange = 3f;
        [Range(1, 3)] public int AttackCooldownTier = 2;

        [Header("爆弾")]
        public GameObject BombPrefab;
        public float BombFuseTime = 1.5f;
        public float BombKnockbackVector = 20f;
        public float BombDamage = 15f;
        public float BombExplosionRadius = 3f;
        [Tooltip("投下のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip DropSound;

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return;

            // 高さ(Y)は無視し、XZ平面上の距離だけで範囲判定する(空中の機体でも地上の敵を狙えるように)。
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > AttackRange * AttackRange) return;

            DropBomb();
            var cfg = GameBalanceConfig.Instance;
            cooldownTimer = cfg != null ? cfg.BomberAttackCooldown.Get(AttackCooldownTier) : 4f;
        }

        void DropBomb()
        {
            GameObject bombGO = BombPrefab != null
                ? Instantiate(BombPrefab, transform.position, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (BombPrefab == null)
            {
                bombGO.transform.position = transform.position;
                bombGO.transform.localScale = Vector3.one * 0.5f;
            }

            var bomb = bombGO.GetComponent<BombProjectile>();
            if (bomb == null) bomb = bombGO.AddComponent<BombProjectile>();
            bomb.Initialize(BombFuseTime, BombKnockbackVector, BombDamage, BombExplosionRadius, identity);

            SfxUtil.PlayAt(DropSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "ボム攻撃");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            TargetingUtility.DrawGizmoCircle(transform.position, AttackRange);
        }
    }
}
