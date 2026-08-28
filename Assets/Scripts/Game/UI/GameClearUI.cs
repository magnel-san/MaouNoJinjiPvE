using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 最終決戦クリア時に表示する「GAME CLEAR」+最終スコア。CommandAnnouncerと同じ
    // 自己構築Canvasパターンのシーン常駐シングルトン。
    public class GameClearUI : MonoBehaviour
    {
        static GameClearUI _instance;

        Text scoreText;
        CanvasGroup canvasGroup;
        float timer;

        public static void Show(int score, float displaySeconds)
        {
            EnsureExists();
            _instance.scoreText.text = $"SCORE: {score}";
            _instance.timer = displaySeconds;
            _instance.canvasGroup.alpha = 1f;
        }

        static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("GameClearUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameClearUI>();
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("GameClearCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            canvasGo.AddComponent<CanvasScaler>();

            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var titleText = BuildText(canvasGo.transform, "Title", 120, new Vector2(0.5f, 0.6f), new Vector2(1400f, 200f), Color.white);
            titleText.text = "GAME CLEAR";

            scoreText = BuildText(canvasGo.transform, "Score", 60, new Vector2(0.5f, 0.42f), new Vector2(1000f, 100f), new Color(1f, 0.9f, 0.3f));

            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            var rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) canvasGroup.alpha = 0f;
        }
    }
}
