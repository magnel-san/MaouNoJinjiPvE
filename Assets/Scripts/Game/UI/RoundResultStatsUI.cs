using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 勝敗画面で、キャラごとの与ダメージ(スキル込み、DamageStatsTracker参照)を多い順に一覧表示する
    // パネル。既存の_winRoot/_loseRoot(シーン側のオブジェクト)の中身には触れず、別レイヤーの
    // 新規Canvasとして重ねて表示する。BossHpBarUIと同じ自己構築Canvasパターンのシーン常駐シングルトン。
    public class RoundResultStatsUI : MonoBehaviour
    {
        const int MaxRows = 8;

        static RoundResultStatsUI _instance;

        readonly List<Text> rows = new List<Text>();
        CanvasGroup canvasGroup;
        float timer;

        public static void Show(IReadOnlyDictionary<string, float> damageByName, float displaySeconds)
        {
            EnsureExists();
            _instance.Populate(damageByName);
            _instance.timer = displaySeconds;
            _instance.canvasGroup.alpha = 1f;
        }

        static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("RoundResultStatsUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<RoundResultStatsUI>();
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("RoundResultStatsCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvasGo.AddComponent<CanvasScaler>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            panelImage.raycastTarget = false;
            var panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(0.02f, 0.2f);
            panelRect.anchorMax = new Vector2(0.28f, 0.8f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var title = BuildRowText(panelGo.transform, "Title", 26, new Color(1f, 1f, 1f));
            title.alignment = TextAnchor.UpperCenter;
            title.text = "与ダメージ";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(0f, 40f);

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(panelGo.transform, false);
            var listRoot = listGo.GetComponent<RectTransform>();
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.offsetMin = new Vector2(12f, 12f);
            listRoot.offsetMax = new Vector2(-12f, -50f);

            for (var i = 0; i < MaxRows; i++)
            {
                var rowText = BuildRowText(listRoot, $"Row{i}", 22, new Color(1f, 0.85f, 0.4f));
                var rowRect = rowText.rectTransform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -i * 28f);
                rowRect.sizeDelta = new Vector2(0f, 26f);
                rows.Add(rowText);
            }

            canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        static Text BuildRowText(Transform parent, string name, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.text = "";
            return text;
        }

        void Populate(IReadOnlyDictionary<string, float> damageByName)
        {
            var sorted = damageByName.OrderByDescending(kv => kv.Value).Take(MaxRows).ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].text = i < sorted.Count ? $"{i + 1}. {sorted[i].Key}  {Mathf.CeilToInt(sorted[i].Value)}" : "";
            }
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) canvasGroup.alpha = 0f;
        }
    }
}
