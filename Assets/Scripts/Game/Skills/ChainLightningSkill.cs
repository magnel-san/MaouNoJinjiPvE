using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「連鎖雷撃」。移動ロジックとは無関係に、射程内の一番近い敵へクールダウンで雷撃を放つ。
    // 雷撃は最初の対象で止まらず、未着弾の近くの敵へ連鎖して最大MaxChainJumps回まで飛び移る。
    [RequireComponent(typeof(CharacterIdentity))]
    public class ChainLightningSkill : MonoBehaviour
    {
        [Header("射程・威力")]
        public float AttackRange = 9f;
        [Range(1, 3)] public int CooldownTier = 2;
        [Range(1, 3)] public int DamageTier = 2;
        [Tooltip("最初の対象から何回まで飛び移るか(0なら単体攻撃)")]
        public int MaxChainJumps = 2;
        [Tooltip("直前の着弾地点からこの距離以内の未着弾の敵にだけ飛び移る")]
        public float ChainJumpRadius = 5f;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip ZapSound;

        static readonly Color BoltColor = new Color(0.55f, 0.85f, 1f);

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

            FireChain(target);
            var cfg = GameBalanceConfig.Instance;
            cooldownTimer = cfg != null ? cfg.ChainLightningCooldown.Get(CooldownTier) : 4f;
        }

        void FireChain(CharacterIdentity firstTarget)
        {
            var cfg = GameBalanceConfig.Instance;
            var damage = cfg != null ? cfg.ChainLightningDamage.Get(DamageTier) : 8f;

            SfxUtil.PlayAt(ZapSound, transform.position);

            var hit = new HashSet<CharacterIdentity>();
            var from = transform.position + Vector3.up;
            var current = firstTarget;

            for (var jump = 0; current != null && jump <= MaxChainJumps; jump++)
            {
                hit.Add(current);

                var health = current.GetComponent<CharacterHealth>();
                if (health != null && health.IsAlive) health.ApplyDamage(damage, BoltColor, identity);

                var to = current.transform.position + Vector3.up;
                LightningBoltEffect.Spawn(from, to, BoltColor);

                from = to;
                current = FindNextChainTarget(current.transform.position, hit);
            }
        }

        CharacterIdentity FindNextChainTarget(Vector3 fromPosition, HashSet<CharacterIdentity> exclude)
        {
            CharacterIdentity best = null;
            var bestDistSqr = ChainJumpRadius * ChainJumpRadius;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team == identity.Team || !c.IsAlive || exclude.Contains(c)) continue;
                var d = (c.transform.position - fromPosition).sqrMagnitude;
                if (d < bestDistSqr)
                {
                    bestDistSqr = d;
                    best = c;
                }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f);
            TargetingUtility.DrawGizmoCircle(transform.position, AttackRange);
            Gizmos.color = BoltColor;
            TargetingUtility.DrawGizmoCircle(transform.position, ChainJumpRadius);
        }
    }
}
