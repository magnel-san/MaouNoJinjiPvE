namespace Game
{
    // 採用候補キャラのレア度。CharacterStatsのステータス計算式(ランク基礎値)と、
    // 3択の抽選確率(GameFlowManager.RankDrawWeight)の両方に使う。
    public enum CharacterRank
    {
        Bronze,
        Silver,
        Gold,
    }

    // 採用候補キャラの大分類。ロールの比率(アタッカー:タンク:ヒーラー = 5:3:2)を意識してプール全体を構成する。
    public enum CharacterRole
    {
        Attacker,
        Tank,
        Healer,
    }
}
