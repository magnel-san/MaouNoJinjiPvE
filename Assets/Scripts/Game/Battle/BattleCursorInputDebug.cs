using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    // 戦闘フェーズ中のみ有効化される、キーボード+マウスカーソルによる全体コマンドの「デバッグ用」入力経路。
    // 実機のジェスチャーと1対1で対応させてあり、カメラ無しでも各ジェスチャーの動作確認ができる。
    // 1キー保持=人差し指(マウス位置に集合)、2キー保持=片手パー(よける)、3キー保持=グー(防御)、
    // 6キー保持=両手パー(2秒キープで必殺技/最終決戦中はビーム)。
    // BattleCommandState.SubmitKeyboardMouse経由で書き込むため、ジェスチャー側が直近に書き込んでいれば
    // 自動的に無視される(優先順位の判定はBattleCommandState側に一元化されている、詳細はそちらのコメント参照)。
    // グー(防御)/両手パーはCommandType/FocusFilterとは別枠の持続フラグのため、優先順位判定を介さず
    // 常に直接書き込む(ジェスチャー入力と同時に使うことは想定していないデバッグ専用の割り切り)。
    // GameFlowManagerが戦闘開始直前にこのGameObjectを有効化し、勝敗確定で無効化する想定。
    public class BattleCursorInputDebug : MonoBehaviour
    {
        [Tooltip("マウスカーソルを戦場の地面(この高さの水平面)へ投影する")]
        [SerializeField] private float _groundHeight = 0f;
        [SerializeField] private Camera _worldCamera;
        [Tooltip("6キーを何秒押し続けたら必殺技(keyboard6相当)を発動するか。ジェスチャー側の両手パー2秒キープと同じ")]
        [SerializeField] private float _bothHandsHoldSeconds = 2f;

        UltimateGaugeController ultimateGauge;
        float bothHandsHoldTimer;
        bool guarding;

        void Awake() => ultimateGauge = GetComponent<UltimateGaugeController>();

        void OnEnable()
        {
            RallyCircleIndicator.EnsureExists();
        }

        void OnDisable()
        {
            BattleCommandState.Clear();
            bothHandsHoldTimer = 0f;
            guarding = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                BattleCommandState.SubmitKeyboardMouse(PlayerCommandType.None, default, FocusFireFilter.None);
                UpdateGuard(false);
                UpdateBothHandsHold(false);
                return;
            }

            var commandType = PlayerCommandType.None;
            var rallyPos = default(Vector3);

            // 1キー = 人差し指のみ(集合)
            if (keyboard[Key.Digit1].isPressed && TryGetGroundPoint(out rallyPos))
            {
                commandType = PlayerCommandType.Rally;
            }
            // 2キー = 片手パー(よける)
            else if (keyboard[Key.Digit2].isPressed)
            {
                commandType = PlayerCommandType.Flee;
            }

            BattleCommandState.SubmitKeyboardMouse(commandType, rallyPos, FocusFireFilter.None);

            // 3キー = グー(防御)
            UpdateGuard(keyboard[Key.Digit3].isPressed);

            // 6キー = 両手パー(必殺技/最終決戦ビーム)
            UpdateBothHandsHold(keyboard[Key.Digit6].isPressed);
        }

        void UpdateGuard(bool active)
        {
            if (active && !guarding)
            {
                CommandAnnouncer.Announce("防御");
                var cfg = GameBalanceConfig.Instance;
                if (cfg != null)
                {
                    var cam = Camera.main;
                    SfxUtil.PlayAt(cfg.GuardEquipSound, cam != null ? cam.transform.position : transform.position);
                }
            }
            guarding = active;
            BattleCommandState.SetGuardActive(active);
        }

        void UpdateBothHandsHold(bool bothOpen)
        {
            BattleCommandState.SetBothHandsOpen(bothOpen);

            if (bothOpen) bothHandsHoldTimer += Time.deltaTime;
            else bothHandsHoldTimer = Mathf.Max(0f, bothHandsHoldTimer - Time.deltaTime * 2f);

            if (bothHandsHoldTimer >= _bothHandsHoldSeconds)
            {
                bothHandsHoldTimer = 0f;
                ultimateGauge?.TryTriggerFromExternal();
            }
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
