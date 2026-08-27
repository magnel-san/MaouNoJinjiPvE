using System.Collections.Generic;
using Game.HandTracking;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;
using NormalizedLandmark = Mediapipe.Tasks.Components.Containers.NormalizedLandmark;

namespace Game
{
    // 戦闘フェーズ中のみ有効化される、手のジェスチャーによる全体コマンド入力(理想形/本命)。
    // 既存のHandTrackingController.OnHandLandmarkerResultをそのまま購読するだけで、
    // MediaPipe側の追加設定は不要。
    //
    // 利き手(HandPreference、ゲーム開始直後にHandPreferenceSelectUIで選択済み)のパーム中心を
    // カーソル位置とし、その手の静的な形(人差し指のみ/パー/親指+人差し指/親指+人差し指+中指)で
    // keyboard1〜4相当のコマンドを決定する。それとは別に、左右両方の手が同時に検出できた場合のみ
    // 「両手パーを2秒キープ」を判定し、keyboard6相当(必殺技)をUltimateGaugeControllerへ直接要求する
    // (こちらは利き手に関係なく両手を見る)。
    //
    // 手が検出されていない間(HasHandDataThisFrame=false)は何もしない。BattleCursorInputDebugが
    // それを見てキーボード/マウス側にフォールバックする(このスクリプト単体では何も保証しない、疎結合)。
    //
    // 注記: 2026-08-28時点でこのプロジェクトはhand_landmarker.bytesの配置不備によりMediaPipeの
    // HandLandmarker初期化に失敗しており、実機でのジェスチャー検証はそのモデルファイル配置が
    // 解消してから行う想定。閾値(Thresholds)は未検証の初期値のため、実際にカメラで試しながら調整すること。
    public class BattleGestureInputController : MonoBehaviour
    {
        [SerializeField] private HandTrackingController _handTrackingController;
        [Tooltip("戦場の地面とみなす高さ(この水平面へパーム中心を投影してカーソル位置にする)")]
        [SerializeField] private float _groundHeight = 0f;
        [Tooltip("パーム中心を戦場へ投影するのに使うカメラ。未設定ならCamera.mainを使う")]
        [SerializeField] private Camera _trackingCamera;
        [Tooltip("同じ手の形をこの秒数保持したらコマンドとして確定する(チラつき対策のdwell)")]
        [SerializeField] private float _poseConfirmSeconds = 0.12f;
        [SerializeField] private HandPoseClassifier.Thresholds _thresholds = HandPoseClassifier.Thresholds.Default;

        [Header("両手パー保持による必殺技発動")]
        [Tooltip("両手ともパーの状態を何秒キープしたら必殺技(keyboard6相当)を発動するか")]
        [SerializeField] private float _bothHandsOpenHoldSeconds = 2f;

        UltimateGaugeController ultimateGauge;

        HandPose _pendingPose = HandPose.None;
        HandPose _confirmedPose = HandPose.None;
        float _pendingTimer;
        float _bothHandsHoldTimer;

        /// <summary>直近のHandLandmarker結果で、利き手側のデータが実際に検出できたか。
        /// BattleCursorInputDebugがこれを見て、キーボード入力を上書きするかどうかを判断する。</summary>
        public bool HasHandDataThisFrame { get; private set; }

        void Awake()
        {
            ultimateGauge = GetComponent<UltimateGaugeController>();
        }

        void OnEnable()
        {
            RallyCircleIndicator.EnsureExists();
            if (_handTrackingController != null) _handTrackingController.OnHandLandmarkerResult += HandleResult;
        }

        void OnDisable()
        {
            if (_handTrackingController != null) _handTrackingController.OnHandLandmarkerResult -= HandleResult;
            HasHandDataThisFrame = false;
            _bothHandsHoldTimer = 0f;
        }

        void HandleResult(HandLandmarkerResult result)
        {
            HasHandDataThisFrame = false;
            if (result.handWorldLandmarks == null || _handTrackingController == null)
            {
                UpdateBothHandsHold(false);
                UpdateConfirmedPose(HandPose.None);
                return;
            }

            var preferRight = HandPreference.PreferRightHand;
            HandPose? leftPose = null;
            HandPose? rightPose = null;
            var selectedIndex = -1;

            for (var i = 0; i < result.handWorldLandmarks.Count; i++)
            {
                var worldLm = result.handWorldLandmarks[i].landmarks;
                if (worldLm == null || worldLm.Count < 21) continue;

                var isRight = IsRightHandAt(result, i);
                var pose = HandPoseClassifier.Classify(ToVector3Array(worldLm), _thresholds);
                if (isRight) rightPose = pose; else leftPose = pose;

                // 利き手側を優先して選ぶ。データが無ければもう片方にフォールバックする。
                if (isRight == preferRight) selectedIndex = i;
                else if (selectedIndex < 0) selectedIndex = i;
            }

            // 利き手に関係なく、両手が同時にパーであるかどうかを見る(必殺技の発動条件)。
            UpdateBothHandsHold(leftPose == HandPose.OpenPalm && rightPose == HandPose.OpenPalm);

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

        static Vector3[] ToVector3Array(List<Landmark> lm)
        {
            var points = new Vector3[21];
            for (var i = 0; i < 21; i++) points[i] = new Vector3(lm[i].x, lm[i].y, lm[i].z);
            return points;
        }

        // HandTrackingController.IsRightHandと同じ規約: パイプラインが鏡像化されていない前提のため、
        // MediaPipeが"Left"と分類した側が実際のプレイヤーの右手になる(判定を反転させる)。
        static bool IsRightHandAt(HandLandmarkerResult result, int index)
        {
            if (result.handedness == null || index >= result.handedness.Count) return false;
            var categories = result.handedness[index].categories;
            if (categories == null || categories.Count == 0) return false;
            return categories[0].categoryName == "Left";
        }

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

            if (_pendingTimer >= _poseConfirmSeconds)
            {
                _confirmedPose = _pendingPose;
            }
        }

        bool TryProjectPalmToGround(List<NormalizedLandmark> normLm, out Vector3 worldPos)
        {
            worldPos = default;
            var cam = _trackingCamera != null ? _trackingCamera : Camera.main;
            if (cam == null) return false;

            // 手首+4指の付け根(正規化座標)の平均を「手の中心」とする(HandPoseClassifier.PalmCenterと同じ考え方)。
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
                case HandPose.ThumbIndex:
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
