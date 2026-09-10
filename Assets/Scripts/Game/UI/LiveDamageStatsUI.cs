using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 戦闘中、画面右側にキャラごとの与ダメージ(DamageStatsTracker参照)を常時表示するパネル。
    // 見た目・並び順のロジックはRoundResultStatsUI([RoundResultStatsUI.cs])とほぼ同じだが、
    // ラウンド終了時だけでなく戦闘中ずっと更新し続ける点が異なる。SkillActivationLogUIの
    // ログ一覧(右上、最大8行)の下に来るよう配置してある。
    public class LiveDamageStatsUI : MonoBehaviour
    {
        const int MaxRows = 6;
        const float RefreshInterval = 0.25f;

        static LiveDamageStatsUI _instance;

        Canvas canvas;
        readonly System.Collections.Generic.List<Text> rows = new System.Collections.Generic.List<Text>();
        float refreshTimer;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("LiveDamageStatsUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<LiveDamageStatsUI>();
        }

        // ラウンド結果画面・ゲームクリア画面等、同じ情報をより大きく表示する演出と重ならないよう隠す用。
        public static void Hide()
        {
            EnsureExists();
            _instance.canvas.enabled = false;
        }

        public static void Show()
        {
            EnsureExists();
            _instance.canvas.enabled = true;
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("LiveDamageStatsCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.sprite = VfxShaderUtil.GetPanelSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            panelImage.raycastTarget = false;
            var panelRect = panelImage.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            // SkillActivationLogUI(右上、最大8行分)の下に来る位置。
            panelRect.anchoredPosition = new Vector2(-20f, -340f);
            panelRect.sizeDelta = new Vector2(320f, 190f);

            var title = BuildRowText(panelGo.transform, "Title", 20, new Color(1f, 1f, 1f));
            title.alignment = TextAnchor.UpperCenter;
            title.text = "与ダメージ";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(0f, 28f);

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(panelGo.transform, false);
            var listRoot = listGo.GetComponent<RectTransform>();
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.offsetMin = new Vector2(12f, 8f);
            listRoot.offsetMax = new Vector2(-12f, -36f);

            for (var i = 0; i < MaxRows; i++)
            {
                var rowText = BuildRowText(listRoot, $"Row{i}", 18, new Color(1f, 0.85f, 0.4f));
                var rowRect = rowText.rectTransform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -i * 24f);
                rowRect.sizeDelta = new Vector2(0f, 22f);
                rows.Add(rowText);
            }
        }

        static Text BuildRowText(Transform parent, string name, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = VfxShaderUtil.GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = "";
            return text;
        }

        void Update()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = RefreshInterval;

            var sorted = DamageStatsTracker.Snapshot.OrderByDescending(kv => kv.Value).Take(MaxRows).ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].text = i < sorted.Count ? $"{i + 1}. {sorted[i].Key}  {Mathf.CeilToInt(sorted[i].Value)}" : "";
            }
        }
    }
}
