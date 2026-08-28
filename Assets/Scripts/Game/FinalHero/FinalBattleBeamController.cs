using UnityEngine;

namespace Game
{
    // 最終決戦専用: 両手パー(ボタン6相当)を維持している間、カメラから最終決戦の敵へ向けて
    // ビームを撃ち続ける。UltimateGaugeController.FinalBattleModeがtrueの間だけ
    // GameFlowManagerが有効化し、ゲージ消費や回数制限を設けず、パーを維持している限り何度でも撃てる。
    public class FinalBattleBeamController : MonoBehaviour
    {
        [Tooltip("ビームの秒間ダメージ")]
        [SerializeField] private float _damagePerSecond = 80f;
        [SerializeField] private float _beamWidth = 0.25f;
        [Tooltip("魔王(プレイヤー)自身の与ダメージとしてリザルト画面の内訳に表示する際の表示名")]
        [SerializeField] private string _attackerDisplayName = "魔王";

        static readonly Color BeamColor = new Color(0.4f, 0.9f, 1f);

        CharacterIdentity target;
        CharacterHealth targetHealth;
        LineRenderer line;
        float hitFxTimer;

        public void SetTarget(CharacterIdentity heroIdentity)
        {
            target = heroIdentity;
            targetHealth = heroIdentity != null ? heroIdentity.GetComponent<CharacterHealth>() : null;
        }

        void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = _beamWidth;
            line.material = new Material(VfxShaderUtil.GetUnlitShader()) { color = BeamColor };
            line.startColor = BeamColor;
            line.endColor = BeamColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
        }

        void OnDisable()
        {
            if (line != null) line.enabled = false;
        }

        void Update()
        {
            var firing = BattleCommandState.BothHandsOpenActive && target != null && targetHealth != null
                && targetHealth.IsAlive && Camera.main != null;

            line.enabled = firing;
            if (!firing) return;

            var camPos = Camera.main.transform.position;
            var targetPos = target.transform.position + Vector3.up * 1f;
            line.SetPosition(0, camPos);
            line.SetPosition(1, targetPos);

            var damage = _damagePerSecond * Time.deltaTime;
            targetHealth.ApplyDamage(damage, BeamColor);
            DamageStatsTracker.RegisterDamageByName(_attackerDisplayName, damage);

            hitFxTimer -= Time.deltaTime;
            if (hitFxTimer <= 0f)
            {
                hitFxTimer = 0.15f;
                CombatFx.ImpactBurst(targetPos, BeamColor, 0.3f);
                CameraShake.Shake(0.15f);
            }
        }
    }
}
