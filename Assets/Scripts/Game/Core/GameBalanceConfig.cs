using UnityEngine;

namespace Game
{
    // ゲーム全体の3段階(金/銀/銅)パラメータ設定。Assets/Resources/GameBalanceConfig.asset として1つだけ配置する。
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Game/Game Balance Config")]
    public class GameBalanceConfig : ScriptableObject
    {
        // ランク(金/銀/銅)ごとの基礎ステータス。MaxHP/AttackPowerはここにStatTierMultiplierを掛けて
        // 実際の値を求める(CharacterStats.Awake参照)。Massはキャラ個別のTierを持たずランクで固定。
        [System.Serializable]
        public struct RankBaseStats
        {
            public float MaxHP;
            public float AttackPower;
            public float Mass;
        }

        [Header("全キャラ共通")]
        public RankBaseStats GoldRankBase = new RankBaseStats { MaxHP = 600, AttackPower = 80, Mass = 3 };
        public RankBaseStats SilverRankBase = new RankBaseStats { MaxHP = 550, AttackPower = 70, Mass = 2 };
        public RankBaseStats BronzeRankBase = new RankBaseStats { MaxHP = 500, AttackPower = 60, Mass = 1 };
        [Tooltip("HP/攻撃力ともに共通で使う3段階倍率(小=Tier1/中=Tier2/大・特大=Tier3)")]
        public ThreeTierFloat StatTierMultiplier = new ThreeTierFloat { Tier1 = 1.2f, Tier2 = 1.5f, Tier3 = 1.8f };
        // 全キャラ共通の移動速度(加速度, m/s^2, ForceMode.Acceleration)。段階分けは廃止し固定値にしてある。
        public float MonsterMoveSpeed = 28f;

        public RankBaseStats GetRankBase(CharacterRank rank)
        {
            switch (rank)
            {
                case CharacterRank.Gold: return GoldRankBase;
                case CharacterRank.Silver: return SilverRankBase;
                default: return BronzeRankBase;
            }
        }

        [Header("自己ダメージ軽減(SelfDamageReductionSkill) - 軽減率(%)")]
        public ThreeTierFloat DamageReductionPercent = new ThreeTierFloat { Tier1 = 15, Tier2 = 30, Tier3 = 50 };

        [Header("爆弾投下(BombDropSkill) - 攻撃クールダウン(秒)")]
        public ThreeTierFloat BomberAttackCooldown = new ThreeTierFloat { Tier1 = 6, Tier2 = 4, Tier3 = 2 };

        [Header("矢攻撃(ArrowShotSkill) - 攻撃クールダウン(秒)")]
        public ThreeTierFloat ArrowShotCooldown = new ThreeTierFloat { Tier1 = 3.5f, Tier2 = 2.5f, Tier3 = 1.5f };

        [Header("吹き飛ばし(NovaBlastSkill) - クールダウン(秒)/範囲/ダメージ")]
        public ThreeTierFloat MagicKnockbackCooldown = new ThreeTierFloat { Tier1 = 7, Tier2 = 5, Tier3 = 3 };
        public ThreeTierFloat MagicKnockbackRadius = new ThreeTierFloat { Tier1 = 2.5f, Tier2 = 3.5f, Tier3 = 4.5f };
        public ThreeTierFloat MagicKnockbackDamage = new ThreeTierFloat { Tier1 = 14, Tier2 = 32, Tier3 = 54 };

        [Header("花火攻撃(FireworkShotSkill) - 攻撃速度(クールダウン秒)")]
        public ThreeTierFloat FireworkAttackCooldown = new ThreeTierFloat { Tier1 = 4f, Tier2 = 2.6f, Tier3 = 1.5f };

        [Header("剣召喚(OrbitingSwordsSkill) - 持続時間(秒)")]
        public ThreeTierFloat SwordDuration = new ThreeTierFloat { Tier1 = 2f, Tier2 = 4f, Tier3 = 6f };

        [Header("回復(HealSkill) - 回復クールダウン(秒)/回復量")]
        public ThreeTierFloat SupportHealCooldown = new ThreeTierFloat { Tier1 = 6f, Tier2 = 4f, Tier3 = 2f };
        public ThreeTierFloat SupportHealAmount = new ThreeTierFloat { Tier1 = 15, Tier2 = 30, Tier3 = 48 };

        [Header("連鎖雷撃(ChainLightningSkill) - クールダウン(秒)/ダメージ")]
        public ThreeTierFloat ChainLightningCooldown = new ThreeTierFloat { Tier1 = 6f, Tier2 = 4f, Tier3 = 2.2f };
        public ThreeTierFloat ChainLightningDamage = new ThreeTierFloat { Tier1 = 15, Tier2 = 30, Tier3 = 50 };

        [Header("反射フィールド(ReflectFieldSkill) - クールダウン(秒)/範囲/ダメージ")]
        public ThreeTierFloat ReflectFieldCooldown = new ThreeTierFloat { Tier1 = 3f, Tier2 = 2.3f, Tier3 = 1.6f };
        public ThreeTierFloat ReflectFieldRadius = new ThreeTierFloat { Tier1 = 2.5f, Tier2 = 3.5f, Tier3 = 4.5f };
        public ThreeTierFloat ReflectFieldDamage = new ThreeTierFloat { Tier1 = 10, Tier2 = 20, Tier3 = 32 };

        [Header("ドラゴンブレス(DragonBreathSkill) - クールダウン(秒)/射程/ダメージ")]
        public ThreeTierFloat DragonBreathCooldown = new ThreeTierFloat { Tier1 = 5f, Tier2 = 4f, Tier3 = 3f };
        public ThreeTierFloat DragonBreathRange = new ThreeTierFloat { Tier1 = 4f, Tier2 = 5.5f, Tier3 = 7f };
        public ThreeTierFloat DragonBreathDamage = new ThreeTierFloat { Tier1 = 12, Tier2 = 24, Tier3 = 40 };

        [Header("レーザービーム(LaserBeamSkill) - クールダウン(秒)/射程/ダメージ")]
        public ThreeTierFloat LaserBeamCooldown = new ThreeTierFloat { Tier1 = 4f, Tier2 = 3f, Tier3 = 2f };
        public ThreeTierFloat LaserBeamRange = new ThreeTierFloat { Tier1 = 6f, Tier2 = 8f, Tier3 = 10f };
        public ThreeTierFloat LaserBeamDamage = new ThreeTierFloat { Tier1 = 10, Tier2 = 20, Tier3 = 34 };

        [Header("防御フィールド(GuardAuraSkill) - 味方へのダメージ軽減オーラ(%)")]
        public ThreeTierFloat GuardAuraReductionPercent = new ThreeTierFloat { Tier1 = 15, Tier2 = 30, Tier3 = 45 };
        [Tooltip("タンクの盾オーラが届く範囲の半径(ステージ直径の1/4)を求めるための基準値")]
        public float StageDiameter = 27f;

        [Header("勇者/モンスター 強化倍率")]
        [Tooltip("勇者(CharacterIdentity.IsHero=true)のMaxHPに掛ける倍率")]
        public float HeroMaxHPMultiplier = 2f;
        [Tooltip("勇者(CharacterIdentity.IsHero=true)の攻撃力(CharacterStats.AttackPower)に掛ける倍率")]
        public float HeroAttackPowerMultiplier = 2f;
        [Tooltip("モンスター(勇者以外の全キャラ。ボスも含む)のMaxHPに掛ける倍率")]
        public float MonsterMaxHPMultiplier = 1f;
        [Tooltip("モンスター(勇者以外の全キャラ。ボスも含む)の攻撃力(CharacterStats.AttackPower)に掛ける倍率")]
        public float MonsterAttackPowerMultiplier = 1f;

        [Header("ボス")]
        public float BossMaxHP = 2200f;
        [Tooltip("衝撃波の再発動間隔(秒)")]
        public float BossShockwaveCooldown = 7f;
        public float BossShockwaveRadius = 8f;
        public float BossShockwaveDamage = 22f;
        public float BossShockwaveKnockback = 25f;
        [Tooltip("召喚の再発動間隔(秒)")]
        public float BossSummonCooldown = 12f;
        public int BossSummonMinCount = 2;
        public int BossSummonMaxCount = 3;
        [Tooltip("同時に生存できる召喚体の上限(これを超えている間は召喚をスキップする)")]
        public int BossSummonMaxConcurrent = 6;
        [Tooltip("通常攻撃「吹き飛ばし」を単独タイマーで発動する際のウインドアップ秒数" +
          "(避けやすくするための予備動作。ジャンプ着地から呼ばれる経路は別途ジャンプ自体の予告があるため対象外)")]
        public float BossKnockbackWindupSeconds = 0.6f;
        [Tooltip("ボスのHPがこの割合(%)削れるたびにコインを1枚落とす")]
        public float BossCoinDropHpStepPercent = 5f;

        [Header("ボス専用技1: 予告攻撃を画面右/左/上/下全体にする攻撃パターン (ラウンド1以降で解禁)")]
        public float BossDirectionalWipeCooldown = 9f;
        public float BossDirectionalWipeWarningSeconds = 2f;
        public float BossDirectionalWipeDamage = 27f;

        [Header("ボス専用技2: 画面全体を覆う攻撃、グー防御で0ダメージ (ラウンド2以降で解禁)")]
        public float BossFullCoverCooldown = 11f;
        public float BossFullCoverWarningSeconds = 1.8f;
        public float BossFullCoverDamage = 33f;

        [Header("防御(グー)の効果音・見た目")]
        [Tooltip("グー(防御)を構え始めた瞬間に鳴らす、盾を装備する効果音。ここに設定するとゲーム全体で使われる")]
        public AudioClip GuardEquipSound;
        [Tooltip("ボス2の全体攻撃をグーで防御し切った(0ダメージにできた)瞬間に鳴らす、はじく効果音")]
        public AudioClip GuardBlockSound;
        [Tooltip("防御中にキャラの周りを3つ公転する盾のプレファブ。未設定なら簡易的な球体を代わりに使う")]
        public GameObject GuardShieldPrefab;

        [Header("ボス専用技3: ボス中心から円状の予告攻撃 (ラウンド3以降で解禁)")]
        public float BossCenterRingCooldown = 13f;
        public float BossCenterRingWarningSeconds = 1.8f;
        public float BossCenterRingRadius = 13f;
        public float BossCenterRingDamage = 36f;
        [Tooltip("ボス戦は3回行う(1回目クリア後に採用キャラを2体追加、2回目クリア後にさらに2体追加)。" +
          "各回のボスの最大HP・攻撃ダメージに掛ける倍率。要素数3(1回目/2回目/3回目)")]
        public float[] BossRoundMultiplier = { 1f, 1.7f, 2.6f };

        static GameBalanceConfig cachedInstance;

        public static GameBalanceConfig Instance
        {
            get
            {
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<GameBalanceConfig>("GameBalanceConfig");
                }
                return cachedInstance;
            }
        }
    }
}
