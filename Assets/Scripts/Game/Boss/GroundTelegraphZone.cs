using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // ボスの予告地面攻撃1箇所分。赤い警告円(半透明の塗り円、残り時間が短くなるほど速く点滅)を表示して
    // 一定時間待ち、時間が来たらその場に留まっているキャラへダメージを与えて消える。
    // BombProjectileと同じ「フューズタイマー→発動→自己消滅」の構造をUpdate()ベースで実装する。
    public class GroundTelegraphZone : MonoBehaviour
    {
        // 現在アクティブな(まだ着弾していない)ゾーンの一覧。CharacterRegistryと同じ「静的な共有窓口」の形で、
        // PlayerCommandIntentSourceの退避コマンドがここを見て危険地帯を避けられるようにする。
        static readonly List<GroundTelegraphZone> _active = new List<GroundTelegraphZone>();
        public static IReadOnlyList<GroundTelegraphZone> Active => _active;

        public Vector3 Center => transform.position;
        public float Radius { get; private set; }

        float warningTime;
        float totalWarningTime;
        float damage;
        CharacterIdentity owner;
        AudioClip detonateSound;
        bool detonated;

        Material visualMaterial;

        public static void Spawn(Vector3 position, float radius, float warningSeconds, float damage, CharacterIdentity owner,
            AudioClip warningSound = null, AudioClip detonateSound = null)
        {
            var go = new GameObject("GroundTelegraphZone");
            go.transform.position = position;
            go.AddComponent<GroundTelegraphZone>().Initialize(radius, warningSeconds, damage, owner, warningSound, detonateSound);
        }

        void Initialize(float zoneRadius, float warningSeconds, float zoneDamage, CharacterIdentity zoneOwner,
            AudioClip warningSound, AudioClip zoneDetonateSound)
        {
            Radius = zoneRadius;
            warningTime = totalWarningTime = Mathf.Max(0.1f, warningSeconds);
            damage = zoneDamage;
            owner = zoneOwner;
            detonateSound = zoneDetonateSound;

            BuildVisual();
            SfxUtil.PlayAt(warningSound, transform.position);
        }

        void OnEnable() => _active.Add(this);
        void OnDisable() => _active.Remove(this);

        void BuildVisual()
        {
            var visual = new GameObject("WarningDisc");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var meshFilter = visual.AddComponent<MeshFilter>();
            meshFilter.mesh = BuildDiscMesh(Radius, 32);

            var visualRenderer = visual.AddComponent<MeshRenderer>();
            visualMaterial = new Material(VfxShaderUtil.GetTransparentShader()) { color = new Color(1f, 0.15f, 0.1f, 0.35f) };
            visualRenderer.sharedMaterial = visualMaterial;
            visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
        }

        static Mesh BuildDiscMesh(float r, int segments)
        {
            var mesh = new Mesh { name = "WarningDisc" };
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (var i = 0; i < segments; i++)
            {
                var t = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f);
            }
            for (var i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if (detonated) return;

            warningTime -= Time.deltaTime;

            // 残り時間が短くなるほど速く点滅させ、着弾が近いことを分かりやすくする。
            var elapsedRatio = 1f - Mathf.Clamp01(warningTime / totalWarningTime);
            var blinkSpeed = Mathf.Lerp(4f, 16f, elapsedRatio);
            var pulse = 0.3f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            var c = visualMaterial.color;
            c.a = pulse;
            visualMaterial.color = c;

            if (warningTime <= 0f) Detonate();
        }

        void Detonate()
        {
            detonated = true;

            var color = new Color(1f, 0.2f, 0.1f);
            ExplosionRingEffect.Spawn(transform.position, Radius, color, 0.35f);
            SfxUtil.PlayAt(detonateSound, transform.position);

            var hits = Physics.OverlapSphere(transform.position, Radius);
            var affected = new HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == owner || !affected.Add(targetIdentity)) continue;
                if (owner != null && targetIdentity.Team == owner.Team) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, color, owner);
                BossAttackFx.NotifyPlayerHit(targetIdentity);
            }

            NotifyNearMissDodges(affected);

            Destroy(gameObject);
        }

        // 着弾範囲のすぐ外にいた(=間一髪よけられた)プレイヤーキャラにDODGE表示を出す。
        void NotifyNearMissDodges(HashSet<CharacterIdentity> hitTargets)
        {
            const float nearMissMultiplier = 1.6f;
            var nearMissRadius = Radius * nearMissMultiplier;

            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team != Team.Player || !c.IsAlive || hitTargets.Contains(c)) continue;
                if (Vector3.Distance(c.transform.position, transform.position) <= nearMissRadius)
                {
                    BossAttackFx.NotifyPlayerDodged(c);
                }
            }
        }
    }
}
