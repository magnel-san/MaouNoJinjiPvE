using UnityEngine;

namespace Game
{
    public enum PlayerCommandType { None, Rally, Flee }

    public enum FocusFireFilter { None, BossOnly, ExcludeBoss }

    // 戦闘フェーズ中のプレイヤーコマンド(カーソル+キーボードのデバッグ経路、または手のジェスチャー経路)の
    // 現在状態を保持する共有ステート。PlayerCommandIntentSource(移動)とTargetingUtility(狙い撃ち)は
    // ここを読むだけでよい(CharacterRegistryと同じ「静的な共有窓口」の形に合わせている)。
    //
    // 書き込みは必ずSubmitGesture/SubmitKeyboardMouse経由で行う(publicフィールドへの直接代入はしない)。
    // 2つの入力コントローラーは別々のUpdate/イベントから非同期に(実行順序の保証なしに)このステートへ
    // 書き込むため、「ジェスチャー優先・キーボードはフォールバック」という優先順位を呼び出し側の
    // 実行順序に頼らず正しく保証する必要がある。そのため優先順位の判定自体をここに一元化し、
    // ジェスチャー側の書き込み直後は一定時間(GestureGraceSeconds)キーボード側の書き込みを無視する。
    public static class BattleCommandState
    {
        [Tooltip("ジェスチャー入力が来てから、この秒数はキーボード/マウス側の書き込みを無視する(ジェスチャー優先)")]
        public static float GestureGraceSeconds = 0.25f;

        public static PlayerCommandType CommandType { get; private set; } = PlayerCommandType.None;
        public static Vector3 RallyWorldPosition { get; private set; }
        public static float RallyRadius = 3.5f;
        public static FocusFireFilter FocusFilter { get; private set; } = FocusFireFilter.None;

        static float _lastGestureWriteTime = -999f;

        // ジェスチャー側は常にステートを更新できる(最優先)。
        public static void SubmitGesture(PlayerCommandType commandType, Vector3 rallyWorldPosition, FocusFireFilter focusFilter)
        {
            CommandType = commandType;
            RallyWorldPosition = rallyWorldPosition;
            FocusFilter = focusFilter;
            _lastGestureWriteTime = Time.unscaledTime;
        }

        // キーボード/マウス側は、直近にジェスチャーの書き込みが無かった場合のみ反映される(デバッグ用フォールバック)。
        public static void SubmitKeyboardMouse(PlayerCommandType commandType, Vector3 rallyWorldPosition, FocusFireFilter focusFilter)
        {
            if (Time.unscaledTime - _lastGestureWriteTime < GestureGraceSeconds) return;

            CommandType = commandType;
            RallyWorldPosition = rallyWorldPosition;
            FocusFilter = focusFilter;
        }

        // 戦闘フェーズの開始/終了時に呼び、前回の戦闘の入力状態を持ち越さないようにする。
        public static void Clear()
        {
            CommandType = PlayerCommandType.None;
            FocusFilter = FocusFireFilter.None;
            _lastGestureWriteTime = -999f;
        }
    }
}
