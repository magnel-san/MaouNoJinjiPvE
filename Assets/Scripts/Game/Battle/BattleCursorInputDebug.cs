using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    // 戦闘フェーズ中のみ有効化される、キーボード+マウスカーソルによる全体コマンドの「デバッグ用」入力経路。
    // 1キー保持=マウス位置(地面へ投影)に全員集合、2キー保持=敵から全員退避。
    // 3/4キーは狙い撃ち(ボス集中/ボス以外集中)のデバッグ用で、1/2キーとは独立して同時に効かせられる
    // (手のジェスチャー版は4つの手の形が排他だが、こちらはキーボードでの検証をしやすくするための割り切り)。
    // BattleCommandState.SubmitKeyboardMouse経由で書き込むため、ジェスチャー側が直近に書き込んでいれば
    // 自動的に無視される(優先順位の判定はBattleCommandState側に一元化されている、詳細はそちらのコメント参照)。
    // GameFlowManagerが戦闘開始直前にこのGameObjectを有効化し、勝敗確定で無効化する想定。
    public class BattleCursorInputDebug : MonoBehaviour
    {
        [Tooltip("マウスカーソルを戦場の地面(この高さの水平面)へ投影する")]
        [SerializeField] private float _groundHeight = 0f;
        [SerializeField] private Camera _worldCamera;

        void OnEnable()
        {
            RallyCircleIndicator.EnsureExists();
        }

        void OnDisable()
        {
            BattleCommandState.Clear();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                BattleCommandState.SubmitKeyboardMouse(PlayerCommandType.None, default, FocusFireFilter.None);
                return;
            }

            var commandType = PlayerCommandType.None;
            var rallyPos = default(Vector3);

            if (keyboard[Key.Digit1].isPressed && TryGetGroundPoint(out rallyPos))
            {
                commandType = PlayerCommandType.Rally;
            }
            else if (keyboard[Key.Digit2].isPressed)
            {
                commandType = PlayerCommandType.Flee;
            }

            var focusFilter = FocusFireFilter.None;
            if (keyboard[Key.Digit3].isPressed) focusFilter = FocusFireFilter.BossOnly;
            else if (keyboard[Key.Digit4].isPressed) focusFilter = FocusFireFilter.ExcludeBoss;

            BattleCommandState.SubmitKeyboardMouse(commandType, rallyPos, focusFilter);
        }

        bool TryGetGroundPoint(out Vector3 worldPos)
        {
            worldPos = default;
            var mouse = Mouse.current;
            var cam = _worldCamera != null ? _worldCamera : Camera.main;
            if (mouse == null || cam == null) return false;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
            if (!plane.Raycast(ray, out var enter)) return false;

            worldPos = ray.GetPoint(enter);
            return true;
        }
    }
}
