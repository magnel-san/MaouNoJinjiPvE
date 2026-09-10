using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 画面右上に、モンスターが技を発動するたびに「キャラ名 の 技名！！」というログを1行積み上げる。
    // 一定時間表示された後フェードアウトして消え、消えた行の分だけ残りの行が上へ詰める
    // (常に先頭=一番古い行から消えるキュー形式。新しい行は一覧の末尾(下)に追加される)。
    // ScoreUI等と同じ自己構築Canvas+EnsureExists()シングルトンパターン。
    public class SkillActivationLogUI : MonoBehaviour
    {
        const float EntryWidth = 360f;
        const float EntryHeight = 32f;
        const float EntrySpacing = 4f;
        const float DisplayDuration = 3f;
        const float FadeOutDuration = 0.5f;
        const int MaxVisible = 8;

        static readonly Color EntryColor = new Color(1f, 0.9f, 0.4f);
        static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.6f);

        class Entry
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Remaining;
        }

        static SkillActivationLogUI _instance;

        RectTransform listRoot;
        readonly List<Entry> entries = new List<Entry>();

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("SkillActivationLogUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillActivationLogUI>();
        }

        // 技を発動したキャラのGameObjectと技名を渡すと、「{characterName} の {skillName}！！」の
        // 形式でログに積む(名前は"(Clone)"サフィックスを取り除いて表示する、DamageStatsTrackerと同じ方針)。
        public static void Log(GameObject caster, string skillName)
        {
            EnsureExists();
            var name = caster != null ? CleanName(caster.name) : "?";
            _instance.AddEntry($"{name} の {skillName}！！");
        }

        static string CleanName(string name)
        {
            const string cloneSuffix = "(Clone)";
            const string variantSuffix = "Variant";

            if (name.EndsWith(cloneSuffix)) name = name.Substring(0, name.Length - cloneSuffix.Length).TrimEnd();
            if (name.EndsWith(variantSuffix)) name = name.Substring(0, name.Length - variantSuffix.Length).TrimEnd();

            return name;
        }

        void Awake() => BuildUi();

        void BuildUi()
        {
            var canvasGo = new GameObject("SkillActivationLogCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var rootGo = new GameObject("ListRoot", typeof(RectTransform));
            listRoot = rootGo.GetComponent<RectTransform>();
            listRoot.SetParent(canvasGo.transform, false);
            listRoot.anchorMin = new Vector2(1f, 1f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.pivot = new Vector2(1f, 1f);
            listRoot.anchoredPosition = new Vector2(-20f, -20f);
        }

        void AddEntry(string text)
        {
            if (entries.Count >= MaxVisible)
            {
                Destroy(entries[0].Rect.gameObject);
                entries.RemoveAt(0);
            }

            var rowRect = new GameObject("LogEntry", typeof(RectTransform)).GetComponent<RectTransform>();
            rowRect.SetParent(listRoot, false);
            rowRect.anchorMin = new Vector2(1f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(1f, 1f);
            rowRect.sizeDelta = new Vector2(EntryWidth, EntryHeight);

            var group = rowRect.gameObject.AddComponent<CanvasGroup>();

            var bg = rowRect.gameObject.AddComponent<Image>();
            bg.sprite = VfxShaderUtil.GetPanelSprite();
            bg.type = Image.Type.Sliced;
            bg.color = BackgroundColor;
            bg.raycastTarget = false;

            var textRect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            textRect.SetParent(rowRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(-10f, -2f);

            var textComp = textRect.gameObject.AddComponent<Text>();
            textComp.font = VfxShaderUtil.GetDefaultFont();
            textComp.fontSize = 20;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = TextAnchor.MiddleRight;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.color = EntryColor;
            textComp.text = text;
            textComp.raycastTarget = false;

            var outline = textRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            entries.Add(new Entry { Rect = rowRect, Group = group, Remaining = DisplayDuration });
            Reposition();
        }

        void Update()
        {
            var changed = false;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                e.Remaining -= Time.deltaTime;

                if (e.Remaining <= FadeOutDuration)
                {
                    e.Group.alpha = Mathf.Clamp01(e.Remaining / FadeOutDuration);
                }

                if (e.Remaining <= 0f)
                {
                    Destroy(e.Rect.gameObject);
                    entries.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed) Reposition();
        }

        void Reposition()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i].Rect.anchoredPosition = new Vector2(0f, -(EntryHeight + EntrySpacing) * i);
            }
        }
    }
}
