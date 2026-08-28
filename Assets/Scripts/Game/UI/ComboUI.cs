using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // ボスへの攻撃が1秒以内に継続している間、"Ncombo"を表示する。CommandAnnouncerと同じ
    // 自己構築Canvasパターンのシーン常駐シングルトン。ComboTracker.OnComboChangedを購読するだけでよい。
    public class ComboUI : MonoBehaviour
    {
        const float ShakeDecay = 8f;
        const float ShakeMaxOffset = 14f;
        const float FadeOutSeconds = 0.3f;

        static ComboUI _instance;

        Text text;
        RectTransform rect;
        CanvasGroup canvasGroup;
        float shakeTrauma;
        float displayAlphaTarget;
        float currentAlpha;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("ComboUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ComboUI>();
        }

        void Awake() => BuildUi();

        void OnEnable() => ComboTracker.OnComboChanged += HandleComboChanged;
        void OnDisable() => ComboTracker.OnComboChanged -= HandleComboChanged;

        void BuildUi()
        {
            var canvasGo = new GameObject("ComboCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.9f, 0.2f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.82f, 0.78f);
            rect.anchorMax = new Vector2(0.82f, 0.78f);
            rect.sizeDelta = new Vector2(400f, 100f);

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        void HandleComboChanged(int combo)
        {
            if (combo <= 0)
            {
                displayAlphaTarget = 0f;
                return;
            }

            text.text = $"{combo}combo";
            displayAlphaTarget = 1f;
            shakeTrauma = Mathf.Clamp01(0.3f + combo * 0.06f);
        }

        void Update()
        {
            currentAlpha = displayAlphaTarget > currentAlpha
                ? displayAlphaTarget
                : Mathf.MoveTowards(currentAlpha, displayAlphaTarget, Time.deltaTime / FadeOutSeconds);
            canvasGroup.alpha = currentAlpha;

            if (shakeTrauma > 0f)
            {
                shakeTrauma = Mathf.Max(0f, shakeTrauma - ShakeDecay * Time.deltaTime);
                var strength = shakeTrauma * shakeTrauma * ShakeMaxOffset;
                var seed = Time.time * 40f;
                rect.anchoredPosition = new Vector2(
                    (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f,
                    (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f) * strength;
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }
}
