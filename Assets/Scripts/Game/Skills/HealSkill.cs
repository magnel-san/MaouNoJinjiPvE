using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「回復エリア」。移動ロジックとは無関係に、クールダウンで自身を中心とした回復の波動を
    // 円状に広げ(GuardAuraSkillの防御フィールドと同じ「波状」の見た目言語)、その瞬間に範囲内へ
    // 居合わせた欠損した味方全員へ、その場で1回だけ回復を与える(継続回復ではなく即時回復)。
    [RequireComponent(typeof(CharacterIdentity))]
    public class HealSkill : MonoBehaviour
    {
        [Header("回復")]
        public float HealRange = 5f;
        [Range(1, 3)] public int HealCooldownTier = 2;
        [Range(1, 3)] public int HealAmountTier = 2;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip HealSound;

        static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);
        static readonly Color HealPulseColor = new Color(0.7f, 1f, 0.75f);

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;
            if (!HasWoundedAllyInRange()) return;

            TriggerHealWave();
        }

        bool HasWoundedAllyInRange()
        {
            foreach (var c in CharacterRegistry.All)
            {
                if (IsHealable(c)) return true;
            }
            return false;
        }

        bool IsHealable(CharacterIdentity c)
        {
            if (c == null || c == identity || c.Team != identity.Team || !c.IsAlive) return false;
            if (Vector3.Distance(transform.position, c.transform.position) > HealRange) return false;

            var health = c.GetComponent<CharacterHealth>();
            var stats = c.GetComponent<CharacterStats>();
            if (health == null || stats == null || stats.MaxHP <= 0f) return false;
            return health.CurrentHP < stats.MaxHP;
        }

        void TriggerHealWave()
        {
            var cfg = GameBalanceConfig.Instance;
            var amount = cfg != null ? cfg.SupportHealAmount.Get(HealAmountTier) : 15f;
            cooldownTimer = cfg != null ? cfg.SupportHealCooldown.Get(HealCooldownTier) : 4f;

            SfxUtil.PlayAt(HealSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "回復エリア");
            StartCoroutine(CoEmitWave());

            // 波動が広がる前に居合わせた味方はまとめて即時回復する(継続回復ではなく1回きり)。
            var healed = new List<CharacterIdentity>();
            foreach (var c in CharacterRegistry.All)
            {
                if (!IsHealable(c)) continue;
                healed.Add(c);
            }

            foreach (var c in healed)
            {
                var health = c.GetComponent<CharacterHealth>();
                health.Heal(amount);
                CombatFx.HealPopup(c.transform.position, amount);
                CombatFx.ImpactBurst(c.transform.position + Vector3.up, HealColor, 0.3f);
            }
        }

        // 防御フィールドと同じ「波状に広がる円」の見た目。少し間隔を空けて2重に出すことで
        // 波動らしい広がりを表現する。
        IEnumerator CoEmitWave()
        {
            ExplosionRingEffect.Spawn(transform.position, HealRange * 0.5f, HealPulseColor, 0.5f);
            yield return new WaitForSeconds(0.12f);
            ExplosionRingEffect.Spawn(transform.position, HealRange, HealColor, 0.6f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = HealColor;
            TargetingUtility.DrawGizmoCircle(transform.position, HealRange);
        }
    }
}
