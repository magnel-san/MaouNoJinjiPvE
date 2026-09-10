using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「反射フィールド」。移動ロジックとは無関係に、被弾した瞬間(CharacterHealth.OnHPChangedで
    // HPが減ったことを検知)にクールダウン付きで自身中心の波状ダメージ+ノックバックを発生させる。
    // 以前は防御フィールド(GuardAuraSkill)とほぼ同じ水色パレット+単純な二重リングだったため、
    // 「何の技が発動したのか分かりづらい」という指摘を受け、(1)専用の紫系カラーに変更、
    // (2)発動時に「反射!」の文字を頭上に出す、(3)常時、頭上に「反射準備完了」を示す
    // ダイヤ型インジケーターを浮かべ、クールダウン中は暗く小さくして状態が一目で分かるようにした。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth))]
    public class ReflectFieldSkill : MonoBehaviour
    {
        [Range(1, 3)] public int Tier = 2;
        public float KnockbackVector = 16f;
        [Tooltip("発動のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip WaveSound;

        static readonly Color WaveColor = new Color(0.85f, 0.3f, 0.95f);
        static readonly Color WaveCoreColor = new Color(1f, 0.85f, 1f);
        static readonly Color IndicatorReadyColor = new Color(0.9f, 0.4f, 1f);
        static readonly Color IndicatorCooldownColor = new Color(0.35f, 0.2f, 0.4f);

        const float IndicatorHeight = 2.1f;
        const float IndicatorSpinDegPerSec = 90f;

        CharacterIdentity identity;
        CharacterHealth health;
        float lastKnownHp;
        float cooldownTimer;
        float maxCooldown = 1f;

        Transform indicator;
        MeshRenderer indicatorRenderer;
        MaterialPropertyBlock indicatorBlock;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            health = GetComponent<CharacterHealth>();
            BuildIndicator();
        }

        void OnEnable()
        {
            lastKnownHp = health.CurrentHP;
            health.OnHPChanged += HandleHpChanged;
        }

        void OnDisable() => health.OnHPChanged -= HandleHpChanged;

        void OnDestroy()
        {
            if (indicator != null) Destroy(indicator.gameObject);
        }

        // 頭上に浮かべる小さな菱形(キューブを45度回転させただけの、他スキルと衝突しない専用ビジュアル)。
        // 「反射準備完了(明るい紫、通常回転)」と「クールダウン中(暗く縮小、回転停止)」を
        // 見た目で区別できるようにし、防御フィールドの水色リングと混同しないよう色相を分ける。
        void BuildIndicator()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ReflectIndicator";
            Destroy(go.GetComponent<Collider>());
            indicator = go.transform;
            indicator.SetParent(transform, false);
            indicator.localPosition = Vector3.up * IndicatorHeight;
            indicator.localRotation = Quaternion.Euler(45f, 45f, 0f);
            indicator.localScale = Vector3.one * 0.22f;

            indicatorRenderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(VfxShaderUtil.GetUnlitShader()) { color = IndicatorReadyColor };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, IndicatorReadyColor * 1.5f);
            indicatorRenderer.sharedMaterial = mat;
            indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            indicatorRenderer.receiveShadows = false;
            indicatorBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

            if (indicator == null) return;

            var ready = cooldownTimer <= 0f;
            var chargeRatio = ready ? 1f : 1f - Mathf.Clamp01(cooldownTimer / maxCooldown);

            if (ready)
            {
                indicator.Rotate(Vector3.up, IndicatorSpinDegPerSec * Time.deltaTime, Space.Self);
                indicator.localScale = Vector3.one * 0.22f;
            }
            else
            {
                indicator.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.22f, chargeRatio);
            }

            var color = Color.Lerp(IndicatorCooldownColor, IndicatorReadyColor, chargeRatio);
            indicatorRenderer.GetPropertyBlock(indicatorBlock);
            indicatorBlock.SetColor(EmissionColorId, color * (ready ? 1.5f : 0.6f));
            indicatorRenderer.SetPropertyBlock(indicatorBlock);
        }

        void HandleHpChanged(float current, float max)
        {
            var damaged = current < lastKnownHp;
            lastKnownHp = current;
            if (!damaged || cooldownTimer > 0f) return;

            TriggerWave();
        }

        void TriggerWave()
        {
            var cfg = GameBalanceConfig.Instance;
            var radius = cfg != null ? cfg.ReflectFieldRadius.Get(Tier) : 3.5f;
            var damage = cfg != null ? cfg.ReflectFieldDamage.Get(Tier) : 20f;
            maxCooldown = cfg != null ? cfg.ReflectFieldCooldown.Get(Tier) : 2.3f;
            cooldownTimer = maxCooldown;

            // 被弾に即座に反応した合図として自身をパッと光らせ、頭上に「反射!」と表示、
            // 破片が弾け飛ぶような紫のバースト+波状の二重衝撃波を広げる(発動理由が
            // 一目で伝わるよう、防御フィールドとは別の色・演出にしてある)。
            CombatFx.HitFlash(transform, WaveCoreColor);
            CombatFx.ReflectPopup(transform.position);
            CombatFx.ImpactBurst(transform.position + Vector3.up, WaveColor, 0.4f);
            StartCoroutine(CoEmitWave(radius));
            SfxUtil.PlayAt(WaveSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "反射フィールド");

            var hits = Physics.OverlapSphere(transform.position, radius);
            var affected = new HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var otherIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (otherIdentity == null || otherIdentity == identity || otherIdentity.Team == identity.Team) continue;
                if (!affected.Add(otherIdentity)) continue;

                var otherHealth = otherIdentity.GetComponent<CharacterHealth>();
                if (otherHealth == null || !otherHealth.IsAlive) continue;
                otherHealth.ApplyDamage(damage, WaveColor, identity);

                var rb = otherIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var dir = otherIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    rb.AddForce(dir.normalized * KnockbackVector, ForceMode.VelocityChange);
                }
            }
        }

        // 中心から外へ2重に広がる衝撃波の見た目(すぐ小さい波が出て、少し遅れて全範囲まで広がる波が続く)。
        IEnumerator CoEmitWave(float radius)
        {
            ExplosionRingEffect.Spawn(transform.position, radius * 0.4f, WaveCoreColor, 0.3f);
            yield return new WaitForSeconds(0.08f);
            ExplosionRingEffect.Spawn(transform.position, radius, WaveColor, 0.45f);
        }

        void OnDrawGizmosSelected()
        {
            var cfg = GameBalanceConfig.Instance;
            var radius = cfg != null ? cfg.ReflectFieldRadius.Get(Tier) : 3.5f;
            Gizmos.color = WaveColor;
            TargetingUtility.DrawGizmoCircle(transform.position, radius);
        }
    }
}
