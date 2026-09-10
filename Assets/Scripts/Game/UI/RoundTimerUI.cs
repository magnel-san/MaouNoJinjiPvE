using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // ラウンドの残り時間を画面上部に表示するカウントダウン。BossHpBarUIと重ならないよう、
    // その少し下に配置する。GameFlowManager.WaitForRoundEndがShow/UpdateRemaining/Hideを呼ぶ。
    public class RoundTimerUI : MonoBehaviour
    {
        static readonly Color NormalColor = Color.white;
        static readonly Color WarningColor = new Color(1f, 0.3f, 0.25f);
        const float WarningThresholdSeconds = 10f;

        static RoundTimerUI _instance;

        Text text;
        CanvasGroup canvasGroup;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("RoundTimerUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<RoundTimerUI>();
        }

        public static void Show(float durationSeconds)
        {
            EnsureExists();
            if (durationSeconds <= 0f)
            {
                _instance.canvasGroup.alpha = 0f;
                return;
            }
            _instance.canvasGroup.alpha = 1f;
            _instance.ApplyRemaining(durationSeconds);
        }

        public static void UpdateRemaining(float remainingSeconds)
        {
            if (_instance == null) return;
            _instance.ApplyRemaining(remainingSeconds);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.canvasGroup.alpha = 0f;
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("RoundTimerCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var textRect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            textRect.SetParent(canvasGo.transform, false);
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -110f);
            textRect.sizeDelta = new Vector2(300f, 50f);

            text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = NormalColor;
            text.raycastTarget = false;

            var outline = textRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        void ApplyRemaining(float remainingSeconds)
        {
            var clamped = Mathf.Max(0f, remainingSeconds);
            var minutes = Mathf.FloorToInt(clamped / 60f);
            var seconds = Mathf.FloorToInt(clamped % 60f);
            text.text = $"残り {minutes:00}:{seconds:00}";
            text.color = clamped <= WarningThresholdSeconds ? WarningColor : NormalColor;
        }
    }
}
