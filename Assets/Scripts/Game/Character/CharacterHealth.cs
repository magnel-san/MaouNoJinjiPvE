using System;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(CharacterStats), typeof(CharacterIdentity))]
    public class CharacterHealth : MonoBehaviour
    {
        public event Action<float, float> OnHPChanged;
        public event Action OnDied;

        // Aタイプ(直進型)の軽減率などが書き込む。既定0。
        [HideInInspector] public float DamageReductionPercent = 0f;

        [Tooltip("落下死のY座標に到達後、この秒数その場に留まると消滅する")]
        public float FallDeathDespawnDelay = 3f;

        [Header("効果音 (未設定なら無音)")]
        [Tooltip("被弾のたびに鳴らす効果音")]
        public AudioClip HitSound;
        [Tooltip("撃破された瞬間に鳴らす効果音")]
        public AudioClip DeathSound;

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

        // fxColorは被弾演出(ヒットフラッシュ・ダメージ数値・弾け)の色。攻撃種別ごとに使い分けたい場合はこちらを呼ぶ。
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

        // 軽減率を無視して即死させる (落下死など)。
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

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Ghost配下にAnimatorがあれば、そのポーズのままアニメーションを止める。
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.speed = 0f;

            CombatFx.DeathBurst(transform.position + Vector3.up * 0.5f, CombatFx.DefaultDamageColor);
            SfxUtil.PlayAt(DeathSound, transform.position);

            OnDied?.Invoke();
        }
    }
}
