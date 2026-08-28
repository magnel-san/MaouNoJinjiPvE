using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    // 最終決戦クリア後のリザルト画面。累計ダメージ→コインスコア→コンボスコア→合計スコアの順に
    // 0から高速でカウントアップ表示し、合計を拡大したのち「score:～ 評価:～ 魔王からの評価「～」」を
    // 表示してEnterキー入力を待つ。CommandAnnouncerと同じ自己構築Canvasパターンのシーン常駐シングルトン。
    public class GameResultUI : MonoBehaviour
    {
        const float CountUpDuration = 1.1f;
        const float HoldAfterCountUp = 0.5f;

        static GameResultUI _instance;

        Image bg;
        Text captionText;
        Text numberText;
        Text scoreLineText;
        Text commentText;
        Text pressEnterText;
        CanvasGroup group;

        public static IEnumerator ShowAsync(float damageTotal, int coinScore, int comboScore, int totalScore,
            string ratingLabel, string comment)
        {
            EnsureExists();
            yield return _instance.CoShow(damageTotal, coinScore, comboScore, totalScore, ratingLabel, comment);
        }

        static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("GameResultUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameResultUI>();
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("GameResultCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            canvasGo.AddComponent<CanvasScaler>();

            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            bg.raycastTarget = false;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            captionText = BuildText(canvasGo.transform, "Caption", 44, new Vector2(0.5f, 0.68f), new Vector2(1200f, 80f), new Color(0.8f, 0.85f, 1f));
            numberText = BuildText(canvasGo.transform, "Number", 96, new Vector2(0.5f, 0.52f), new Vector2(1400f, 160f), Color.white);
            scoreLineText = BuildText(canvasGo.transform, "ScoreLine", 40, new Vector2(0.5f, 0.32f), new Vector2(1400f, 70f), new Color(1f, 0.9f, 0.3f));
            commentText = BuildText(canvasGo.transform, "Comment", 34, new Vector2(0.5f, 0.22f), new Vector2(1500f, 90f), Color.white);
            pressEnterText = BuildText(canvasGo.transform, "PressEnter", 28, new Vector2(0.5f, 0.08f), new Vector2(900f, 60f), new Color(0.8f, 0.8f, 0.8f));
            pressEnterText.text = "";

            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
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
            text.text = "";

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return text;
        }

        IEnumerator CoShow(float damageTotal, int coinScore, int comboScore, int totalScore, string ratingLabel, string comment)
        {
            group.alpha = 1f;
            captionText.text = "";
            numberText.text = "";
            numberText.rectTransform.localScale = Vector3.one;
            scoreLineText.text = "";
            commentText.text = "";
            pressEnterText.text = "";

            yield return CoCountUp("累計ダメージ", Mathf.CeilToInt(damageTotal));
            yield return CoCountUp("コインで得たスコア", coinScore);
            yield return CoCountUp("コンボで得たスコア", comboScore);
            yield return CoCountUp("合計スコア", totalScore);

            // 合計スコアの数値を拡大する。
            var t = 0f;
            const float enlargeDuration = 0.4f;
            while (t < enlargeDuration)
            {
                t += Time.deltaTime;
                var scale = Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(t / enlargeDuration));
                numberText.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }

            scoreLineText.text = $"score:{totalScore}　評価:{ratingLabel}";
            commentText.text = $"魔王からの評価「{comment}」";
            pressEnterText.text = "Enterキーでもう一度";

            yield return WaitForEnterKey();

            group.alpha = 0f;
        }

        IEnumerator CoCountUp(string caption, int target)
        {
            captionText.text = caption;

            if (target <= 0)
            {
                numberText.text = "0";
                yield return new WaitForSeconds(0.3f);
                yield break;
            }

            var elapsed = 0f;
            var shown = 0;
            while (shown < target)
            {
                elapsed += Time.deltaTime;
                var frac = Mathf.Clamp01(elapsed / CountUpDuration);
                shown = Mathf.Min(target, Mathf.CeilToInt(target * frac));
                numberText.text = shown.ToString();
                yield return null;
            }

            numberText.text = target.ToString();
            yield return new WaitForSeconds(HoldAfterCountUp);
        }

        static IEnumerator WaitForEnterKey()
        {
            while (true)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard[Key.Enter].wasPressedThisFrame || keyboard[Key.NumpadEnter].wasPressedThisFrame))
                {
                    yield break;
                }
                yield return null;
            }
        }
    }
}
