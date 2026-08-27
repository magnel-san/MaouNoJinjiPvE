using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // ジェスチャーコマンドが発動した瞬間に、画面上へ「命令：〜が発動！」を一瞬表示するフィードバックUI。
    // CombatFx/RallyCircleIndicatorと同じ「実行時にオブジェクトを組み立てる」パターンを踏襲し、
    // 専用のScreen Space - Overlay Canvasを自前で持つ(既存のCanvas配線に依存しない、シーン常駐の単一インスタンス)。
    public class CommandAnnouncer : MonoBehaviour
    {
        const float FadeInSeconds = 0.1f;
        const float HoldSeconds = 0.9f;
        const float FadeOutSeconds = 0.4f;

        static CommandAnnouncer _instance;

        Text _text;
        CanvasGroup _canvasGroup;
        float _timer;
        bool _showing;

        // 呼ぶだけでよい。表示中に再度呼ばれた場合はタイマーをリセットして表示し直す。
        public static void Announce(string label)
        {
            if (string.IsNullOrEmpty(label)) return;

            if (_instance == null)
            {
                var go = new GameObject("CommandAnnouncer");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CommandAnnouncer>();
            }

            _instance.Show($"命令：{label}が発動！");
        }

        void Awake()
        {
            var canvasGo = new GameObject("CommandAnnouncerCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            _text = textGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 56;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = Color.white;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rect = _text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.85f);
            rect.anchorMax = new Vector2(0.5f, 0.85f);
            rect.sizeDelta = new Vector2(1200, 100);
            rect.anchoredPosition = Vector2.zero;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
        }

        void Show(string message)
        {
            _text.text = message;
            _timer = 0f;
            _showing = true;
        }

        void Update()
        {
            if (!_showing) return;
            _timer += Time.deltaTime;

            if (_timer < FadeInSeconds)
            {
                _canvasGroup.alpha = _timer / FadeInSeconds;
            }
            else if (_timer < FadeInSeconds + HoldSeconds)
            {
                _canvasGroup.alpha = 1f;
            }
            else if (_timer < FadeInSeconds + HoldSeconds + FadeOutSeconds)
            {
                _canvasGroup.alpha = 1f - (_timer - FadeInSeconds - HoldSeconds) / FadeOutSeconds;
            }
            else
            {
                _canvasGroup.alpha = 0f;
                _showing = false;
            }
        }
    }
}
