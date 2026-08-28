using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // ボス出現前の「警告：ボスN」フルスクリーン赤点滅演出と、予告攻撃ごとの操作指示テキストを
    // 表示する。CommandAnnouncerと同じ自己構築Canvasパターンのシーン常駐シングルトン。
    public class BossWarningUI : MonoBehaviour
    {
        const float FadeSeconds = 0.3f;

        static BossWarningUI _instance;

        Image flashBg;
        Text introText;
        Text instructionText;
        CanvasGroup instructionGroup;
        float instructionTimer;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("BossWarningUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<BossWarningUI>();
        }

        // 呼び出し元(GameFlowManager)がyield returnして表示完了を待てるよう、コルーチンとして公開する。
        public static IEnumerator ShowBossIntroAsync(string title, float displaySeconds)
        {
            EnsureExists();
            yield return _instance.CoShowBossIntro(title, displaySeconds);
        }

        // ボスの予告攻撃ごとの操作指示("人差し指でキャラを移動させてよけろ！"等)を短時間表示する。
        public static void ShowInstruction(string text, float displaySeconds = 2.5f)
        {
            EnsureExists();
            _instance.instructionText.text = text;
            _instance.instructionTimer = displaySeconds;
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("BossWarningCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            canvasGo.AddComponent<CanvasScaler>();

            var bgGo = new GameObject("FlashBg", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            flashBg = bgGo.AddComponent<Image>();
            flashBg.color = new Color(1f, 0f, 0f, 0f);
            flashBg.raycastTarget = false;
            var bgRect = flashBg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            introText = BuildText(canvasGo.transform, "IntroText", 110, new Vector2(0.5f, 0.5f), new Vector2(1600f, 220f), Color.white);
            introText.canvasRenderer.SetAlpha(0f);

            var instructionTextObj = BuildText(canvasGo.transform, "InstructionText", 52, new Vector2(0.5f, 0.65f),
                new Vector2(1400f, 120f), new Color(1f, 0.9f, 0.3f));
            instructionText = instructionTextObj;
            instructionGroup = instructionTextObj.gameObject.AddComponent<CanvasGroup>();
            instructionGroup.alpha = 0f;
        }

        static Text BuildText(Transform parent, string name, int fontSize, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }

        IEnumerator CoShowBossIntro(string title, float displaySeconds)
        {
            introText.text = title;
            var elapsed = 0f;

            while (elapsed < displaySeconds)
            {
                elapsed += Time.deltaTime;

                var pulse = 0.35f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
                flashBg.color = new Color(1f, 0f, 0f, pulse);

                float alpha;
                if (elapsed < FadeSeconds) alpha = elapsed / FadeSeconds;
                else if (elapsed > displaySeconds - FadeSeconds) alpha = Mathf.Clamp01((displaySeconds - elapsed) / FadeSeconds);
                else alpha = 1f;
                introText.canvasRenderer.SetAlpha(alpha);

                yield return null;
            }

            flashBg.color = new Color(1f, 0f, 0f, 0f);
            introText.canvasRenderer.SetAlpha(0f);
        }

        void Update()
        {
            if (instructionTimer <= 0f) return;

            instructionTimer -= Time.deltaTime;
            instructionGroup.alpha = Mathf.Clamp01(instructionTimer / FadeSeconds);
        }
    }
}
