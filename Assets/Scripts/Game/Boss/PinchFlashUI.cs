using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 勇者の特定攻撃（RectTelegraphZone等）が命中した際に、
    // 画面全体に一時的な赤いフラッシュを発生させる画面演出クラス。
    // シーン内に存在しない場合は自動で生成され、CanvasやImageもコード上で完結して構築されます。
    public class PinchFlashUI : MonoBehaviour
    {
        private static PinchFlashUI _instance;

        [Header("フラッシュ演出設定")]
        [Tooltip("フラッシュ時の最大不透明度と色")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.1f, 0.1f, 0.5f);
        [Tooltip("フラッシュが消える速度")]
        [SerializeField] private float _fadeSpeed = 1f;

        private Canvas _canvas;
        private Image _flashImage;
        private float _currentAlpha = 0f;

        public static PinchFlashUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PinchFlashUI");
                    _instance = go.AddComponent<PinchFlashUI>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUi();
        }

        // 外部（RectTelegraphZoneなど）から呼び出して画面を赤く光らせる静的メソッド
        public static void TriggerFlash()
        {
            Instance.StartFlash();
        }

        public void StartFlash()
        {
            _currentAlpha = _flashColor.a;
        }

        void BuildUi()
        {
            var canvasGO = new GameObject("PinchFlashCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500; // 最前面に表示

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panelGO = new GameObject("FlashPanel", typeof(RectTransform));
            panelGO.transform.SetParent(_canvas.transform, false);

            var rect = panelGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _flashImage = panelGO.AddComponent<Image>();
            _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);
            _flashImage.raycastTarget = false; // 操作を邪魔しない
        }

        void Update()
        {
            if (_flashImage == null) return;

            if (_currentAlpha > 0f)
            {
                _currentAlpha -= Time.deltaTime * _fadeSpeed;
                _currentAlpha = Mathf.Max(0f, _currentAlpha);

                Color c = _flashColor;
                c.a = _currentAlpha;
                _flashImage.color = c;
            }
        }
    }
}
