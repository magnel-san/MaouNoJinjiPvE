namespace Game.HandTracking
{
    // 戦闘中の全体コマンドに使う、静的な手の形の判定結果(HandPoseClassifier.FingerStateから
    // BattleGestureInputController.ClassifyPoseが組み立てる)。
    public enum HandPose
    {
        None,      // どの形にも当てはまらない
        IndexOnly, // 人差し指のみ伸展(親指含む他4指は屈曲) → keyboard1 (集合)
        OpenPalm,  // パー(5指すべて伸展) → keyboard2 (退避)
        Scissors,  // チョキ(人差し指+中指の2本のみ伸展、親指は屈曲) → keyboard3 (ボス集中攻撃)
        ThumbUp,   // グッドサイン(親指のみ伸展・上向き、他4指は屈曲) → keyboard4 (ボス以外集中攻撃)
        ThumbDown, // バッドサイン(親指のみ伸展・下向き、他4指は屈曲) → 回復(持続効果、UpdateHealPulse参照)
    }
}
