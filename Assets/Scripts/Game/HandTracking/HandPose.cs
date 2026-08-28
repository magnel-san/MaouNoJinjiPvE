namespace Game.HandTracking
{
    // 戦闘中の全体コマンドに使う、静的な手の形の判定結果(HandPoseClassifier.FingerStateから
    // BattleGestureInputController.ClassifyPoseが組み立てる)。
    public enum HandPose
    {
        None,      // どの形にも当てはまらない
        IndexOnly, // 人差し指のみ伸展(親指含む他4指は屈曲) → keyboard1 (集合)
        OpenPalm,  // パー(5指すべて伸展) → keyboard2 (退避/よける)
        Scissors,  // チョキ(人差し指+中指の2本のみ伸展、親指は屈曲)。判定は残しているが、
                   // 現在ApplyCommand/UpdateGuardHoldでは使用しない(防御はグーに変更済み。
                   // 将来また使う可能性があるため判定自体は残す)
        Fist,      // グー(5指すべて屈曲、親指も握り込む) → 防御(持続効果、UpdateGuardHold参照)
        ThumbUp,    // グッドサイン(親指のみ伸展・上向き、他4指は屈曲)。判定は残しているが、
                    // ApplyCommand/PoseLabelでは使用しない(ボス以外集中攻撃コマンドは廃止済み)
        ThumbDown,  // バッドサイン(親指のみ伸展・下向き、他4指は屈曲)。判定は残しているが、
                    // ApplyCommand/PoseLabelでは使用しない(ボス集中攻撃コマンドは廃止済み)
        IndexPinky, // 人差し指+小指のみ伸展(親指・中指・薬指は屈曲) → 必殺技(ゲージが溜まっていれば即発動、片手のみで可)
    }
}
