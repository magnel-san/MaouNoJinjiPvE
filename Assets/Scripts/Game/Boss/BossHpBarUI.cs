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

        [Header("遅れて減るゲージ設定")]
        [Tooltip("遅れて減るバーの色（赤メインゲージの後ろで見えやすい暗めのアラートカラー）")]
        [SerializeField] private Color _delayColor = new Color(0.8f, 0.4f, 0.1f, 0.9f);
        [Tooltip("ダメージ後、遅れて減り始めるまでの待機時間（秒）")]
        [SerializeField] private float _delayStartWait = 0.4f;
        [Tooltip("遅れて減るバーの減少速度")]
        [SerializeField] private float _delayDrainSpeed = 0.5f;

        [Tooltip("被弾時、フラッシュとカメラ揺れをどれだけ強くするか")]
        [SerializeField] private float _hitShakeIntensity = 0.15f;

        const float FlashDuration = 0.2f;

        CharacterHealth health;

        Canvas canvas;
        Image fillImage;
        Image delayImage;
        Image flashImage;
        Text nameText;
        Text percentText;
        float flashTimer;

        private float targetHpRatio = 1f;
        private float currentDelayRatio = 1f;
        private float delayWaitTimer = 0f;

        // 動的にAddComponentする場合(最終決戦の勇者等)、Inspectorで設定できない名前をここで上書きする。
        public void SetName(string name)
        {
            _bossName = name;
            if (nameText != null) nameText.text = name;
        }

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
            // ボス本体の子にしておく(ScreenSpaceOverlayなので見た目上の位置・回転・縮小には影響されない)。
            // 死亡演出でボスがSetActive(false)/Destroyされた際に、このキャンバスだけ取り残されて
            // 次のボスのHPバーと重複表示されるのを防ぐため。
            canvasGO.transform.SetParent(transform, false);
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
            bgImage.sprite = VfxShaderUtil.GetPanelSprite();
            bgImage.type = Image.Type.Sliced;
            bgImage.color = _backgroundColor;
            bgImage.raycastTarget = false;

            var nameRect = NewChildRect("Name", barRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            nameRect.anchoredPosition = new Vector2(0f, 22f);
            nameRect.sizeDelta = new Vector2(0f, 24f);
            nameText = nameRect.gameObject.AddComponent<Text>();
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

            // 遅れて減るゲージ（DelayFill）をメインゲージの背後に生成
            var delayRect = NewChildRect("DelayFill", fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero);
            delayRect.offsetMin = Vector2.zero;
            delayRect.offsetMax = Vector2.zero;
            delayImage = delayRect.gameObject.AddComponent<Image>();
            delayImage.sprite = VfxShaderUtil.GetGradientFillSprite();
            delayImage.color = _delayColor;
            delayImage.type = Image.Type.Filled;
            delayImage.fillMethod = Image.FillMethod.Horizontal;
            delayImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            delayImage.fillAmount = 1f;
            delayImage.raycastTarget = false;

            var fillRect = NewChildRect("Fill", fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = VfxShaderUtil.GetGradientFillSprite();
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

            var pctRect = NewChildRect("Percent", fillAreaRect, Vector2.zero, Vector2.one, Vector2.zero);
            pctRect.offsetMin = new Vector2(6f, 0f);
            pctRect.offsetMax = new Vector2(-6f, 0f);
            percentText = pctRect.gameObject.AddComponent<Text>();
            percentText.font = VfxShaderUtil.GetDefaultFont();
            percentText.fontSize = 20;
            percentText.fontStyle = FontStyle.Bold;
            percentText.alignment = TextAnchor.MiddleRight;
            percentText.color = Color.white;
            percentText.text = "100%";
            percentText.raycastTarget = false;
            var pctOutline = pctRect.gameObject.AddComponent<Outline>();
            pctOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            pctOutline.effectDistance = new Vector2(1.5f, -1.5f);
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
            
            // 回復・即時上昇時
            if (pct >= targetHpRatio)
            {
                currentDelayRatio = pct;
                if (delayImage != null) delayImage.fillAmount = currentDelayRatio;
            }
            else
            {
                // 被ダメージ時は待機タイマーを設定
                delayWaitTimer = _delayStartWait;
            }

            targetHpRatio = pct;
            fillImage.fillAmount = targetHpRatio;
            fillImage.color = Color.Lerp(_lowColor, _fullColor, targetHpRatio);
            percentText.text = $"{Mathf.RoundToInt(targetHpRatio * 100f)}%";

            flashTimer = FlashDuration;
            CameraShake.Shake(_hitShakeIntensity);
        }

        void Update()
        {
            // 1. 被弾時画面・ゲージフラッシュ処理
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                var c = flashImage.color;
                c.a = Mathf.Clamp01(flashTimer / FlashDuration) * 0.7f;
                flashImage.color = c;
            }

            // 2. 遅れて下がるゲージのアニメーション処理
            if (currentDelayRatio > targetHpRatio)
            {
                if (delayWaitTimer > 0f)
                {
                    delayWaitTimer -= Time.deltaTime;
                }
                else
                {
                    currentDelayRatio = Mathf.MoveTowards(currentDelayRatio, targetHpRatio, _delayDrainSpeed * Time.deltaTime);
                    if (delayImage != null)
                    {
                        delayImage.fillAmount = currentDelayRatio;
                    }
                }
            }
        }
    }
}
