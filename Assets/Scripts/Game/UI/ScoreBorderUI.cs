using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // スコアに応じて色(緑→赤)と太さ(最小→最大)が変化する画面四辺の枠。BossHpBarUI/CommandAnnouncerと
    // 同じ「実行時にCanvasごと自己構築する」方式のシーン常駐シングルトン。ScoreManager.OnScoreChangedを
    // 購読して見た目を更新し、増加のたびに枠を短時間震わせる。
    // ボスの予告攻撃警告(画面枠赤点滅)にも同じ枠を使い回せるよう、static FlashRed()を公開する
    // (警告点滅は色状態の上に重ねる別レイヤーとして描画するため、スコアの色表示と干渉しない)。
    public class ScoreBorderUI : MonoBehaviour
    {
        [SerializeField] private int _maxScoreForFullRed = 500;
        [SerializeField] private float _minThickness = 4f;
        [SerializeField] private float _maxThickness = 40f;
        [SerializeField] private float _shakeDecay = 6f;
        [SerializeField] private float _shakeMaxOffset = 10f;

        static readonly Color LowColor = new Color(0.25f, 0.85f, 0.35f, 0.9f);
        static readonly Color HighColor = new Color(0.9f, 0.15f, 0.15f, 0.9f);
        static readonly Color WarningColor = new Color(1f, 0.1f, 0.1f, 0.85f);

        static ScoreBorderUI _instance;

        Image top, bottom, left, right;
        Image flashTop, flashBottom, flashLeft, flashRight;
        float shakeTrauma;
        float flashRemaining;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("ScoreBorderUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScoreBorderUI>();
        }

        // ボスの予告攻撃警告用: 画面枠を指定秒数だけ赤く点滅させる。
        public static void FlashRed(float duration)
        {
            EnsureExists();
            _instance.flashRemaining = Mathf.Max(_instance.flashRemaining, duration);
        }

        void Awake()
        {
            BuildUi();
        }

        void OnEnable() => ScoreManager.OnScoreChanged += HandleScoreChanged;
        void OnDisable() => ScoreManager.OnScoreChanged -= HandleScoreChanged;

        void BuildUi()
        {
            var canvasGo = new GameObject("ScoreBorderCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            top = BuildEdge(canvasGo.transform, "Top", LowColor);
            bottom = BuildEdge(canvasGo.transform, "Bottom", LowColor);
            left = BuildEdge(canvasGo.transform, "Left", LowColor);
            right = BuildEdge(canvasGo.transform, "Right", LowColor);

            flashTop = BuildEdge(canvasGo.transform, "FlashTop", WarningColor);
            flashBottom = BuildEdge(canvasGo.transform, "FlashBottom", WarningColor);
            flashLeft = BuildEdge(canvasGo.transform, "FlashLeft", WarningColor);
            flashRight = BuildEdge(canvasGo.transform, "FlashRight", WarningColor);
            SetFlashAlpha(0f);

            ApplyThicknessAndColor(_minThickness, LowColor);
        }

        static Image BuildEdge(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        void HandleScoreChanged(int totalScore, int delta)
        {
            var t = _maxScoreForFullRed > 0 ? Mathf.Clamp01((float)totalScore / _maxScoreForFullRed) : 0f;
            var thickness = Mathf.Lerp(_minThickness, _maxThickness, t);
            var color = Color.Lerp(LowColor, HighColor, t);
            ApplyThicknessAndColor(thickness, color);

            if (delta > 0) shakeTrauma = Mathf.Clamp01(shakeTrauma + 0.35f);
        }

        void ApplyThicknessAndColor(float thickness, Color color)
        {
            LayoutEdge(top, thickness, RectTransform.Edge.Top);
            LayoutEdge(bottom, thickness, RectTransform.Edge.Bottom);
            LayoutEdge(left, thickness, RectTransform.Edge.Left);
            LayoutEdge(right, thickness, RectTransform.Edge.Right);

            top.color = color;
            bottom.color = color;
            left.color = color;
            right.color = color;
        }

        void Update()
        {
            UpdateShake();
            UpdateFlash();
        }

        void UpdateShake()
        {
            if (shakeTrauma <= 0f) return;

            shakeTrauma = Mathf.Max(0f, shakeTrauma - _shakeDecay * Time.deltaTime);
            var strength = shakeTrauma * shakeTrauma * _shakeMaxOffset;
            var seed = Time.time * 30f;
            var offset = new Vector3(
                (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f,
                0f) * strength;

            top.rectTransform.anchoredPosition = new Vector2(offset.x, offset.y);
            bottom.rectTransform.anchoredPosition = new Vector2(offset.x, offset.y);
            left.rectTransform.anchoredPosition = new Vector2(offset.x, offset.y);
            right.rectTransform.anchoredPosition = new Vector2(offset.x, offset.y);
        }

        void UpdateFlash()
        {
            if (flashRemaining <= 0f)
            {
                SetFlashAlpha(0f);
                return;
            }

            flashRemaining -= Time.deltaTime;
            var pulse = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
            SetFlashAlpha(flashRemaining > 0f ? pulse : 0f);

            LayoutEdge(flashTop, _maxThickness, RectTransform.Edge.Top);
            LayoutEdge(flashBottom, _maxThickness, RectTransform.Edge.Bottom);
            LayoutEdge(flashLeft, _maxThickness, RectTransform.Edge.Left);
            LayoutEdge(flashRight, _maxThickness, RectTransform.Edge.Right);
        }

        void SetFlashAlpha(float alpha)
        {
            SetAlpha(flashTop, alpha);
            SetAlpha(flashBottom, alpha);
            SetAlpha(flashLeft, alpha);
            SetAlpha(flashRight, alpha);
        }

        static void SetAlpha(Image image, float alpha)
        {
            var c = WarningColor;
            c.a = alpha;
            image.color = c;
        }

        static void LayoutEdge(Image image, float thickness, RectTransform.Edge edge)
        {
            var rect = image.rectTransform;
            switch (edge)
            {
                case RectTransform.Edge.Top:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case RectTransform.Edge.Bottom:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(0f, thickness);
                    break;
                case RectTransform.Edge.Left:
                    rect.anchorMin = new Vector2(0f, 0f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
                default:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 0.5f);
                    rect.sizeDelta = new Vector2(thickness, 0f);
                    break;
            }
        }
    }
}
