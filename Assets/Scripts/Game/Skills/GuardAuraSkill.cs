using UnityEngine;

namespace Game
{
    // スキル「防御フィールド」。移動ロジックとは無関係に、味方全体へダメージ軽減オーラをかける。
    // 実際の軽減計算はCharacterHealth.ApplyDamage側が生存中のGuardAuraSkillを距離判定して都度行う
    // (このクラスは範囲の見た目表示のみを担当する)。移動タイプは自由に選べる(前に出る近接タンクにも、
    // 後方から味方を守る形にも使える)。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth))]
    public class GuardAuraSkill : MonoBehaviour
    {
        [Range(1, 3)] public int AuraReductionTier = 2;

        static readonly Color AuraColor = new Color(0.3f, 0.6f, 1f, 0.25f);
        static readonly Color AuraPulseColor = new Color(0.5f, 0.8f, 1f);
        static readonly Color RuneColor = new Color(0.65f, 0.9f, 1f);

        const float PulseInterval = 1.2f;
        const int RuneCount = 4;
        const float RuneOrbitSpeedDegPerSec = 35f;

        Transform auraDisc;
        Transform[] runes;
        float pulseTimer;
        float runeAngleDeg;

        public float AuraRadius
        {
            get
            {
                var cfg = GameBalanceConfig.Instance;
                return cfg != null ? cfg.StageDiameter / 4f : 6f;
            }
        }

        public float AuraReductionPercent
        {
            get
            {
                var cfg = GameBalanceConfig.Instance;
                return cfg != null ? cfg.GuardAuraReductionPercent.Get(AuraReductionTier) : 20f;
            }
        }

        void Awake()
        {
            BuildAuraDisc();
            BuildRunes();
        }

        // 常時発動の受動スキルのため、発動タイミングとして展開した瞬間に一度だけログを出す。
        void Start() => SkillActivationLogUI.Log(gameObject, "防御フィールド");

        // フィールド系の技はXZ平面に固定して表示する(キャラの向きの回転につられて傾かないように)。
        // そのためキャラのtransformには親子付けせず、Updateで位置だけを毎フレーム追従させる。
        void BuildAuraDisc()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GuardAuraDisc";
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(VfxShaderUtil.GetTransparentShader()) { color = AuraColor };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            auraDisc = go.transform;
            auraDisc.SetPositionAndRotation(transform.position + Vector3.down * 0.15f, Quaternion.identity);
        }

        // フィールドの縁を巡回する小さな光る紋章。ただの半透明の円盤だけだと動きが無く地味なため、
        // 「稼働中の結界」らしい生きた見た目にする(当たり判定は持たない、純粋な演出)。
        void BuildRunes()
        {
            runes = new Transform[RuneCount];
            for (var i = 0; i < RuneCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "GuardRune";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * 0.3f;

                var renderer = go.GetComponent<Renderer>();
                var mat = new Material(VfxShaderUtil.GetUnlitShader()) { color = RuneColor };
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", RuneColor * 1.5f);
                }
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                runes[i] = go.transform;
            }
        }

        void OnEnable()
        {
            SetVisible(true);
        }

        void OnDisable()
        {
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            if (auraDisc != null) auraDisc.gameObject.SetActive(visible);
            if (runes == null) return;
            foreach (var rune in runes)
            {
                if (rune != null) rune.gameObject.SetActive(visible);
            }
        }

        void Update()
        {
            var radius = AuraRadius;
            var center = transform.position + Vector3.down * 0.15f;
            auraDisc.SetPositionAndRotation(center, Quaternion.identity);
            auraDisc.localScale = new Vector3(radius * 2f, 0.008f, radius * 2f);

            runeAngleDeg += RuneOrbitSpeedDegPerSec * Time.deltaTime;
            var spacing = 360f / runes.Length;
            for (var i = 0; i < runes.Length; i++)
            {
                var rad = (runeAngleDeg + spacing * i) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
                runes[i].position = center + offset + Vector3.up * 0.25f;
            }

            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0f)
            {
                pulseTimer = PulseInterval;
                ExplosionRingEffect.Spawn(transform.position, radius, AuraPulseColor, 0.6f);
            }
        }

        void OnDestroy()
        {
            if (auraDisc != null) Destroy(auraDisc.gameObject);
            if (runes == null) return;
            foreach (var rune in runes)
            {
                if (rune != null) Destroy(rune.gameObject);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = AuraPulseColor;
            TargetingUtility.DrawGizmoCircle(transform.position, AuraRadius);
        }
    }
}
