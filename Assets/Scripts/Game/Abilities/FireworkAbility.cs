using UnityEngine;

namespace Game
{
    // Fタイプ: 花火。一番近くの敵から固定距離(最大〜最小)を保つように移動し(Cタイプと同じ移動ロジック)、
    // クールダウンで花火を発射する。花火は矢と同じ直進ロジックだが、最初に触れたキャラの位置で
    // 爆発して範囲ダメージ・吹き飛ばしを与えてから消滅する。
    [RequireComponent(typeof(CharacterIdentity))]
    public class FireworkAbility : MonoBehaviour, IMovementIntentSource, IDistanceHoldingAbility
    {
        [Header("距離維持")]
        public float MaxDistance = 8f;
        public float MinDistance = 4f;

        [Header("花火 (攻撃速度は5段階でGameBalanceConfig参照)")]
        [Range(1, 5)] public int AttackCooldownTier = 3;
        public GameObject FireworkPrefab;
        public float FireworkSpeed = 30f;
        public float ExplosionDamage = 12f;
        public float ExplosionRadius = 2.5f;
        public float KnockbackVector = 20f;
        public float FireworkLifetime = 4f;
        [Tooltip("発射のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip FireSound;

        public int MovementPriority => 10;
        public bool IsHoldingDistance { get; private set; }

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null)
            {
                IsHoldingDistance = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, target.transform.position);
            IsHoldingDistance = dist <= MaxDistance && dist >= MinDistance;

            if (IsHoldingDistance && cooldownTimer <= 0f)
            {
                FireFirework(target);
                var cfg = GameBalanceConfig.Instance;
                cooldownTimer = cfg != null ? cfg.FireworkAttackCooldown.Get(AttackCooldownTier) : 3f;
            }
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

        // デバッグ表示用: 選択時のみ維持距離の最大・最小をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MaxDistance);
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistance);
        }
    }
}
