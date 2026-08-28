using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 現在の合計スコア(コイン+コンボ)を常時表示するカウンター。ComboUIと同じく、値が増える
    // たびにテキストを短時間震わせる。CommandAnnouncerと同じ自己構築Canvasパターンの
    // シーン常駐シングルトン。
    public class ScoreUI : MonoBehaviour
    {
        const float ShakeDecay = 8f;
        const float ShakeMaxOffset = 10f;

        static ScoreUI _instance;

        Text text;
        RectTransform rect;
        float shakeTrauma;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("ScoreUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScoreUI>();
        }

        void Awake() => BuildUi();

        void OnEnable()
        {
            ScoreManager.OnScoreChanged += HandleScoreChanged;
            HandleScoreChanged(ScoreManager.TotalScore, 0);
        }

        void OnDisable() => ScoreManager.OnScoreChanged -= HandleScoreChanged;

        void BuildUi()
        {
            var canvasGo = new GameObject("ScoreCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(1f, 0.85f, 0.2f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.02f, 0.9f);
            rect.anchorMax = new Vector2(0.02f, 0.9f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(500f, 70f);

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        void HandleScoreChanged(int totalScore, int delta)
        {
            text.text = $"SCORE: {totalScore}";
            if (delta > 0) shakeTrauma = Mathf.Clamp01(shakeTrauma + 0.5f);
        }

        void Update()
        {
            if (shakeTrauma <= 0f)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            shakeTrauma = Mathf.Max(0f, shakeTrauma - ShakeDecay * Time.deltaTime);
            var strength = shakeTrauma * shakeTrauma * ShakeMaxOffset;
            var seed = Time.time * 40f;
            rect.anchoredPosition = new Vector2(
                (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f) * strength;
        }
    }
}
