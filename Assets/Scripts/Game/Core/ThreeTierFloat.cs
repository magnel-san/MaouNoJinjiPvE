using System;

namespace Game
{
    // ゲーム全体設定で調整する3段階の数値 (Tier1=銅/Tier2=銀/Tier3=金)。
    [Serializable]
    public struct ThreeTierFloat
    {
        public float Tier1;
        public float Tier2;
        public float Tier3;

        public float Get(int tier)
        {
            int clamped = tier < 1 ? 1 : (tier > 3 ? 3 : tier);
            switch (clamped)
            {
                case 1: return Tier1;
                case 2: return Tier2;
                default: return Tier3;
            }
        }
    }
}
