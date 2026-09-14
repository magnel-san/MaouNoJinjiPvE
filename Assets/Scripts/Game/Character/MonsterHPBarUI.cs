using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// モンスターの頭上にHPバーを追従表示し、遅れて下がる赤ゲージ演出を提供するUIスクリプト
    /// </summary>
    public class MonsterHPBarUI : MonoBehaviour
    {
        [Header("参照設定")]
        [Tooltip("追従対象のCharacterHealth（未設定の場合は親オブジェクトから自動取得）")]
        [SerializeField] private CharacterHealth targetHealth;

        [Tooltip("メインのHPゲージ画像 (Image Type: Filled)")]
        [SerializeField] private Image mainHpImage;

        [Tooltip("遅れて下がる赤ゲージ画像 (Image Type: Filled)")]
        [SerializeField] private Image redHpImage;

        [Header("位置設定")]
        [Tooltip("ターゲットからのオフセット位置（頭上の高さ等）")]
        private Vector3 offset = new Vector3(0f, 4.0f, 0f);

        [Header("赤ゲージアニメーション設定")]
        [Tooltip("ダメージを受けてから赤ゲージが下がり始めるまでの待機時間（秒）")]
        [SerializeField] private float redGaugeDelay = 0.5f;

        [Tooltip("赤ゲージが減算追従する速度")]
        [SerializeField] private float redGaugeSpeed = 0.5f;

        private Camera mainCamera;
        private float targetHpRatio = 1f;
        private float currentRedRatio = 1f;
        private float delayTimer = 0f;
        private bool isTracking = true;

        void Awake()
        {
            if (targetHealth == null)
            {
                targetHealth = GetComponentInParent<CharacterHealth>();
            }
        }

        void OnEnable()
        {
            if (targetHealth != null)
            {
                targetHealth.OnHPChanged += HandleHPChanged;
                targetHealth.OnDied += HandleDied;
            }
        }

        void OnDisable()
        {
            if (targetHealth != null)
            {
                targetHealth.OnHPChanged -= HandleHPChanged;
                targetHealth.OnDied -= HandleDied;
            }
        }

        void Start()
        {
            mainCamera = Camera.main;

            if (targetHealth != null && targetHealth.CurrentHP > 0)
            {
                // 初期表示の設定
                var stats = targetHealth.GetComponent<CharacterStats>();
                float maxHp = stats != null ? stats.MaxHP : targetHealth.CurrentHP;
                targetHpRatio = Mathf.Clamp01(targetHealth.CurrentHP / maxHp);
                currentRedRatio = targetHpRatio;

                UpdateGaugeImages();
            }
        }

        void LateUpdate()
        {
            if (!isTracking || targetHealth == null) return;

            // --- 1. 頭上追従とビルボード処理 ---
            transform.position = targetHealth.transform.position + offset;

            if (mainCamera != null)
            {
                // カメラの方向を向かせる
                transform.rotation = mainCamera.transform.rotation;
            }

            // --- 2. 遅れて下がる赤ゲージの更新 ---
            if (currentRedRatio > targetHpRatio)
            {
                if (delayTimer > 0f)
                {
                    delayTimer -= Time.deltaTime;
                }
                else
                {
                    currentRedRatio = Mathf.MoveTowards(currentRedRatio, targetHpRatio, redGaugeSpeed * Time.deltaTime);
                    if (redHpImage != null)
                    {
                        redHpImage.fillAmount = currentRedRatio;
                    }
                }
            }
        }

        private void HandleHPChanged(float currentHP, float maxHP)
        {
            if (maxHP <= 0f) return;

            float newRatio = Mathf.Clamp01(currentHP / maxHP);

            // 回復時またはHPが増えた場合、赤ゲージも即時同期
            if (newRatio >= targetHpRatio)
            {
                currentRedRatio = newRatio;
                if (redHpImage != null) redHpImage.fillAmount = currentRedRatio;
            }
            else
            {
                // ダメージ時はタイマーをリセットして遅れて減らす
                delayTimer = redGaugeDelay;
            }

            targetHpRatio = newRatio;

            if (mainHpImage != null)
            {
                mainHpImage.fillAmount = targetHpRatio;
            }
        }

        private void HandleDied()
        {
            isTracking = false;
            // 非表示にする場合
            gameObject.SetActive(false);
        }

        private void UpdateGaugeImages()
        {
            if (mainHpImage != null) mainHpImage.fillAmount = targetHpRatio;
            if (redHpImage != null) redHpImage.fillAmount = currentRedRatio;
        }
    }
}