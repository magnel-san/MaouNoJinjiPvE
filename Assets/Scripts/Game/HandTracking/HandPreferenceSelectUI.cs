using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game;
using Game.UI;

namespace Game.HandTracking
{
    // 利き手(ジェスチャーのカーソル・コマンドに使う手)を選ばせる、実行時に自己構築する選択画面。
    // BossHpBarUI/UltimateGaugeUI等と同じ「procedural・自己完結」の方針。GameFlowManagerが
    // ゲーム開始直後に1度だけ表示し、選択が終わるまで待つ。既存のUiPointerController/
    // HoldToActivateButtonの仕組み(マウスデバッグ・将来的な指差しの両方に対応)をそのまま使う。
    public class HandPreferenceSelectUI : MonoBehaviour
    {
        static HandPreferenceSelectUI _instance;

        GameObject canvasRoot;
        HoldToActivateButton rightButton;
        HoldToActivateButton leftButton;

        public static HandPreferenceSelectUI EnsureExists()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("HandPreferenceSelectUI");
            _instance = go.AddComponent<HandPreferenceSelectUI>();
            return _instance;
        }

        void Awake()
        {
            BuildUi();
            canvasRoot.SetActive(false);
        }

        // 表示→どちらかのボタンが確定発火するまで待つ→選択をHandPreferenceへ反映→非表示、を行う。
        public IEnumerator WaitForSelection()
        {
            var chosen = false;
            void HandleRight() { HandPreference.Choose(true); chosen = true; }
            void HandleLeft() { HandPreference.Choose(false); chosen = true; }

            rightButton.OnTriggered += HandleRight;
            leftButton.OnTriggered += HandleLeft;

            canvasRoot.SetActive(true);
            yield return new WaitUntil(() => chosen);

            rightButton.OnTriggered -= HandleRight;
            leftButton.OnTriggered -= HandleLeft;
            canvasRoot.SetActive(false);
        }

        void BuildUi()
        {
            canvasRoot = new GameObject("HandPreferenceCanvas");
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var bg = NewChildRect("Background", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.75f);
            bgImage.raycastTarget = false;

            var titleRect = NewChildRect("Title", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            titleRect.anchoredPosition = new Vector2(0f, 160f);
            titleRect.sizeDelta = new Vector2(900f, 80f);
            var titleText = titleRect.gameObject.AddComponent<Text>();
            titleText.text = "利き手を選択してください";
            titleText.font = VfxShaderUtil.GetDefaultFont();
            titleText.fontSize = 40;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.raycastTarget = false;

            rightButton = BuildChoiceButton(canvas.transform, "右手", new Vector2(-220f, -20f));
            leftButton = BuildChoiceButton(canvas.transform, "左手", new Vector2(220f, -20f));
        }

        HoldToActivateButton BuildChoiceButton(Transform parent, string label, Vector2 anchoredPos)
        {
            var root = NewChildRect(label + "Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            root.anchoredPosition = anchoredPos;
            root.sizeDelta = new Vector2(180f, 180f);

            var bgImage = root.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.18f, 0.25f, 0.95f);
            bgImage.raycastTarget = false;

            var gaugeRect = NewChildRect("Gauge", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            gaugeRect.offsetMin = Vector2.zero;
            gaugeRect.offsetMax = Vector2.zero;
            var gaugeImage = gaugeRect.gameObject.AddComponent<Image>();
            gaugeImage.color = new Color(0.3f, 0.9f, 1f, 0.85f);
            gaugeImage.type = Image.Type.Filled;
            gaugeImage.fillMethod = Image.FillMethod.Radial360;
            gaugeImage.fillOrigin = (int)Image.Origin360.Top;
            gaugeImage.fillAmount = 0f;
            gaugeImage.raycastTarget = false;

            var labelRect = NewChildRect("Label", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelRect.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = VfxShaderUtil.GetDefaultFont();
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var button = root.gameObject.AddComponent<HoldToActivateButton>();
            button.HoldSeconds = 1.2f;
            button.SetGaugeImage(gaugeImage);
            return button;
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
    }
}
