namespace Game.HandTracking
{
    // 戦闘中の全体コマンドに使う、MediaPipe GestureRecognizerの定番ジェスチャー分類結果。
    // BattleGestureInputController.MapGestureが各ジェスチャーをここへ対応付ける。
    public enum HandPose
    {
        None,             // どの形にも当てはまらない
        IndexOnly,        // Pointing_Up(人差し指のみ) → keyboard1 (集合)
        OpenPalm,         // Open_Palm(パー) → keyboard2 (退避)
        ThumbIndexMiddle, // Thumb_Up(グッドサイン) → keyboard4 (ボス以外集中攻撃)
        Scissors,         // Victory(チョキ) → keyboard3 (ボス集中攻撃)
    }
}
