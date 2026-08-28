using UnityEngine;

namespace Game
{
    // ゲーム全体の5段階パラメータ設定。Assets/Resources/GameBalanceConfig.asset として1つだけ配置する。
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Game/Game Balance Config")]
    public class GameBalanceConfig : ScriptableObject
    {
        [Header("全キャラ共通")]
        // ボス(HP2200〜、3回戦で最大5720)を2体編成でも削り切れるよう、接触ダメージの基礎値を引き上げてある。
        public FiveTierFloat AttackPower = new FiveTierFloat { Tier1 = 13, Tier2 = 25, Tier3 = 38, Tier4 = 50, Tier5 = 63 };
        public FiveTierFloat Mass = new FiveTierFloat { Tier1 = 5, Tier2 = 10, Tier3 = 15, Tier4 = 20, Tier5 = 25 };
        // 移動力は加速度(m/s^2, ForceMode.Acceleration)。Rigidbodyの空気抵抗(CharacterBody.LinearDamping)と
        // 釣り合った終端速度が実質的な移動速度になる (既定の減衰0.6なら終端速度はおよそTier値÷0.6)。
        public FiveTierFloat MoveSpeed = new FiveTierFloat { Tier1 = 14f, Tier2 = 20f, Tier3 = 26f, Tier4 = 32f, Tier5 = 40f };

        [Header("A: 直進型 - 軽減率(%)")]
        public FiveTierFloat DamageReductionPercent = new FiveTierFloat { Tier1 = 10, Tier2 = 20, Tier3 = 30, Tier4 = 40, Tier5 = 50 };

        [Header("B: 浮遊 - 攻撃クールダウン(秒)")]
        public FiveTierFloat BomberAttackCooldown = new FiveTierFloat { Tier1 = 6, Tier2 = 5, Tier3 = 4, Tier4 = 3, Tier5 = 2 };

        [Header("C: 隠密 - 攻撃クールダウン(秒)")]
        public FiveTierFloat StealthAttackCooldown = new FiveTierFloat { Tier1 = 3.5f, Tier2 = 3f, Tier3 = 2.5f, Tier4 = 2f, Tier5 = 1.5f };

        [Header("E: 魔法 - 吹き飛ばしクールダウン(秒)/範囲/ダメージ")]
        public FiveTierFloat MagicKnockbackCooldown = new FiveTierFloat { Tier1 = 7, Tier2 = 6, Tier3 = 5, Tier4 = 4, Tier5 = 3 };
        public FiveTierFloat MagicKnockbackRadius = new FiveTierFloat { Tier1 = 2f, Tier2 = 2.5f, Tier3 = 3f, Tier4 = 3.5f, Tier5 = 4f };
        public FiveTierFloat MagicKnockbackDamage = new FiveTierFloat { Tier1 = 9, Tier2 = 15, Tier3 = 21, Tier4 = 27, Tier5 = 36 };

        [Header("F: 花火 - 攻撃速度(クールダウン秒)")]
        public FiveTierFloat FireworkAttackCooldown = new FiveTierFloat { Tier1 = 4f, Tier2 = 3.3f, Tier3 = 2.6f, Tier4 = 2f, Tier5 = 1.5f };

        [Header("G: 剣召喚 - 持続時間(秒)")]
        public FiveTierFloat SwordDuration = new FiveTierFloat { Tier1 = 2f, Tier2 = 3f, Tier3 = 4f, Tier4 = 5f, Tier5 = 6f };

        [Header("H: 支援 - 回復クールダウン(秒)/回復量")]
        public FiveTierFloat SupportHealCooldown = new FiveTierFloat { Tier1 = 6f, Tier2 = 5f, Tier3 = 4f, Tier4 = 3f, Tier5 = 2f };
        public FiveTierFloat SupportHealAmount = new FiveTierFloat { Tier1 = 10, Tier2 = 15, Tier3 = 20, Tier4 = 25, Tier5 = 32 };

        [Header("I: 連鎖雷撃 - クールダウン(秒)/ダメージ")]
        public FiveTierFloat ChainLightningCooldown = new FiveTierFloat { Tier1 = 6f, Tier2 = 5f, Tier3 = 4f, Tier4 = 3f, Tier5 = 2.2f };
        public FiveTierFloat ChainLightningDamage = new FiveTierFloat { Tier1 = 10, Tier2 = 15, Tier3 = 20, Tier4 = 25, Tier5 = 33 };

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
        public float BossShockwaveRadius = 6f;
        public float BossShockwaveDamage = 15f;
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
        public float BossDirectionalWipeDamage = 18f;

        [Header("ボス専用技2: 画面全体を覆う攻撃、グー防御で0ダメージ (ラウンド2以降で解禁)")]
        public float BossFullCoverCooldown = 11f;
        public float BossFullCoverWarningSeconds = 1.8f;
        public float BossFullCoverDamage = 22f;

        [Header("防御(グー)の効果音")]
        [Tooltip("グー(防御)を構え始めた瞬間に鳴らす、盾を装備する効果音。ここに設定するとゲーム全体で使われる")]
        public AudioClip GuardEquipSound;
        [Tooltip("ボス2の全体攻撃をグーで防御し切った(0ダメージにできた)瞬間に鳴らす、はじく効果音")]
        public AudioClip GuardBlockSound;

        [Header("ボス専用技3: ボス中心から円状の予告攻撃 (ラウンド3以降で解禁)")]
        public float BossCenterRingCooldown = 13f;
        public float BossCenterRingWarningSeconds = 1.8f;
        public float BossCenterRingRadius = 10f;
        public float BossCenterRingDamage = 24f;
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
