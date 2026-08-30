using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 取得したコインの枚数を常時表示するカウンター。ScoreUIと同じ自己構築Canvasパターンの
    // シーン常駐シングルトン。コインがカメラ右下(CoinPickup.FlyTargetViewport)へ吸い込まれて
    // 消えるのに合わせて、その近くに配置している。
    public class CoinCountUI : MonoBehaviour
    {
        const float ShakeDecay = 8f;
        const float ShakeMaxOffset = 10f;

        static CoinCountUI _instance;

        Text text;
        RectTransform rect;
        float shakeTrauma;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("CoinCountUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<CoinCountUI>();
        }

        void Awake() => BuildUi();

        void OnEnable()
        {
            ScoreManager.OnCoinScoreChanged += HandleCoinCountChanged;
            HandleCoinCountChanged(ScoreManager.CoinScore, 0);
        }

        void OnDisable() => ScoreManager.OnCoinScoreChanged -= HandleCoinCountChanged;

        void BuildUi()
        {
            var canvasGo = new GameObject("CoinCountCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleRight;
            text.color = new Color(1f, 0.85f, 0.2f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            rect = text.rectTransform;
            // コインの吸い込み先(CoinPickup.FlyTargetViewport: 0.92, 0.08)のすぐ近くに置き、
            // コインがここへ集まって数字に反映されているように見せる。
            rect.anchorMin = new Vector2(0.9f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 60f);

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        // 表示のみコイン1枚=100枚分に見せる(実際のスコア計算やCoinScore自体には手を加えない)。
        const int DisplayMultiplier = 100;

        void HandleCoinCountChanged(int coinScore, int delta)
        {
            text.text = $"コイン: {coinScore * DisplayMultiplier}";
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
