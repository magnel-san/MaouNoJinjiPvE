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
    // カーソル位置とし、その手の静的な形(人差し指のみ/パー)で移動系コマンドを決定する:
    // 人差し指のみ→集合(Rally)、パー→よける(Flee、危険地帯回避込み)。それとは別に、左右両方の手が
    // 同時に検出できた場合のみ「両手パーを2秒キープ」を判定し、keyboard6相当(必殺技)を
    // UltimateGaugeControllerへ直接要求する(こちらは利き手に関係なく両手を見る)。加えて、
    // 人差し指+小指のみ伸展(IndexPinky)は片手だけでもゲージが溜まっていれば即座に必殺技を
    // 要求できる簡易トリガーにしている(利き手に関係なくどちらかの手で成立すればよい)。
    //
    // NOTE: 一度MediaPipe GestureRecognizer(学習済み定番ジェスチャー分類)へ切り替えたが、
    // バンドル内のhand_landmarker.taskをネイティブ側が解決できずロードに失敗する既知の問題があったため、
    // HandLandmarkerの生ランドマーク＋HandPoseClassifierによる自前の幾何学的判定へ戻した(ClassifyPose参照)。
    // HandPoseClassifier側で指ごとにヒステリシス(前フレームの伸展/屈曲状態を見て、判定の上げ下げに
    // 別の閾値を使う)を掛けているため、_leftFingerState/_rightFingerStateに前フレームの状態を保持し、
    // 毎フレームHandPoseClassifier.GetFingerStateへ渡す。
    //
    // 防御(グー)は離散コマンドとは別枠の「持続効果」として扱い、出している間ずっと
    // BattleCommandState.GuardActiveを立てる(実際のダメージ軽減はCharacterHealth.ApplyDamageが
    // 行う。UpdateGuardHold参照)。以前チョキに割り当てていた回復、および親指の向き(グッドサイン/
    // バッドサイン)によるボス集中攻撃/ボス以外集中攻撃コマンドは廃止した
    // (Scissors/ThumbUp/ThumbDownのポーズ自体はHandPoseClassifierが検出するが、
    // ApplyCommand/PoseLabelでは使用しない。チョキは将来また使う可能性があるため判定だけ残してある)。
    //
    // コマンドが新しく確定した瞬間、および防御が開始した瞬間にCommandAnnouncerで
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
        [SerializeField] private HandPoseClassifier.Thresholds _thresholds = HandPoseClassifier.Thresholds.Default;

        [Header("両手パー保持による必殺技発動")]
        [Tooltip("両手ともパーの状態を何秒キープしたら必殺技(keyboard6相当)を発動するか")]
        [SerializeField] private float _bothHandsOpenHoldSeconds = 2f;


        [Header("デバッグ")]
        [Tooltip("各指の生の判定値(直線度/距離比)と伸展/屈曲の状態を画面左上にリアルタイム表示する。" +
            "実機のカメラで映しながら_thresholdsをチューニングするために使う。調整が終わったらオフにしてよい。")]
        [SerializeField] private bool _debugOverlay = true;

        UltimateGaugeController ultimateGauge;

        HandPose _pendingPose = HandPose.None;
        HandPose _confirmedPose = HandPose.None;
        float _pendingTimer;
        float _bothHandsHoldTimer;
        bool _guarding;

        // ヒステリシス計算のために前フレームの指の伸展/屈曲状態を保持する(HandPoseClassifier参照)。
        HandPoseClassifier.FingerState _leftFingerState;
        HandPoseClassifier.FingerState _rightFingerState;

        // デバッグ表示用に、直近フレームで検出できた指標値をキャッシュしておく。
        HandPoseClassifier.FingerMetrics _leftMetrics;
        HandPoseClassifier.FingerMetrics _rightMetrics;
        HandPoseClassifier.ThumbDirection _leftThumbDir;
        HandPoseClassifier.ThumbDirection _rightThumbDir;
        bool _leftHandDetected;
        bool _rightHandDetected;

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
            UpdateGuardHold(false);
        }

        void HandleResult(HandLandmarkerResult result)
        {
            HasHandDataThisFrame = false;
            _leftHandDetected = false;
            _rightHandDetected = false;

            if (result.handWorldLandmarks == null || _handTrackingController == null)
            {
                UpdateBothHandsHold(false);
                UpdateGuardHold(false);
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
                var points = ToVector3Array(worldLm);
                var previous = isRight ? _rightFingerState : _leftFingerState;
                var fingers = HandPoseClassifier.GetFingerState(points, _thresholds, previous, out var metrics);
                var thumbDir = HandPoseClassifier.GetThumbDirection(points, _thresholds.ThumbDirectionMargin);
                var pose = ClassifyPose(fingers, metrics, thumbDir, _thresholds.ClearlyExtendedStraightness);
                if (isRight) { rightPose = pose; _rightFingerState = fingers; _rightMetrics = metrics; _rightThumbDir = thumbDir; _rightHandDetected = true; }
                else { leftPose = pose; _leftFingerState = fingers; _leftMetrics = metrics; _leftThumbDir = thumbDir; _leftHandDetected = true; }

                // 利き手側を優先して選ぶ。データが無ければもう片方にフォールバックする。
                if (isRight == preferRight) selectedIndex = i;
                else if (selectedIndex < 0) selectedIndex = i;
            }

            // 利き手に関係なく、両手が同時にパーであるかどうかを見る(必殺技の発動条件)。
            UpdateBothHandsHold(leftPose == HandPose.OpenPalm && rightPose == HandPose.OpenPalm);

            // グーも利き手に関係なく、どちらかの手で出ていれば防御状態にする(離散コマンドとは独立)。
            UpdateGuardHold(leftPose == HandPose.Fist || rightPose == HandPose.Fist);

            // 人差し指+小指も利き手に関係なく、片手だけで即発動要求できる(両手パー2秒キープの簡易版)。
            // TryTriggerFromExternal側でIsReady&&!IsBoostActiveを見ているので、ゲージ未満なら何も起きず、
            // 毎フレーム呼んでも二重発動しない。
            if (leftPose == HandPose.IndexPinky || rightPose == HandPose.IndexPinky) ultimateGauge?.TryTriggerFromExternal();

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
            BattleCommandState.SetBothHandsOpen(bothOpen);

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

        // グーを出している間、BattleCommandState.GuardActiveを立てるだけの持続コマンド。
        // 実際のダメージ軽減はCharacterHealth.ApplyDamageがGuardActiveを見て行う
        // (このメソッドはフラグの管理と開始時のアナウンス・盾装備効果音のみを担当する)。
        void UpdateGuardHold(bool active)
        {
            if (active && !_guarding)
            {
                CommandAnnouncer.Announce("防御");
                var cfg = GameBalanceConfig.Instance;
                if (cfg != null)
                {
                    var cam = Camera.main;
                    SfxUtil.PlayAt(cfg.GuardEquipSound, cam != null ? cam.transform.position : transform.position);
                }
            }
            _guarding = active;
            BattleCommandState.SetGuardActive(active);
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

        // 5指の伸展/屈曲状態から、離散コマンドに使う形を判定する。
        //
        // パーは親指の伸展判定(距離比ベース)を条件に含めない。親指を横に大きく開かなくても
        // 残り4指さえ伸びていれば「パーのつもり」であることが実機テストで分かったため
        // (親指の距離比が伸展しきい値に届かず、パーの残り4指が0.96〜0.99と明確に伸びていても
        // 不成立になっていたバグ)、親指の状態に関係なく残り4指の伸展だけで判定する。
        //
        // ThumbUp(グッドサイン)/ThumbDown(バッドサイン)は、親指自身の伸展判定(f.Thumb)を要求せず、
        // 向き(thumbDir、真上/真下)だけを主判定にする。ThumbDown時は掌がカメラを向くのに対し
        // ThumbUp時は掌が反対を向くことが多く、他4指の屈曲判定がオクルージョンでブレて
        // 「屈曲している」と確定しづらいため(ThumbDownは反応するがThumbUpは全く反応しないバグの原因だった)。
        // 誤検出防止のため、他4指のうち明らかに(通常よりずっと厳しいClearlyExtendedStraightness基準で)
        // 伸びている指が1本でもあれば、パー等の別ジェスチャーの途中とみなしキャンセルする。
        static HandPose ClassifyPose(HandPoseClassifier.FingerState f, HandPoseClassifier.FingerMetrics m, HandPoseClassifier.ThumbDirection thumbDir, float clearlyExtendedThreshold)
        {
            if (!f.Thumb && f.Index && !f.Middle && !f.Ring && !f.Pinky) return HandPose.IndexOnly;
            if (f.Index && f.Middle && f.Ring && f.Pinky) return HandPose.OpenPalm;
            if (!f.Thumb && f.Index && f.Middle && !f.Ring && !f.Pinky) return HandPose.Scissors;
            if (!f.Thumb && f.Index && !f.Middle && !f.Ring && f.Pinky) return HandPose.IndexPinky;

            // グー: 親指含め5指すべて屈曲。ThumbUp/ThumbDown(親指だけ上/下に突き出す)と区別するため、
            // 親指の向きがNeutral(はっきり上でも下でもない=握り込んでいる)場合のみグーとみなす。
            if (!f.Thumb && !f.Index && !f.Middle && !f.Ring && !f.Pinky
                && thumbDir == HandPoseClassifier.ThumbDirection.Neutral)
            {
                return HandPose.Fist;
            }

            if (thumbDir != HandPoseClassifier.ThumbDirection.Neutral && !AnyClearlyExtended(m, clearlyExtendedThreshold))
                return thumbDir == HandPoseClassifier.ThumbDirection.Down ? HandPose.ThumbDown : HandPose.ThumbUp;

            return HandPose.None;
        }

        static bool AnyClearlyExtended(HandPoseClassifier.FingerMetrics m, float threshold) =>
            m.Index > threshold || m.Middle > threshold || m.Ring > threshold || m.Pinky > threshold;

        // ApplyCommandと対になる、CommandAnnouncer用の表示名。Scissors(防御)はUpdateGuardHoldが
        // 独自に「防御」を通知するため、ここではnull(無表示)にして二重通知を避ける。
        static string PoseLabel(HandPose pose) => pose switch
        {
            HandPose.IndexOnly => "集合",
            HandPose.OpenPalm => "よける",
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
                default:
                    BattleCommandState.SubmitGesture(PlayerCommandType.None, groundPos, FocusFireFilter.None);
                    break;
            }
        }

        // 各指の生の判定値(直線度/距離比)と伸展/屈曲の状態を画面左上に表示する。
        // カメラに手を映しながら_thresholdsをInspectorで調整するためのツール。
        void OnGUI()
        {
            if (!_debugOverlay) return;

            var y = 10f;
            GUI.Label(new Rect(10, y, 600, 20), $"Pose: {_confirmedPose} (pending: {_pendingPose} {_pendingTimer:F2}s)");
            y += 20;

            if (_rightHandDetected) DrawHandDebug(ref y, "Right", _rightMetrics, _rightFingerState, _rightThumbDir);
            else GUI.Label(new Rect(10, y, 600, 20), "Right: 未検出");
            y += 20;

            if (_leftHandDetected) DrawHandDebug(ref y, "Left", _leftMetrics, _leftFingerState, _leftThumbDir);
            else GUI.Label(new Rect(10, y, 600, 20), "Left: 未検出");
        }

        static void DrawHandDebug(ref float y, string label, HandPoseClassifier.FingerMetrics m, HandPoseClassifier.FingerState f, HandPoseClassifier.ThumbDirection thumbDir)
        {
            string Cell(string name, float value, bool extended) => $"{name} {value:F2}{(extended ? "○" : "×")}";
            var text = $"{label}: " +
                $"{Cell("親指", m.Thumb, f.Thumb)}({thumbDir})  {Cell("人差", m.Index, f.Index)}  " +
                $"{Cell("中指", m.Middle, f.Middle)}  {Cell("薬指", m.Ring, f.Ring)}  {Cell("小指", m.Pinky, f.Pinky)}";
            GUI.Label(new Rect(10, y, 700, 20), text);
        }
    }
}
