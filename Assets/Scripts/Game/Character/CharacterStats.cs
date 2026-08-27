using UnityEngine;

namespace Game
{
    // 攻撃力・質量・移動速度は5段階のTierでGameBalanceConfigから解決する。
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterStats : MonoBehaviour
    {
        [Header("5段階設定 (GameBalanceConfig参照)")]
        [Range(1, 5)] public int AttackPowerTier = 1;
        [Range(1, 5)] public int MassTier = 1;
        [Range(1, 5)] public int MoveSpeedTier = 1;

        [Header("キャラ個別の値")]
        public float MaxHP = 100f;
        public float InvincibilityTime = 0.5f;
        public float KnockbackVectorStrength = 45f;

        public float AttackPower { get; private set; }
        public float MoveSpeed { get; private set; }

        // 必殺技(UltimateGaugeController)による一時的な倍率ブースト用に、Tierから求めた素の値を覚えておく。
        float baseAttackPower;
        float baseMoveSpeed;

        // ボス等、5段階Tierに乗らない固定値が必要なキャラ専用の上書き(例: 接触ダメージを完全無効化する攻撃力0)。
        public void OverrideAttackPower(float value)
        {
            AttackPower = value;
            baseAttackPower = value;
        }

        // 必殺技中の一時ブースト。multiplier=1で通常値に戻る(掛け算・割り算を繰り返さないので誤差が蓄積しない)。
        public void SetStatMultiplier(float multiplier)
        {
            AttackPower = baseAttackPower * multiplier;
            MoveSpeed = baseMoveSpeed * multiplier;
        }

        void Awake()
        {
            var rb = GetComponent<Rigidbody>();
            var cfg = GameBalanceConfig.Instance;
            if (cfg != null)
            {
                AttackPower = cfg.AttackPower.Get(AttackPowerTier);
                MoveSpeed = cfg.MoveSpeed.Get(MoveSpeedTier);
                rb.mass = cfg.Mass.Get(MassTier);
            }
            else
            {
                Debug.LogWarning("GameBalanceConfig が見つかりません。Assets/Resources/GameBalanceConfig.asset を作成してください。既定値で続行します。", this);
                AttackPower = 10f;
                MoveSpeed = 20f;
            }

            baseAttackPower = AttackPower;
            baseMoveSpeed = MoveSpeed;
        }
    }
}
