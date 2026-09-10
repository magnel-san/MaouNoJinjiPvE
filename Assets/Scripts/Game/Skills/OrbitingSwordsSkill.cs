using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // スキル「剣召喚」。移動ロジックとは無関係に、周囲を回転する剣(1〜3本)を一定時間だけ自身に
    // 付与し、時間が切れたらクールダウンを挟んで再度付与するのを繰り返す。剣本体は[OrbitingSword]を流用する。
    [RequireComponent(typeof(CharacterIdentity))]
    public class OrbitingSwordsSkill : MonoBehaviour
    {
        [Header("剣の本数・持続時間")]
        [Range(1, 3)] public int SwordCount = 1;
        [Tooltip("持続時間は3段階でGameBalanceConfigを参照する")]
        [Range(1, 3)] public int SwordDurationTier = 2;
        [Tooltip("剣が消えてから再び付与されるまでの秒数")]
        public float Cooldown = 8f;

        [Header("剣の威力 (弱めの数値を想定)")]
        public float SwordDamage = 3f;
        public float SwordInvincibilityTime = 0.5f;

        [Header("公転設定")]
        public float OrbitRadius = 1.5f;
        public float OrbitHeight = 1f;
        public float OrbitSpeed = 220f;
        public GameObject SwordPrefab;
        [Tooltip("剣を呼び出すたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip SummonSound;

        static readonly Color SwordGlowColor = new Color(0.6f, 0.75f, 1f);

        CharacterIdentity identity;
        readonly List<OrbitingSword> activeSwords = new List<OrbitingSword>();
        bool swordsActive;
        float stateTimer;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            SpawnSwords();
        }

        void Update()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            if (swordsActive) DespawnSwords();
            else SpawnSwords();
        }

        void SpawnSwords()
        {
            swordsActive = true;
            var cfg = GameBalanceConfig.Instance;
            stateTimer = cfg != null ? cfg.SwordDuration.Get(SwordDurationTier) : 3f;
            SfxUtil.PlayAt(SummonSound, transform.position);
            SkillActivationLogUI.Log(gameObject, "回転ブレード");

            // 召喚の瞬間を分かりやすくする閃光+リング。
            CombatFx.ImpactBurst(transform.position + Vector3.up, SwordGlowColor, 0.4f);
            ExplosionRingEffect.Spawn(transform.position, OrbitRadius * 1.3f, SwordGlowColor, 0.35f);

            for (int i = 0; i < SwordCount; i++)
            {
                GameObject swordGO = SwordPrefab != null
                    ? Instantiate(SwordPrefab)
                    : CreateFallbackSwordVisual();

                var orbit = swordGO.AddComponent<OrbitingSword>();
                float angleOffset = (360f / SwordCount) * i;
                orbit.Initialize(transform, identity, SwordDamage, SwordInvincibilityTime, OrbitRadius, OrbitHeight, OrbitSpeed, angleOffset);
                activeSwords.Add(orbit);
            }
        }

        static GameObject CreateFallbackSwordVisual()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Sword";
            go.transform.localScale = new Vector3(0.1f, 0.1f, 0.7f);

            // 光る魔法剣らしく発光マテリアルを付ける(無地の灰色キューブのままだと味気ないため)。
            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(VfxShaderUtil.GetUnlitShader()) { color = SwordGlowColor };
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", SwordGlowColor * 1.4f);
            }
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // 小さな鍔(つば)を付けて剣らしいシルエットにする。
            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "Guard";
            guard.transform.SetParent(go.transform, false);
            guard.transform.localScale = new Vector3(2.2f, 0.6f, 0.15f);
            guard.transform.localPosition = new Vector3(0f, 0f, -0.42f);
            Object.Destroy(guard.GetComponent<Collider>());
            guard.GetComponent<Renderer>().sharedMaterial = mat;

            return go;
        }

        void DespawnSwords()
        {
            foreach (var sword in activeSwords)
            {
                if (sword != null) Destroy(sword.gameObject);
            }
            activeSwords.Clear();
            swordsActive = false;
            stateTimer = Cooldown;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.7f, 1f);
            TargetingUtility.DrawGizmoCircle(transform.position, OrbitRadius);
        }
    }
}
