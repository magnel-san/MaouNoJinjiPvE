using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 必殺ゲージを画面下部に表示する。BossHpBarUIと同じく実行時にCanvasごと自己構築し、
    // 標準のEventSystem/GraphicRaycasterには依存しない純粋表示用のCanvasにする。
    [RequireComponent(typeof(UltimateGaugeController))]
    public class UltimateGaugeUI : MonoBehaviour
    {
        [SerializeField] private Color _fillingColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color _readyColor = new Color(1f, 0.85f, 0.2f);

        UltimateGaugeController controller;
        Canvas canvas;
        Image fillImage;
        Text label;
        float pulseTimer;

        void Awake()
        {
            controller = GetComponent<UltimateGaugeController>();
            BuildUi();
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        void BuildUi()
        {
            var canvasGO = new GameObject("UltimateGaugeCanvas");
            // BattleInput(このコンポーネントが乗っているGameObject)の子にする。親を付けないと
            // このGameObjectがSetActive(false)されても表示専用のCanvasだけ画面に残り続けてしまう
            // (戦闘フェーズ外でもゲージが表示されっぱなしになるバグだった)。
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 190;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var root = NewChildRect("BarRoot", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            root.anchoredPosition = new Vector2(0f, 40f);
            root.sizeDelta = new Vector2(500f, 50f);

            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = VfxShaderUtil.GetPanelSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            var fillRect = NewChildRect("Fill", root, Vector2.zero, Vector2.one, Vector2.zero);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = VfxShaderUtil.GetGradientFillSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = _fillingColor;
            fillImage.raycastTarget = false;

            var labelRect = NewChildRect("Label", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label = labelRect.gameObject.AddComponent<Text>();
            label.font = VfxShaderUtil.GetDefaultFont();
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "必殺 0%";
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

        void Update()
        {
            fillImage.fillAmount = controller.GaugeFraction;

            if (controller.IsBoostActive)
            {
                fillImage.color = _readyColor;
                label.text = "必殺技 発動中!";
            }
            else if (controller.IsReady)
            {
                pulseTimer += Time.deltaTime;
                fillImage.color = Color.Lerp(_fillingColor, _readyColor, 0.5f + 0.5f * Mathf.Sin(pulseTimer * 6f));
                label.text = "6キーで必殺技!";
            }
            else
            {
                fillImage.color = _fillingColor;
                label.text = $"必殺 {Mathf.FloorToInt(controller.GaugeFraction * 100f)}%";
            }
        }
    }
}
