using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // Iタイプ: 連鎖雷撃。CタイプやFタイプと同じく一定距離を保ちながら、クールダウンで
    // 最も近い敵へ雷撃を放つ。雷撃は最初の対象で止まらず、未着弾の近くの敵へ連鎖して
    // 最大MaxChainJumps回まで飛び移る(既存のどのアビリティとも違う「複数体を一撃で巻き込む」タイプ)。
    [RequireComponent(typeof(CharacterIdentity))]
    public class ChainLightningAbility : MonoBehaviour, IMovementIntentSource, IDistanceHoldingAbility
    {
        [Header("距離維持")]
        public float MaxDistance = 9f;
        public float MinDistance = 5f;

        [Header("連鎖雷撃 (クールダウン/ダメージは5段階でGameBalanceConfig参照)")]
        [Range(1, 5)] public int CooldownTier = 3;
        [Range(1, 5)] public int DamageTier = 2;
        [Tooltip("最初の対象から何回まで飛び移るか(0なら単体攻撃)")]
        public int MaxChainJumps = 2;
        [Tooltip("直前の着弾地点からこの距離以内の未着弾の敵にだけ飛び移る")]
        public float ChainJumpRadius = 5f;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip ZapSound;

        public int MovementPriority => 10;
        public bool IsHoldingDistance { get; private set; }

        static readonly Color BoltColor = new Color(0.55f, 0.85f, 1f);

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

            var dist = Vector3.Distance(transform.position, target.transform.position);
            IsHoldingDistance = dist <= MaxDistance && dist >= MinDistance;

            if (IsHoldingDistance && cooldownTimer <= 0f)
            {
                FireChain(target);
                var cfg = GameBalanceConfig.Instance;
                cooldownTimer = cfg != null ? cfg.ChainLightningCooldown.Get(CooldownTier) : 4f;
            }
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
                if (health != null && health.IsAlive) health.ApplyDamage(damage, BoltColor);

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

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            var dist = toTarget.magnitude;
            if (dist < 0.0001f) return false;
            var flatDirToTarget = toTarget / dist;

            Vector3 moveDir;
            bool move;
            if (dist > MaxDistance) { moveDir = flatDirToTarget; move = true; }
            else if (dist < MinDistance) { moveDir = -flatDirToTarget; move = true; }
            else { moveDir = Vector3.zero; move = false; }

            intent = new MovementIntent { DesiredDirection = moveDir, FaceOverride = flatDirToTarget, Move = move };
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MaxDistance);
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistance);
            Gizmos.color = BoltColor;
            TargetingUtility.DrawGizmoCircle(transform.position, ChainJumpRadius);
        }
    }
}
