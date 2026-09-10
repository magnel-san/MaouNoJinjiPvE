using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 戦闘中、画面左側に味方全員の名前+HPバーを縦に並べて常時表示する。BossHpBarUI([BossHpBarUI.cs])と
    // 同じ「自己構築Canvas、CharacterHealth.OnHPChangedを購読してバーを更新する」方式を踏襲する
    // (Updateで毎フレームCurrentHPを読みに行くポーリング方式ではなく、変化イベント駆動にすることで
    // 表示が確実に最新のHPへ追従するようにしてある)。
    // 死亡したキャラも、そのGameObjectが実際に破棄される(次ラウンドのRespawnAllPlayerCharacters等)
    // までは0HPのまま一覧に残す(誰が倒れたか分かりやすくするため)。
    public class PartyHpListUI : MonoBehaviour
    {
        const float RowWidth = 260f;
        const float RowHeight = 44f;
        const float RowSpacing = 8f;

        static readonly Color FullColor = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color LowColor = new Color(0.9f, 0.25f, 0.2f);
        static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.75f);

        class Row
        {
            public CharacterHealth Health;
            public RectTransform Rect;
            public Image FillImage;
            public Text NameText;
            public Text PercentText;
            public System.Action<float, float> HpChangedHandler;
        }

        static PartyHpListUI _instance;

        Canvas canvas;
        RectTransform listRoot;
        readonly Dictionary<CharacterHealth, Row> rows = new Dictionary<CharacterHealth, Row>();
        readonly List<CharacterHealth> currentOrder = new List<CharacterHealth>();
        readonly List<CharacterHealth> removeBuffer = new List<CharacterHealth>();

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("PartyHpListUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<PartyHpListUI>();
        }

        // ラウンド結果画面(RoundResultStatsUI)やゲームクリア画面等、同じ左側の領域に重なる
        // パネルを表示する間だけ一時的に隠す用。次ラウンドのキャラ配置後にShowで戻す。
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
            var canvasGo = new GameObject("PartyHpListCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var rootGo = new GameObject("ListRoot", typeof(RectTransform));
            listRoot = rootGo.GetComponent<RectTransform>();
            listRoot.SetParent(canvasGo.transform, false);
            listRoot.anchorMin = new Vector2(0f, 1f);
            listRoot.anchorMax = new Vector2(0f, 1f);
            listRoot.pivot = new Vector2(0f, 1f);
            // ScoreUI(0.02, 0.9)と被らないよう、十分下、左端に配置する。
            listRoot.anchoredPosition = new Vector2(16f, -200f);
        }

        void Update()
        {
            currentOrder.Clear();
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team != Team.Player) continue;
                var health = c.GetComponent<CharacterHealth>();
                if (health != null) currentOrder.Add(health);
            }

            // いなくなった(GameObjectが破棄された)キャラの行を取り除く。
            removeBuffer.Clear();
            foreach (var kv in rows)
            {
                if (kv.Key == null || !currentOrder.Contains(kv.Key)) removeBuffer.Add(kv.Key);
            }
            foreach (var key in removeBuffer) RemoveRow(key);

            // 新規キャラの行を追加する。
            foreach (var health in currentOrder)
            {
                if (!rows.ContainsKey(health)) AddRow(health);
            }

            // 表示順(currentOrderの並び)に合わせて位置を揃え直す。
            for (var i = 0; i < currentOrder.Count; i++)
            {
                if (rows.TryGetValue(currentOrder[i], out var row))
                {
                    row.Rect.anchoredPosition = new Vector2(0f, -(RowHeight + RowSpacing) * i);
                }
            }
        }

        void AddRow(CharacterHealth health)
        {
            var rowRect = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            rowRect.SetParent(listRoot, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(RowWidth, RowHeight);

            var bg = rowRect.gameObject.AddComponent<Image>();
            bg.sprite = VfxShaderUtil.GetPanelSprite();
            bg.type = Image.Type.Sliced;
            bg.color = BackgroundColor;
            bg.raycastTarget = false;

            var nameRect = NewChildRect("Name", rowRect, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            nameRect.anchoredPosition = new Vector2(8f, -4f);
            nameRect.sizeDelta = new Vector2(-16f, 16f);
            var nameText = nameRect.gameObject.AddComponent<Text>();
            nameText.font = VfxShaderUtil.GetDefaultFont();
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.color = Color.white;
            nameText.text = CleanName(health.gameObject.name);
            nameText.raycastTarget = false;
            var outline = nameRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var fillBgRect = NewChildRect("FillBg", rowRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            fillBgRect.anchoredPosition = new Vector2(8f, 4f);
            fillBgRect.sizeDelta = new Vector2(-16f, 12f);
            var fillBg = fillBgRect.gameObject.AddComponent<Image>();
            fillBg.color = new Color(0f, 0f, 0f, 0.6f);
            fillBg.raycastTarget = false;

            var fillRect = NewChildRect("Fill", fillBgRect, Vector2.zero, Vector2.one, Vector2.zero);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = VfxShaderUtil.GetGradientFillSprite();
            fillImage.color = FullColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var pctRect = NewChildRect("Percent", fillBgRect, Vector2.zero, Vector2.one, Vector2.zero);
            pctRect.offsetMin = new Vector2(4f, 0f);
            pctRect.offsetMax = new Vector2(-4f, 0f);
            var pctText = pctRect.gameObject.AddComponent<Text>();
            pctText.font = VfxShaderUtil.GetDefaultFont();
            pctText.fontSize = 10;
            pctText.fontStyle = FontStyle.Bold;
            pctText.alignment = TextAnchor.MiddleRight;
            pctText.horizontalOverflow = HorizontalWrapMode.Overflow;
            pctText.color = Color.white;
            pctText.raycastTarget = false;
            var pctOutline = pctRect.gameObject.AddComponent<Outline>();
            pctOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            pctOutline.effectDistance = new Vector2(1f, -1f);

            var row = new Row { Health = health, Rect = rowRect, FillImage = fillImage, NameText = nameText, PercentText = pctText };

            var stats = health.GetComponent<CharacterStats>();
            void OnHpChanged(float current, float max)
            {
                var m = max > 0f ? max : (stats != null ? stats.MaxHP : 1f);
                var pct = m > 0f ? Mathf.Clamp01(current / m) : 0f;
                fillImage.fillAmount = pct;
                fillImage.color = Color.Lerp(LowColor, FullColor, pct);
                pctText.text = $"{Mathf.RoundToInt(pct * 100f)}%";
            }
            row.HpChangedHandler = OnHpChanged;
            health.OnHPChanged += row.HpChangedHandler;

            // 登録直後の初期値を即座に反映する(登録タイミング次第でOnHPChangedの初回通知を
            // 取りこぼしていても、表示が満タンのまま固まらないようにする)。
            var initialMax = stats != null ? stats.MaxHP : 1f;
            OnHpChanged(health.CurrentHP, initialMax);

            rows[health] = row;
        }

        void RemoveRow(CharacterHealth health)
        {
            if (!rows.TryGetValue(health, out var row)) return;
            if (health != null) health.OnHPChanged -= row.HpChangedHandler;
            if (row.Rect != null) Destroy(row.Rect.gameObject);
            rows.Remove(health);
        }

        static RectTransform NewChildRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            return rect;
        }

        static string CleanName(string name)
        {
            const string cloneSuffix = "(Clone)";
            const string variantSuffix = "Variant";

            if (name.EndsWith(cloneSuffix)) name = name.Substring(0, name.Length - cloneSuffix.Length).TrimEnd();
            if (name.EndsWith(variantSuffix)) name = name.Substring(0, name.Length - variantSuffix.Length).TrimEnd();

            return name;
        }
    }
}
