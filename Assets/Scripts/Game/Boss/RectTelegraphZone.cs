using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // GroundTelegraphZoneの矩形版。アリーナの半面(右/左/前/奥)を覆う予告攻撃に使う
    // (画面全体を覆う技にも、アリーナ全体を覆うサイズで流用する)。警告点滅→Detonateの
    // タイマー構造・見た目の点滅演出はGroundTelegraphZoneと同一で、判定範囲だけが円ではなく
    // ローカルXZ矩形になっている。
    public class RectTelegraphZone : MonoBehaviour
    {
        static readonly List<RectTelegraphZone> _active = new List<RectTelegraphZone>();
        public static IReadOnlyList<RectTelegraphZone> Active => _active;

        public Vector3 Center => transform.position;
        // (X半幅, Z半奥行き)
        public Vector2 HalfExtents { get; private set; }

        float warningTime;
        float totalWarningTime;
        float damage;
        CharacterIdentity owner;
        AudioClip detonateSound;
        bool detonated;
        // trueの場合、防御(グー)中のプレイヤーキャラはダメージを完全無効化する
        // (位置に関わらず全体に命中する「画面全体を覆う攻撃」向け)。
        bool ignoreGuard;

        Material visualMaterial;

        public static RectTelegraphZone Spawn(Vector3 center, Vector2 size, float warningSeconds, float damage,
            CharacterIdentity owner, bool ignoreGuard = false, AudioClip warningSound = null, AudioClip detonateSound = null)
        {
            var go = new GameObject("RectTelegraphZone");
            go.transform.position = center;
            var zone = go.AddComponent<RectTelegraphZone>();
            zone.Initialize(size, warningSeconds, damage, owner, ignoreGuard, warningSound, detonateSound);
            return zone;
        }

        void Initialize(Vector2 size, float warningSeconds, float zoneDamage, CharacterIdentity zoneOwner,
            bool zoneIgnoreGuard, AudioClip warningSound, AudioClip zoneDetonateSound)
        {
            HalfExtents = size * 0.5f;
            warningTime = totalWarningTime = Mathf.Max(0.1f, warningSeconds);
            damage = zoneDamage;
            owner = zoneOwner;
            ignoreGuard = zoneIgnoreGuard;
            detonateSound = zoneDetonateSound;

            BuildVisual();
            SfxUtil.PlayAt(warningSound, transform.position);
        }

        void OnEnable() => _active.Add(this);
        void OnDisable() => _active.Remove(this);

        void BuildVisual()
        {
            var visual = new GameObject("WarningRect");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var meshFilter = visual.AddComponent<MeshFilter>();
            meshFilter.mesh = BuildQuadMesh(HalfExtents.x, HalfExtents.y);

            var visualRenderer = visual.AddComponent<MeshRenderer>();
            visualMaterial = new Material(VfxShaderUtil.GetTransparentShader()) { color = new Color(1f, 0.15f, 0.1f, 0.35f) };
            visualRenderer.sharedMaterial = visualMaterial;
            visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
        }

        static Mesh BuildQuadMesh(float halfX, float halfZ)
        {
            var mesh = new Mesh { name = "WarningRect" };
            mesh.vertices = new[]
            {
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(halfX, 0f, -halfZ),
                new Vector3(halfX, 0f, halfZ),
                new Vector3(-halfX, 0f, halfZ),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if (detonated) return;

            warningTime -= Time.deltaTime;

            // 残り時間が短くなるほど速く点滅させ、着弾が近いことを分かりやすくする(GroundTelegraphZoneと同様)。
            var elapsedRatio = 1f - Mathf.Clamp01(warningTime / totalWarningTime);
            var blinkSpeed = Mathf.Lerp(4f, 16f, elapsedRatio);
            var pulse = 0.3f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
            var c = visualMaterial.color;
            c.a = pulse;
            visualMaterial.color = c;

            if (warningTime <= 0f) Detonate();
        }

        public bool Contains(Vector3 worldPos)
        {
            var local = worldPos - transform.position;
            return Mathf.Abs(local.x) <= HalfExtents.x && Mathf.Abs(local.z) <= HalfExtents.y;
        }

        void Detonate()
        {
            detonated = true;

            SfxUtil.PlayAt(detonateSound, transform.position);
            CameraShake.Shake(0.4f);

            var color = new Color(1f, 0.2f, 0.1f);
            var hitTargets = new HashSet<CharacterIdentity>();

            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || !c.IsAlive || c == owner) continue;
                if (owner != null && c.Team == owner.Team) continue;
                if (!Contains(c.transform.position)) continue;

                // ignoreGuard(画面全体を覆う攻撃)の場合、防御中のプレイヤーはダメージを完全無効化する。
                if (ignoreGuard && c.Team == Team.Player && BattleCommandState.GuardActive)
                {
                    BossAttackFx.NotifyPlayerGuarded(c);
                    continue;
                }

                var health = c.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;

                health.ApplyDamage(damage, color, owner);
                BossAttackFx.NotifyPlayerHit(c);
                hitTargets.Add(c);
            }

            // ignoreGuardの攻撃は位置に関わらず全体に命中するため、「よけた」の概念が無い
            // (被弾しなかったのは防御が効いたからであり、位置取りで回避したわけではない)。
            if (!ignoreGuard) NotifyNearMissDodges(hitTargets);

            Destroy(gameObject);
        }

        void NotifyNearMissDodges(HashSet<CharacterIdentity> hitTargets)
        {
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team != Team.Player || !c.IsAlive || hitTargets.Contains(c)) continue;
                BossAttackFx.NotifyPlayerDodged(c);
            }
        }
    }
}
