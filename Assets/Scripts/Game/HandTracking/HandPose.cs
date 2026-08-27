namespace Game.HandTracking
{
    // 戦闘中の全体コマンドに使う、静的な手の形の判定結果。
    // 親指の伸展/屈曲も含めて5指の状態が互いに排他になるよう定義しているため、
    // 同時に2つ以上のPoseに該当することはない(HandPoseClassifier参照)。
    public enum HandPose
    {
        None,             // どの形にも当てはまらない
        IndexOnly,        // 人差し指のみ伸展(親指含む他4指は屈曲) → keyboard1 (集合)
        OpenPalm,         // パー(5指すべて伸展) → keyboard2 (退避)
        ThumbIndex,       // 親指+人差し指の2本のみ伸展 → keyboard3 (ボス集中攻撃)
        ThumbIndexMiddle, // 親指+人差し指+中指の3本のみ伸展 → keyboard4 (ボス以外集中攻撃)
    }
}
