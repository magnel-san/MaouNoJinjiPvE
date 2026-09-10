using UnityEngine;

namespace Game
{
    // MaxHP・攻撃力は「ランク(金/銀/銅)の基礎値 × HP/攻撃力のティア倍率(3段階)」で求める
    // (GameBalanceConfig.GetRankBase/StatTierMultiplier参照)。質量はキャラ個別のTierを持たず
    // ランクで固定。移動速度は全キャラ共通の固定値(GameBalanceConfig.MonsterMoveSpeed)を使う。
    // MaxHP・AttackPowerには、CharacterIdentity.IsHeroに応じてGameBalanceConfigの
    // Hero/Monster倍率(HeroMaxHPMultiplier等)も掛かる(勇者プレファブとそれ以外の全キャラ=モンスターを
    // 一括で強化/弱化するための共通ノブ。Awake参照)。
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CharacterIdentity))]
    public class CharacterStats : MonoBehaviour
    {
        [Header("ランク・ステータス段階")]
        public CharacterRank Rank = CharacterRank.Bronze;
        [Tooltip("MaxHPのティア(小=1/中=2/大・特大=3)")]
        [Range(1, 3)] public int HPTier = 1;
        [Tooltip("攻撃力のティア(小=1/中=2/大・特大=3)")]
        [Range(1, 3)] public int AttackPowerTier = 1;

        [Header("ランク方式を使わないキャラ用 (ボス等)")]
        [Tooltip("trueの場合、Rank/HPTier/AttackPowerTierを無視してFixed*の値を直接使う")]
        public bool UseFixedStats = false;
        public float FixedMaxHP = 100f;
        public float FixedAttackPower = 10f;
        public float FixedMass = 10f;

        [Header("キャラ個別の値")]
        public float InvincibilityTime = 0.5f;
        public float KnockbackVectorStrength = 45f;

        public float MaxHP { get; private set; }
        public float AttackPower { get; private set; }
        public float MoveSpeed { get; private set; }

        // 必殺技(UltimateGaugeController)による一時的な倍率ブースト用に、Tierから求めた素の値を覚えておく。
        float baseAttackPower;
        float baseMoveSpeed;

        // ボス等、ランク方式に乗らない固定値が必要なキャラ専用の上書き(例: 接触ダメージを完全無効化する攻撃力0、
        // ラウンド倍率を掛けたHP)。
        public void OverrideAttackPower(float value)
        {
            AttackPower = value;
            baseAttackPower = value;
        }

        public void OverrideMaxHP(float value) => MaxHP = value;

        // 必殺技中の一時ブースト。multiplier=1で通常値に戻る(掛け算・割り算を繰り返さないので誤差が蓄積しない)。
        public void SetStatMultiplier(float multiplier)
        {
            AttackPower = baseAttackPower * multiplier;
            MoveSpeed = baseMoveSpeed * multiplier;
        }

        void Awake()
        {
            var rb = GetComponent<Rigidbody>();
            var isHero = GetComponent<CharacterIdentity>().IsHero;
            var cfg = GameBalanceConfig.Instance;
            if (cfg != null)
            {
                var hpMultiplier = isHero ? cfg.HeroMaxHPMultiplier : cfg.MonsterMaxHPMultiplier;
                var attackMultiplier = isHero ? cfg.HeroAttackPowerMultiplier : cfg.MonsterAttackPowerMultiplier;

                if (UseFixedStats)
                {
                    MaxHP = FixedMaxHP * hpMultiplier;
                    AttackPower = FixedAttackPower * attackMultiplier;
                    rb.mass = FixedMass;
                }
                else
                {
                    var rankBase = cfg.GetRankBase(Rank);
                    MaxHP = rankBase.MaxHP * cfg.StatTierMultiplier.Get(HPTier) * hpMultiplier;
                    AttackPower = rankBase.AttackPower * cfg.StatTierMultiplier.Get(AttackPowerTier) * attackMultiplier;
                    rb.mass = rankBase.Mass;
                }

                MoveSpeed = cfg.MonsterMoveSpeed;
            }
            else
            {
                Debug.LogWarning("GameBalanceConfig が見つかりません。Assets/Resources/GameBalanceConfig.asset を作成してください。既定値で続行します。", this);
                MaxHP = UseFixedStats ? FixedMaxHP : 100f;
                AttackPower = 10f;
                MoveSpeed = 20f;
            }

            baseAttackPower = AttackPower;
            baseMoveSpeed = MoveSpeed;
        }
    }
}
