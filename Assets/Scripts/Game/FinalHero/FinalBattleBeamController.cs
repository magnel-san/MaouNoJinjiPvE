using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

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
            // ターゲットが設定されていない場合、自動的に敵（Boss/Enemy）を検出してセットする
            if (target == null || targetHealth == null || !targetHealth.IsAlive)
            {
                TryAutoAcquireTarget();
            }

            // 両手パー(BothHandsOpenActive) または キーボードの「6キー押しっぱなし」でビーム発射判定
            var keyboard = Keyboard.current;
            bool isKey6Pressed = keyboard != null && keyboard[Key.Digit6].isPressed;
            bool isInputActive = BattleCommandState.BothHandsOpenActive || isKey6Pressed;

            var mainCam = Camera.main;

            // デバッグログ（入力されているが発射条件が揃っていない場合のサポート）
            if (isInputActive)
            {
                if (target == null || targetHealth == null || !targetHealth.IsAlive)
                {
                    Debug.LogWarning("[FinalBattleBeam] 入力は検知されましたが、攻撃対象(敵)が見つからないか死亡しています。");
                }
                else if (mainCam == null)
                {
                    Debug.LogWarning("[FinalBattleBeam] 入力は検知されましたが、MainCameraが見つかりません。");
                }
            }

            var firing = isInputActive && target != null && targetHealth != null
                && targetHealth.IsAlive && mainCam != null;

            line.enabled = firing;
            if (!firing) return;

            var camPos = mainCam.transform.position;
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

        /// <summary>
        /// 外部からSetTargetが呼ばれていない場合、自動的に生存している敵を取得する
        /// </summary>
        private void TryAutoAcquireTarget()
        {
            if (CharacterRegistry.All == null) return;

            var enemy = CharacterRegistry.All.FirstOrDefault(c => 
                c != null && c.Team == Team.Enemy && c.IsAlive);

            if (enemy != null)
            {
                SetTarget(enemy);
            }
        }
    }
}


