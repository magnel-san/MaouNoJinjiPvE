using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    // 戦闘中、時間経過で自動的に溜まる必殺ゲージ。100%になった状態でキーボード6を押すと、
    // 生存している全プレイヤーキャラを一定時間だけ巨大化させ、HPを回復し、攻撃力・移動速度を倍増させる。
    // BattleInput GameObject(BattleCursorInputDebug等と同じ)に乗せることで、戦闘フェーズ中だけ
    // 自動的に有効/無効になる(OnEnable/OnDisableでゲージ・ブースト状態をリセットする)。
    public class UltimateGaugeController : MonoBehaviour
    {
        [Header("ゲージ")]
        [Tooltip("ゲージが0%から100%になるまでの秒数")]
        [SerializeField] private float _fillDuration = 30f;

        [Header("発動効果")]
        [SerializeField] private float _boostDuration = 12f;
        [SerializeField] private float _statMultiplier = 2f;
        [SerializeField] private float _scaleMultiplier = 1.8f;
        [SerializeField] private float _healAmount = 300f;
        [SerializeField] private AudioClip _activateSound;

        static readonly Color UltimateColor = new Color(1f, 0.85f, 0.2f);

        public float GaugeFraction { get; private set; }
        public bool IsReady => GaugeFraction >= 1f;
        public bool IsBoostActive { get; private set; }

        float boostTimer;
        // ブースト解除時に確実に元へ戻せるよう、倍率をかけた対象を(死亡・ラウンド跨ぎも含めて)覚えておく。
        readonly List<Transform> boostedCharacters = new List<Transform>();

        // 最終決戦専用: trueの間はゲージを常に100%表示にし、通常のブースト発動(スタット強化)を行わない。
        // 両手パーでの発動はFinalBattleBeamControllerがBattleCommandState.BothHandsOpenActiveを見て
        // 直接処理するため、こちらは「普段のブーストを暴発させない」ためだけに使う。
        public bool FinalBattleMode { get; private set; }

        public void SetFinalBattleMode(bool active)
        {
            FinalBattleMode = active;
            if (active) GaugeFraction = 1f;
        }

        void OnEnable()
        {
            GaugeFraction = 0f;
            IsBoostActive = false;
            boostTimer = 0f;
            boostedCharacters.Clear();
            FinalBattleMode = false;
        }

        void OnDisable()
        {
            if (IsBoostActive) EndBoost();
        }

        void Update()
        {
            if (FinalBattleMode)
            {
                // 最終決戦中は常に100%表示にするだけで、通常のブースト(スタット強化)は発動させない
                // (発動はFinalBattleBeamControllerが両手パーの継続を見て別途処理する)。
                GaugeFraction = 1f;
                return;
            }

            if (IsBoostActive)
            {
                boostTimer -= Time.deltaTime;
                if (boostTimer <= 0f) EndBoost();
                return;
            }

            if (GaugeFraction < 1f)
            {
                GaugeFraction = Mathf.Clamp01(GaugeFraction + Time.deltaTime / Mathf.Max(_fillDuration, 0.01f));
            }

            if (IsReady)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard[Key.Digit6].wasPressedThisFrame)
                {
                    TriggerBoost();
                }
            }
        }

        // ジェスチャー側(両手パーを2秒キープ)からの発動要求用。キーボード6と同じ条件を満たす場合のみ発動する。
        // 最終決戦中はここでの通常ブーストは無効(ビームはFinalBattleBeamController側で処理する)。
        public void TryTriggerFromExternal()
        {
            if (FinalBattleMode) return;
            if (IsReady && !IsBoostActive) TriggerBoost();
        }

        void TriggerBoost()
        {
            IsBoostActive = true;
            boostTimer = _boostDuration;
            GaugeFraction = 0f;
            boostedCharacters.Clear();

            // ★ここに追加します！（78行目付近）
            if (UltimateCutinManager.Instance != null)
            {
                UltimateCutinManager.Instance.PlayUltimateCutin();
            }
            
            foreach (var identity in CharacterRegistry.All.ToList())
            {
                if (identity == null || identity.Team != Team.Player || !identity.IsAlive) continue;

                var stats = identity.GetComponent<CharacterStats>();
                if (stats != null) stats.SetStatMultiplier(_statMultiplier);

                var health = identity.GetComponent<CharacterHealth>();
                if (health != null) health.Heal(_healAmount);

                identity.transform.localScale *= _scaleMultiplier;
                boostedCharacters.Add(identity.transform);

                CombatFx.ImpactBurst(identity.transform.position + Vector3.up, UltimateColor, 0.6f);
            }

            CameraShake.Shake(0.8f);
            SfxUtil.PlayAt(_activateSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }

        void EndBoost()
        {
            IsBoostActive = false;

            foreach (var t in boostedCharacters)
            {
                if (t == null) continue;

                var stats = t.GetComponent<CharacterStats>();
                if (stats != null) stats.SetStatMultiplier(1f);

                t.localScale /= _scaleMultiplier;
            }
            boostedCharacters.Clear();
        }
    }
}
