using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「反射フィールド」。移動ロジックとは無関係に、被弾した瞬間(CharacterHealth.OnHPChangedで
    // HPが減ったことを検知)にクールダウン付きで自身中心の波状ダメージ+ノックバックを発生させる。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth))]
    public class ReflectFieldSkill : MonoBehaviour
    {
        [Range(1, 3)] public int Tier = 2;
        public float KnockbackVector = 16f;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip WaveSound;

        static readonly Color WaveColor = new Color(0.4f, 0.75f, 1f);
        static readonly Color WaveCoreColor = new Color(0.75f, 0.95f, 1f);

        CharacterIdentity identity;
        CharacterHealth health;
        float lastKnownHp;
        float cooldownTimer;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            health = GetComponent<CharacterHealth>();
        }

        void OnEnable()
        {
            lastKnownHp = health.CurrentHP;
            health.OnHPChanged += HandleHpChanged;
        }

        void OnDisable() => health.OnHPChanged -= HandleHpChanged;

        void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        }

        void HandleHpChanged(float current, float max)
        {
            var damaged = current < lastKnownHp;
            lastKnownHp = current;
            if (!damaged || cooldownTimer > 0f) return;

            TriggerWave();
        }

        void TriggerWave()
        {
            var cfg = GameBalanceConfig.Instance;
            var radius = cfg != null ? cfg.ReflectFieldRadius.Get(Tier) : 3.5f;
            var damage = cfg != null ? cfg.ReflectFieldDamage.Get(Tier) : 20f;
            cooldownTimer = cfg != null ? cfg.ReflectFieldCooldown.Get(Tier) : 2.3f;

            // 被弾に即座に反応した合図として自身をパッと光らせてから、波状に2重の衝撃波を広げる。
            CombatFx.HitFlash(transform, WaveCoreColor);
            StartCoroutine(CoEmitWave(radius));
            SfxUtil.PlayAt(WaveSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "反射フィールド");

            var hits = Physics.OverlapSphere(transform.position, radius);
            var affected = new HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var otherIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (otherIdentity == null || otherIdentity == identity || otherIdentity.Team == identity.Team) continue;
                if (!affected.Add(otherIdentity)) continue;

                var otherHealth = otherIdentity.GetComponent<CharacterHealth>();
                if (otherHealth == null || !otherHealth.IsAlive) continue;
                otherHealth.ApplyDamage(damage, WaveColor, identity);

                var rb = otherIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var dir = otherIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    rb.AddForce(dir.normalized * KnockbackVector, ForceMode.VelocityChange);
                }
            }
        }

        // 中心から外へ2重に広がる衝撃波の見た目(すぐ小さい波が出て、少し遅れて全範囲まで広がる波が続く)。
        IEnumerator CoEmitWave(float radius)
        {
            ExplosionRingEffect.Spawn(transform.position, radius * 0.4f, WaveCoreColor, 0.3f);
            yield return new WaitForSeconds(0.08f);
            ExplosionRingEffect.Spawn(transform.position, radius, WaveColor, 0.45f);
        }

        void OnDrawGizmosSelected()
        {
            var cfg = GameBalanceConfig.Instance;
            var radius = cfg != null ? cfg.ReflectFieldRadius.Get(Tier) : 3.5f;
            Gizmos.color = WaveColor;
            TargetingUtility.DrawGizmoCircle(transform.position, radius);
        }
    }
}
