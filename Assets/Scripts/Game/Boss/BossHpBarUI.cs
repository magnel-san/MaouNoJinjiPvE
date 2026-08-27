using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // ボスのHPを画面上部にUIで表示する。他キャラのHpBarGauge(頭上のワールド空間リング)とは別に、
    // 常に画面上部固定で目立つように、実行時にCanvasごと自己構築する(シーン側の手動配置は不要、
    // HpBarGauge/ExplosionRingEffect等と同じ「procedural・自己完結」の方針を踏襲)。
    // このプロジェクトのUI操作(UiPointerController/HoldToActivateButton)は標準のEventSystem/
    // GraphicRaycasterを使わない独自方式のため、このCanvasも純粋な表示専用としてそれらを付けない。
    [RequireComponent(typeof(CharacterHealth))]
    public class BossHpBarUI : MonoBehaviour
    {
        [SerializeField] private string _bossName = "ボス";
        [SerializeField] private Color _fullColor = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color _backgroundColor = new Color(0.08f, 0.02f, 0.02f, 0.85f);
        [Tooltip("被弾時、フラッシュとカメラ揺れをどれだけ強くするか")]
        [SerializeField] private float _hitShakeIntensity = 0.15f;

        const float FlashDuration = 0.2f;

        CharacterHealth health;

        Canvas canvas;
        Image fillImage;
        Image flashImage;
        float flashTimer;

        void Awake()
        {
            health = GetComponent<CharacterHealth>();
            BuildUi();
        }

        void OnEnable() => health.OnHPChanged += HandleHpChanged;
        void OnDisable() => health.OnHPChanged -= HandleHpChanged;

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        void BuildUi()
        {
            var canvasGO = new GameObject("BossHpCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var barRoot = new GameObject("BarRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            barRoot.SetParent(canvas.transform, false);
            barRoot.anchorMin = new Vector2(0.5f, 1f);
            barRoot.anchorMax = new Vector2(0.5f, 1f);
            barRoot.pivot = new Vector2(0.5f, 1f);
            barRoot.anchoredPosition = new Vector2(0f, -40f);
            barRoot.sizeDelta = new Vector2(900f, 70f);

            var bgImage = barRoot.gameObject.AddComponent<Image>();
            bgImage.color = _backgroundColor;
            bgImage.raycastTarget = false;

            var nameRect = NewChildRect("Name", barRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            nameRect.anchoredPosition = new Vector2(0f, 22f);
            nameRect.sizeDelta = new Vector2(0f, 24f);
            var nameText = nameRect.gameObject.AddComponent<Text>();
            nameText.text = _bossName;
            nameText.font = VfxShaderUtil.GetDefaultFont();
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.raycastTarget = false;

            var fillAreaRect = NewChildRect("FillArea", barRoot, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.55f), Vector2.zero);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillBgImage = fillAreaRect.gameObject.AddComponent<Image>();
            fillBgImage.color = new Color(0f, 0f, 0f, 0.6f);
            fillBgImage.raycastTarget = false;

            var fillRect = NewChildRect("Fill", fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = _fullColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var flashRect = NewChildRect("Flash", fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero);
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            flashImage = flashRect.gameObject.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;
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

        void HandleHpChanged(float current, float max)
        {
            var pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fillImage.fillAmount = pct;
            fillImage.color = Color.Lerp(_lowColor, _fullColor, pct);

            flashTimer = FlashDuration;
            CameraShake.Shake(_hitShakeIntensity);
        }

        void Update()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            var c = flashImage.color;
            c.a = Mathf.Clamp01(flashTimer / FlashDuration) * 0.7f;
            flashImage.color = c;
        }
    }
}
