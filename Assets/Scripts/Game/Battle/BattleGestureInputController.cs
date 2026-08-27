using System.Collections.Generic;
using Game.HandTracking;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using UnityEngine;
using NormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;

namespace Game
{
    // 戦闘フェーズ中のみ有効化される、手のジェスチャーによる全体コマンド入力(理想形/本命)。
    // 既存のHandTrackingController.OnGestureRecognizerResultをそのまま購読するだけで、
    // MediaPipe側の追加設定は不要。
    //
    // 利き手(HandPreference、ゲーム開始直後にHandPreferenceSelectUIで選択済み)のパーム中心を
    // カーソル位置とし、その手の静的な形(人差し指のみ/パー/チョキ/グッドサイン)で
    // keyboard1〜4相当のコマンドを決定する。それとは別に、左右両方の手が同時に検出できた場合のみ
    // 「両手パーを2秒キープ」を判定し、keyboard6相当(必殺技)をUltimateGaugeControllerへ直接要求する
    // (こちらは利き手に関係なく両手を見る)。
    //
    // ジェスチャー判定はMediaPipe GestureRecognizerの学習済み定番分類(Open_Palm/Victory/
    // Pointing_Up/Thumb_Up等)をそのまま使う(MapGesture参照)。以前あった「親指+人差し指のみ」
    // (距離比ベースの自前判定でしか出せないポーズ)はチョキに統合して廃止した。
    //
    // ILoveYouジェスチャ(親指+人差し指+小指)は上記の離散コマンドとは別枠の「持続効果」として扱い、
    // 出している間ずっと味方全体を微量回復し続ける(UpdateHealPulse参照)。
    //
    // コマンドが新しく確定した瞬間、および回復が開始した瞬間にCommandAnnouncerで
    // 「命令：〜が発動！」を画面へ表示する(演出をわかりやすくするためのフィードバック)。
    //
    // 手が検出されていない間(HasHandDataThisFrame=false)は何もしない。BattleCursorInputDebugが
    // それを見てキーボード/マウス側にフォールバックする(このスクリプト単体では何も保証しない、疎結合)。
    public class BattleGestureInputController : MonoBehaviour
    {
        [SerializeField] private HandTrackingController _handTrackingController;
        [Tooltip("戦場の地面とみなす高さ(この水平面へパーム中心を投影してカーソル位置にする)")]
        [SerializeField] private float _groundHeight = 0f;
        [Tooltip("パーム中心を戦場へ投影するのに使うカメラ。未設定ならCamera.mainを使う")]
        [SerializeField] private Camera _trackingCamera;
        [Tooltip("同じ手の形をこの秒数保持したらコマンドとして確定する(チラつき対策のdwell)")]
        [SerializeField] private float _poseConfirmSeconds = 0.12f;

        [Header("両手パー保持による必殺技発動")]
        [Tooltip("両手ともパーの状態を何秒キープしたら必殺技(keyboard6相当)を発動するか")]
        [SerializeField] private float _bothHandsOpenHoldSeconds = 2f;

        [Header("回復（ILoveYouジェスチャ）")]
        [Tooltip("ILoveYouジェスチャ(親指+人差し指+小指を伸ばす)をどちらかの手で出している間、味方全体に与える回復量(HP/秒)")]
        [SerializeField] private float _healPerSecond = 20f;

        UltimateGaugeController ultimateGauge;

        HandPose _pendingPose = HandPose.None;
        HandPose _confirmedPose = HandPose.None;
        float _pendingTimer;
        float _bothHandsHoldTimer;
        bool _healing;

        /// <summary>直近のGestureRecognizer結果で、利き手側のデータが実際に検出できたか。
        /// BattleCursorInputDebugがこれを見て、キーボード入力を上書きするかどうかを判断する。</summary>
        public bool HasHandDataThisFrame { get; private set; }

        void Awake()
        {
            ultimateGauge = GetComponent<UltimateGaugeController>();
        }

        void OnEnable()
        {
            RallyCircleIndicator.EnsureExists();
            if (_handTrackingController != null) _handTrackingController.OnGestureRecognizerResult += HandleResult;
        }

        void OnDisable()
        {
            if (_handTrackingController != null) _handTrackingController.OnGestureRecognizerResult -= HandleResult;
            HasHandDataThisFrame = false;
            _bothHandsHoldTimer = 0f;
            UpdateHealPulse(false);
        }

        void HandleResult(GestureRecognizerResult result)
        {
            HasHandDataThisFrame = false;
            if (result.handWorldLandmarks == null || _handTrackingController == null)
            {
                UpdateBothHandsHold(false);
                UpdateHealPulse(false);
                UpdateConfirmedPose(HandPose.None);
                return;
            }

            var preferRight = HandPreference.PreferRightHand;
            HandPose? leftPose = null;
            HandPose? rightPose = null;
            string leftGesture = null;
            string rightGesture = null;
            var selectedIndex = -1;

            for (var i = 0; i < result.handWorldLandmarks.Count; i++)
            {
                var worldLm = result.handWorldLandmarks[i].landmarks;
                if (worldLm == null || worldLm.Count < 21) continue;

                var isRight = IsRightHandAt(result, i);
                var gestureName = GetTopGestureCategory(result, i);
                var pose = MapGesture(gestureName);
                if (isRight) { rightPose = pose; rightGesture = gestureName; }
                else { leftPose = pose; leftGesture = gestureName; }

                // 利き手側を優先して選ぶ。データが無ければもう片方にフォールバックする。
                if (isRight == preferRight) selectedIndex = i;
                else if (selectedIndex < 0) selectedIndex = i;
            }

            // 利き手に関係なく、両手が同時にパーであるかどうかを見る(必殺技の発動条件)。
            UpdateBothHandsHold(leftPose == HandPose.OpenPalm && rightPose == HandPose.OpenPalm);

            // ILoveYouも利き手に関係なく、どちらかの手で出ていれば回復し続ける(離散コマンドとは独立)。
            UpdateHealPulse(leftGesture == "ILoveYou" || rightGesture == "ILoveYou");

            if (selectedIndex < 0)
            {
                UpdateConfirmedPose(HandPose.None);
                return;
            }

            var selectedPose = (IsRightHandAt(result, selectedIndex) ? rightPose : leftPose) ?? HandPose.None;
            UpdateConfirmedPose(selectedPose);

            if (result.handLandmarks != null && selectedIndex < result.handLandmarks.Count)
            {
                var normLm = result.handLandmarks[selectedIndex].landmarks;
                if (normLm != null && normLm.Count >= 21 && TryProjectPalmToGround(normLm, out var groundPos))
                {
                    ApplyCommand(_confirmedPose, groundPos);
                    HasHandDataThisFrame = true;
                }
            }
        }

        void UpdateBothHandsHold(bool bothOpen)
        {
            if (bothOpen)
            {
                _bothHandsHoldTimer += Time.deltaTime;
            }
            else
            {
                // 1フレームの誤検出だけで進捗が消えないよう、離した時は倍速で減衰させる(即ゼロにはしない)。
                _bothHandsHoldTimer = Mathf.Max(0f, _bothHandsHoldTimer - Time.deltaTime * 2f);
            }

            if (_bothHandsHoldTimer >= _bothHandsOpenHoldSeconds)
            {
                _bothHandsHoldTimer = 0f;
                ultimateGauge?.TryTriggerFromExternal();
            }
        }

        // ILoveYouを出している間、味方全体(Team.Player)を毎フレームdeltaTime分だけ回復し続ける。
        // 開始した瞬間だけCommandAnnouncerで通知する(毎フレーム通知すると連呼になるため)。
        void UpdateHealPulse(bool active)
        {
            if (active && !_healing) CommandAnnouncer.Announce("回復");
            _healing = active;
            if (!active) return;

            var amount = _healPerSecond * Time.deltaTime;
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team != Team.Player || !c.IsAlive) continue;
                var health = c.GetComponent<CharacterHealth>();
                if (health != null) health.Heal(amount);
            }
        }

        // HandTrackingController.IsRightHandと同じ規約: パイプラインが鏡像化されていない前提のため、
        // MediaPipeが"Left"と分類した側が実際のプレイヤーの右手になる(判定を反転させる)。
        static bool IsRightHandAt(GestureRecognizerResult result, int index)
        {
            if (result.handedness == null || index >= result.handedness.Count) return false;
            var categories = result.handedness[index].categories;
            if (categories == null || categories.Count == 0) return false;
            return categories[0].categoryName == "Left";
        }

        static string GetTopGestureCategory(GestureRecognizerResult result, int handIndex)
        {
            if (result.gestures == null || handIndex >= result.gestures.Count) return null;
            var categories = result.gestures[handIndex].categories;
            return categories != null && categories.Count > 0 ? categories[0].categoryName : null;
        }

        // 定番7ジェスチャー(None/Closed_Fist/Open_Palm/Pointing_Up/Thumb_Down/Thumb_Up/Victory/ILoveYou)
        // のうち、このゲームで離散コマンドとして使う4つをHandPoseへ対応付ける。
        // ILoveYouはここには含めない(UpdateHealPulseで別枠の持続効果として扱うため)。
        static HandPose MapGesture(string categoryName) => categoryName switch
        {
            "Pointing_Up" => HandPose.IndexOnly,
            "Open_Palm" => HandPose.OpenPalm,
            "Victory" => HandPose.Scissors,
            "Thumb_Up" => HandPose.ThumbIndexMiddle,
            _ => HandPose.None,
        };

        // ApplyCommandと対になる、CommandAnnouncer用の表示名。
        static string PoseLabel(HandPose pose) => pose switch
        {
            HandPose.IndexOnly => "集合",
            HandPose.OpenPalm => "退避",
            HandPose.Scissors => "ボス集中攻撃",
            HandPose.ThumbIndexMiddle => "ボス以外集中攻撃",
            _ => null,
        };

        void UpdateConfirmedPose(HandPose pose)
        {
            if (pose == _pendingPose)
            {
                _pendingTimer += Time.deltaTime;
            }
            else
            {
                _pendingPose = pose;
                _pendingTimer = 0f;
            }

            if (_pendingTimer >= _poseConfirmSeconds && _confirmedPose != _pendingPose)
            {
                _confirmedPose = _pendingPose;
                var label = PoseLabel(_confirmedPose);
                if (label != null) CommandAnnouncer.Announce(label);
            }
        }

        bool TryProjectPalmToGround(List<NormalizedLandmark> normLm, out Vector3 worldPos)
        {
            worldPos = default;
            var cam = _trackingCamera != null ? _trackingCamera : Camera.main;
            if (cam == null) return false;

            // 手首+4指の付け根(正規化座標)の平均を「手の中心」とする。
            var palmX = (normLm[0].x + normLm[5].x + normLm[9].x + normLm[13].x + normLm[17].x) / 5f;
            var palmY = (normLm[0].y + normLm[5].y + normLm[9].y + normLm[13].y + normLm[17].y) / 5f;
            // _handTrackingController.NormalizedToViewport経由で変換することで、_mirrorX設定値の
            // ハードコードを避け、UIカーソル(UiPointerController)等と常に同じ変換規約に揃える。
            var viewport2D = _handTrackingController.NormalizedToViewport(palmX, palmY);
            var viewport = new Vector3(viewport2D.x, viewport2D.y, 0f);

            var ray = cam.ViewportPointToRay(viewport);
            var plane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
            if (!plane.Raycast(ray, out var enter)) return false;

            worldPos = ray.GetPoint(enter);
            return true;
        }

        void ApplyCommand(HandPose pose, Vector3 groundPos)
        {
            switch (pose)
            {
                case HandPose.IndexOnly:
                    BattleCommandState.SubmitGesture(PlayerCommandType.Rally, groundPos, FocusFireFilter.None);
                    break;
                case HandPose.OpenPalm:
                    BattleCommandState.SubmitGesture(PlayerCommandType.Flee, groundPos, FocusFireFilter.None);
                    break;
                case HandPose.Scissors:
                    BattleCommandState.SubmitGesture(PlayerCommandType.None, groundPos, FocusFireFilter.BossOnly);
                    break;
                case HandPose.ThumbIndexMiddle:
                    BattleCommandState.SubmitGesture(PlayerCommandType.None, groundPos, FocusFireFilter.ExcludeBoss);
                    break;
                default:
                    BattleCommandState.SubmitGesture(PlayerCommandType.None, groundPos, FocusFireFilter.None);
                    break;
            }
        }
    }
}
