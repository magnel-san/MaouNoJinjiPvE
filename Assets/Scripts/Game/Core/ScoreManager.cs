using System;

namespace Game
{
    // コイン取得・コンボによって加算されるスコアの静的な共有ステート。CharacterRegistry/
    // BattleCommandStateと同じ「静的な共有窓口」の形。内訳(コイン/コンボ)を最終リザルト画面で
    // 別々にカウントアップ表示するため、コイン分とコンボ分を別々に集計する。
    public static class ScoreManager
    {
        public static int CoinScore { get; private set; }
        public static int ComboScore { get; private set; }
        public static int TotalScore => CoinScore + ComboScore;

        // (newTotal, delta) 表示用の枠(ScoreBorderUI)等、内訳を区別しない購読者向け。
        public static event Action<int, int> OnScoreChanged;
        // (newCoinScore, delta)
        public static event Action<int, int> OnCoinScoreChanged;
        // (newComboScore, delta)
        public static event Action<int, int> OnComboScoreChanged;

        public static void AddCoinScore(int amount)
        {
            if (amount <= 0) return;
            CoinScore += amount;
            OnCoinScoreChanged?.Invoke(CoinScore, amount);
            OnScoreChanged?.Invoke(TotalScore, amount);
        }

        public static void AddComboScore(int amount)
        {
            if (amount <= 0) return;
            ComboScore += amount;
            OnComboScoreChanged?.Invoke(ComboScore, amount);
            OnScoreChanged?.Invoke(TotalScore, amount);
        }

        public static void Reset()
        {
            CoinScore = 0;
            ComboScore = 0;
            OnCoinScoreChanged?.Invoke(0, 0);
            OnComboScoreChanged?.Invoke(0, 0);
            OnScoreChanged?.Invoke(0, 0);
        }
    }
}
