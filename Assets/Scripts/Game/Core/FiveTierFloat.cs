using System;

namespace Game
{
    // ゲーム全体設定で調整する5段階の数値 (Tier1〜Tier5)。
    [Serializable]
    public struct FiveTierFloat
    {
        public float Tier1;
        public float Tier2;
        public float Tier3;
        public float Tier4;
        public float Tier5;

        public float Get(int tier)
        {
            int clamped = tier < 1 ? 1 : (tier > 5 ? 5 : tier);
            switch (clamped)
            {
                case 1: return Tier1;
                case 2: return Tier2;
                case 3: return Tier3;
                case 4: return Tier4;
                default: return Tier5;
            }
        }
    }
}
