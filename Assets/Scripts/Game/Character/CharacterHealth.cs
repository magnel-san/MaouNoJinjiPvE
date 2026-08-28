using System;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(CharacterStats), typeof(CharacterIdentity))]
    public class CharacterHealth : MonoBehaviour
    {
        public event Action<float, float> OnHPChanged;
        public event Action OnDied;

        [HideInInspector] public float DamageReductionPercent = 0f;

        [Tooltip("落下死のY座標に到達後、この秒数その場に留まると消滅する")]
        public float FallDeathDespawnDelay = 3f;

        [Header("効果音 (未設定なら無音)")]
        [Tooltip("被弾のたびに鳴らす効果音")]
        public AudioClip HitSound;
        [Tooltip("撃破された瞬間に鳴らす効果音")]
        public AudioClip DeathSound;

        [Header("死亡時ゴースト表現設定")]
        [Tooltip("死亡した際の半透明度 (0.0 = 完全透明, 1.0 = 不透明)")]
        [Range(0f, 1f)]
        [SerializeField] private float deathGhostAlpha = 0.8f;

        public float CurrentHP { get; private set; }
        public bool IsAlive { get; private set; } = true;

        CharacterStats stats;
        CharacterIdentity identity;
        Rigidbody rb;
        MapBounds mapBounds;
        float belowFallDeathTimer;
        bool hasFallenPastDeathLine;

        void Awake()
        {
            stats = GetComponent<CharacterStats>();
            identity = GetComponent<CharacterIdentity>();
            rb = GetComponent<Rigidbody>();
        }

        void Start()
        {
            CurrentHP = stats.MaxHP;
            mapBounds = FindAnyObjectByType<MapBounds>();
            OnHPChanged?.Invoke(CurrentHP, stats.MaxHP);
        }

        void Update()
        {
            if (mapBounds == null) return;

            if (transform.position.y <= mapBounds.FallDeathY)
            {
                if (!hasFallenPastDeathLine)
                {
                    hasFallenPastDeathLine = true;
                    if (IsAlive) Kill();
                }
                belowFallDeathTimer += Time.deltaTime;
                if (belowFallDeathTimer >= FallDeathDespawnDelay)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                hasFallenPastDeathLine = false;
                belowFallDeathTimer = 0f;
            }
        }

        public void ApplyDamage(float rawDamage) => ApplyDamage(rawDamage, CombatFx.DefaultDamageColor);

        public void ApplyDamage(float rawDamage, Color fxColor)
        {
            if (!IsAlive || rawDamage <= 0f) return;

            float finalDamage = rawDamage * (1f - Mathf.Clamp01(DamageReductionPercent / 100f));
            CurrentHP = Mathf.Max(0f, CurrentHP - finalDamage);
            OnHPChanged?.Invoke(CurrentHP, stats.MaxHP);

            CombatFx.HitFlash(transform, fxColor);
            CombatFx.DamagePopup(transform.position, finalDamage, fxColor);
            CombatFx.ImpactBurst(transform.position + Vector3.up, fxColor);
            SfxUtil.PlayAt(HitSound, transform.position);

            if (CurrentHP <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            CurrentHP = Mathf.Min(stats.MaxHP, CurrentHP + amount);
            OnHPChanged?.Invoke(CurrentHP, stats.MaxHP);
        }

        public void Kill()
        {
            if (!IsAlive) return;
            CurrentHP = 0f;
            OnHPChanged?.Invoke(CurrentHP, stats.MaxHP);
            Die();
        }

        void Die()
        {
            if (!IsAlive) return;
            IsAlive = false;
            identity.IsAlive = false;

            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == this) continue;
                if (behaviour is CharacterMovement || behaviour is CharacterPosture || behaviour is IMovementIntentSource
                    || behaviour is BoundaryAvoidance || behaviour is CharacterChargeAssist || behaviour is BossController)
                {
                    behaviour.enabled = false;
                }
            }

            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.speed = 0f;

            // コライダーを非有効化して、死体に判定が残らない（引っかからない）ようにする
            if (TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;
            }

            CombatFx.DeathBurst(transform.position + Vector3.up * 0.5f, CombatFx.DefaultDamageColor);
            SfxUtil.PlayAt(DeathSound, transform.position);

            // --- 追記：死亡時の半透明表現 ---
            ApplyGhostAlpha(deathGhostAlpha);

                // ★ 追記：死亡カットイン演出の再生呼び出し
                if (DeathCutinManager.Instance != null)
                {
                    DeathCutinManager.Instance.PlayDeathCutin();
                }

            OnDied?.Invoke();
        }

        // 見た目を半透明にする処理
        private void ApplyGhostAlpha(float alpha)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                foreach (var mat in rend.materials)
                {
                    // URP/HDRPやStandardシェーダーで透明度変更を有効化する設定
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.color;
                        c.a = alpha;
                        mat.color = c;
                    }
                    else if (mat.HasProperty("_BaseColor")) // URPシェーダー等の場合
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }

                    // シェーダーのレンダーモードをTransparent(半透明)に切り替える（Standardシェーダー用補正）
                    mat.SetFloat("_Mode", 3f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
        }
    }
}
