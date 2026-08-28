using System.Collections.Generic;

namespace Game
{
    // キャラごとの与ダメージ(スキル込み)を集計する静的ステート。CharacterRegistry/ScoreManagerと
    // 同じ「静的な共有窓口」の形。CharacterIdentityはラウンドごとにDestroy/再生成されるため、
    // インスタンス参照ではなく表示名(gameObject.name、"(Clone)"サフィックス除去)をキーにする。
    // Team.Playerのキャラが与えたダメージのみを対象にする(ボス・召喚敵からの被ダメージは含めない)。
    public static class DamageStatsTracker
    {
        static readonly Dictionary<string, float> damageByName = new Dictionary<string, float>();

        public static IReadOnlyDictionary<string, float> Snapshot => damageByName;

        public static void RegisterDamage(CharacterIdentity attacker, float amount)
        {
            if (attacker == null || amount <= 0f || attacker.Team != Team.Player) return;
            RegisterDamageByName(DisplayNameOf(attacker), amount);
        }

        // キャラクター(CharacterIdentity)を持たない攻撃源(最終決戦の魔王本人のビーム等)向け。
        public static void RegisterDamageByName(string name, float amount)
        {
            if (string.IsNullOrEmpty(name) || amount <= 0f) return;
            damageByName.TryGetValue(name, out var current);
            damageByName[name] = current + amount;
        }

        // 現在記録されている全キャラの与ダメージ合計(リザルト画面の「累計ダメージ」表示用)。
        public static float TotalDamage
        {
            get
            {
                float total = 0f;
                foreach (var v in damageByName.Values) total += v;
                return total;
            }
        }

        public static void Reset() => damageByName.Clear();

        static string DisplayNameOf(CharacterIdentity identity)
        {
            const string cloneSuffix = "(Clone)";
            var name = identity.gameObject.name;
            return name.EndsWith(cloneSuffix) ? name.Substring(0, name.Length - cloneSuffix.Length).TrimEnd() : name;
        }
    }
}
