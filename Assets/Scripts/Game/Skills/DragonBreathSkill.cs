using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「ドラゴンブレス」。移動ロジックとは無関係に、射程内の一番近い敵がいればクールダウンで
    // その方向へ前方矩形範囲の炎ダメージを放つ。
    [RequireComponent(typeof(CharacterIdentity))]
    public class DragonBreathSkill : MonoBehaviour
    {
        [Range(1, 3)] public int Tier = 2;
        public float BreathWidth = 3f;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip BreathSound;

        static readonly Color FlameColor = new Color(1f, 0.45f, 0.1f);
        static readonly Color FlameCoreColor = new Color(1f, 0.85f, 0.3f);

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var cfg = GameBalanceConfig.Instance;
            var range = cfg != null ? cfg.DragonBreathRange.Get(Tier) : 5.5f;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return;

            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            var dist = toTarget.magnitude;
            if (dist > range || dist < 0.0001f) return;

            FireBreath(toTarget.normalized, range);
            cooldownTimer = cfg != null ? cfg.DragonBreathCooldown.Get(Tier) : 4f;
        }

        void FireBreath(Vector3 direction, float range)
        {
            var cfg = GameBalanceConfig.Instance;
            var damage = cfg != null ? cfg.DragonBreathDamage.Get(Tier) : 24f;

            transform.forward = direction;
            SfxUtil.PlayAt(BreathSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "ドラゴンブレス");

            // 口元がパッと光ってから炎が奔る「溜め→放出」の流れにする。
            CombatFx.ImpactBurst(transform.position + Vector3.up * 0.6f, FlameCoreColor, 0.35f);
            StartCoroutine(CoEmitFlameStream(direction, range));

            var center = transform.position + Vector3.up * 0.6f + direction * (range * 0.5f);
            var halfExtents = new Vector3(BreathWidth * 0.5f, 1f, range * 0.5f);
            var rotation = Quaternion.LookRotation(direction, Vector3.up);

            var hits = Physics.OverlapBox(center, halfExtents, rotation);
            var affected = new HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var otherIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (otherIdentity == null || otherIdentity == identity || otherIdentity.Team == identity.Team) continue;
                if (!affected.Add(otherIdentity)) continue;

                var health = otherIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, FlameColor, identity);
            }
        }

        // 根元から先端へ炎が流れていくように、リングを間隔を空けて順番に出す
        // (判定はFireBreath側で既に即座に処理済みなので、これは見た目のみの後追い演出)。
        IEnumerator CoEmitFlameStream(Vector3 direction, float range)
        {
            const int steps = 8;
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var pos = transform.position + direction * (range * t) + Vector3.up * 0.3f;
                var color = Color.Lerp(FlameCoreColor, FlameColor, t);
                var size = Mathf.Lerp(BreathWidth * 0.25f, BreathWidth * 0.45f, t);
                ExplosionRingEffect.Spawn(pos, size, color, 0.3f);
                yield return new WaitForSeconds(0.025f);
            }
        }

        void OnDrawGizmosSelected()
        {
            var cfg = GameBalanceConfig.Instance;
            var range = cfg != null ? cfg.DragonBreathRange.Get(Tier) : 5.5f;
            Gizmos.color = FlameColor;
            TargetingUtility.DrawGizmoCircle(transform.position, range);
        }
    }
}
