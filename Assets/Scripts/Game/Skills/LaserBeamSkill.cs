using UnityEngine;

namespace Game
{
    // スキル「レーザービーム」。移動ロジックとは無関係に、射程内の一番近い敵へクールダウンで
    // 瞬間ビームを放って単発ダメージを与える。
    [RequireComponent(typeof(CharacterIdentity))]
    public class LaserBeamSkill : MonoBehaviour
    {
        [Range(1, 3)] public int Tier = 2;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip BeamSound;

        static readonly Color BeamColor = new Color(1f, 0.2f, 0.25f);

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            var cfg = GameBalanceConfig.Instance;
            var range = cfg != null ? cfg.LaserBeamRange.Get(Tier) : 8f;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return;
            if (Vector3.Distance(transform.position, target.transform.position) > range) return;

            FireBeam(target);
            cooldownTimer = cfg != null ? cfg.LaserBeamCooldown.Get(Tier) : 3f;
        }

        void FireBeam(CharacterIdentity target)
        {
            var cfg = GameBalanceConfig.Instance;
            var damage = cfg != null ? cfg.LaserBeamDamage.Get(Tier) : 20f;

            var from = transform.position + Vector3.up;
            var to = target.transform.position + Vector3.up;
            LaserBeamEffect.Spawn(from, to, BeamColor);
            SfxUtil.PlayAt(BeamSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "レーザービーム");

            var health = target.GetComponent<CharacterHealth>();
            if (health != null && health.IsAlive) health.ApplyDamage(damage, BeamColor, identity);
        }
    }
}
