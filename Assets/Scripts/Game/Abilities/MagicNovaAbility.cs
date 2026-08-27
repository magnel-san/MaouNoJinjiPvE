using UnityEngine;

namespace Game
{
    // Eタイプ: 魔法。ランダムな生存敵に向かって直進する(その敵が死ぬまで対象を変えない)。
    // 感知距離内に敵が入り、クールダウンが明けていれば自身中心の吹き飛ばしを発動する。
    // 吹き飛ばしは自身以外の全キャラに当たり(味方も含む)、少量ダメージも与える。
    [RequireComponent(typeof(CharacterIdentity))]
    public class MagicNovaAbility : MonoBehaviour, IMovementIntentSource
    {
        [Header("感知・発動")]
        public float SenseDistance = 5f;
        [Range(1, 5)] public int CooldownTier = 3;

        [Header("吹き飛ばし")]
        public float KnockbackVector = 20f;
        [Range(1, 5)] public int BlastRadiusTier = 3;
        [Range(1, 5)] public int DamageTier = 2;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip NovaSound;

        public int MovementPriority => 9;

        CharacterIdentity identity;
        CharacterIdentity chaseTarget;
        float cooldownTimer;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            TargetingUtility.CreateRangeGizmoCollider(transform, "MagicSenseRange", SenseDistance);
        }

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var nearestEnemy = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (nearestEnemy == null) return;

            float dist = Vector3.Distance(transform.position, nearestEnemy.transform.position);
            if (dist > SenseDistance) return;

            TriggerNova();
            var cfg = GameBalanceConfig.Instance;
            cooldownTimer = cfg != null ? cfg.MagicKnockbackCooldown.Get(CooldownTier) : 5f;
        }

        void TriggerNova()
        {
            var cfg = GameBalanceConfig.Instance;
            float radius = cfg != null ? cfg.MagicKnockbackRadius.Get(BlastRadiusTier) : 3f;
            float damage = cfg != null ? cfg.MagicKnockbackDamage.Get(DamageTier) : 5f;

            var color = new Color(0.6f, 0.3f, 1f);
            ExplosionRingEffect.Spawn(transform.position, radius, color);
            SfxUtil.PlayAt(NovaSound, transform.position);

            var hits = Physics.OverlapSphere(transform.position, radius);
            var affected = new System.Collections.Generic.HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var otherIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (otherIdentity == null || otherIdentity == identity || !affected.Add(otherIdentity)) continue;

                var health = otherIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, color);

                var rb = otherIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = otherIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    // 質量に関わらず一定の速度変化を与える(重いキャラでも魔法でしっかり吹き飛ぶように)。
                    rb.AddForce(dir.normalized * KnockbackVector, ForceMode.VelocityChange);
                }
            }
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            if (chaseTarget == null || !chaseTarget.IsAlive)
            {
                chaseTarget = TargetingUtility.FindRandomLivingEnemy(identity.Team);
            }
            if (chaseTarget == null) return false;

            Vector3 dir = chaseTarget.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            intent = new MovementIntent { DesiredDirection = dir.normalized, Move = true };
            return true;
        }

        // デバッグ表示用: 選択時のみ感知距離と吹き飛ばし範囲をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f);
            TargetingUtility.DrawGizmoCircle(transform.position, SenseDistance);

            var cfg = GameBalanceConfig.Instance;
            float radius = cfg != null ? cfg.MagicKnockbackRadius.Get(BlastRadiusTier) : 3f;
            Gizmos.color = new Color(1f, 0.2f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, radius);
        }
    }
}
