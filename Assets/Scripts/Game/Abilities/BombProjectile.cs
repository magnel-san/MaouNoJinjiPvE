using UnityEngine;

namespace Game
{
    // Bタイプの爆弾。投下後、フューズタイム(クールダウン)を経て爆発し、
    // 放射状にベクトルを与えて吹き飛ばし、ダメージを与えてから消滅する。
    // 着弾地点には警告用の円(フューズが進むほど縮んでいく)を表示し、いつ・どこで爆発するか
    // 事前に分かるようにしてある。
    [RequireComponent(typeof(Rigidbody))]
    public class BombProjectile : MonoBehaviour
    {
        static readonly Color WarningColor = new Color(1f, 0.6f, 0.15f, 0.5f);
        static readonly Color ExplosionColor = new Color(1f, 0.55f, 0.1f);

        float fuseTime;
        float totalFuseTime;
        float knockbackVector;
        float damage;
        float explosionRadius;
        CharacterIdentity owner;
        bool exploded;
        Transform warningRing;

        public void Initialize(float fuseTime, float knockbackVector, float damage, float explosionRadius, CharacterIdentity owner)
        {
            this.fuseTime = fuseTime;
            totalFuseTime = Mathf.Max(0.01f, fuseTime);
            this.knockbackVector = knockbackVector;
            this.damage = damage;
            this.explosionRadius = explosionRadius;
            this.owner = owner;

            BuildWarningRing();
        }

        void BuildWarningRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "BombWarningRing";
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(VfxShaderUtil.GetTransparentShader()) { color = WarningColor };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            warningRing = go.transform;
            warningRing.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
        }

        void Update()
        {
            if (exploded) return;
            fuseTime -= Time.deltaTime;

            if (warningRing != null)
            {
                // フューズが進むほど輪が締まっていき、着弾直前が分かりやすいように点滅を速める。
                var remainingFrac = Mathf.Clamp01(fuseTime / totalFuseTime);
                var ringScale = Mathf.Lerp(explosionRadius * 0.4f, explosionRadius, remainingFrac) * 2f;
                warningRing.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
                warningRing.localScale = new Vector3(ringScale, 0.01f, ringScale);

                var blink = 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(4f, 14f, 1f - remainingFrac)));
                var c = WarningColor;
                c.a = blink;
                warningRing.GetComponent<Renderer>().sharedMaterial.color = c;
            }

            if (fuseTime <= 0f) Explode();
        }

        void Explode()
        {
            exploded = true;
            if (warningRing != null) Destroy(warningRing.gameObject);

            // 中心の閃光+2重の爆風リングで、単発の輪より弾けるような爆発感を出す。
            CombatFx.ImpactBurst(transform.position + Vector3.up * 0.3f, ExplosionColor, 0.5f);
            ExplosionRingEffect.Spawn(transform.position, explosionRadius * 0.5f, Color.white, 0.25f);
            ExplosionRingEffect.Spawn(transform.position, explosionRadius, ExplosionColor, 0.45f);
            CameraShake.Shake(0.3f);

            var hits = Physics.OverlapSphere(transform.position, explosionRadius);
            var affected = new System.Collections.Generic.HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == owner || !affected.Add(targetIdentity)) continue;
                if (owner != null && targetIdentity.Team == owner.Team) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, ExplosionColor, owner);

                var rb = targetIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = targetIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    // 爆発は質量に関わらず一定の速度変化を与える(重いキャラでも爆風でしっかり吹き飛ぶように)。
                    rb.AddForce(dir.normalized * knockbackVector, ForceMode.VelocityChange);
                }
            }

            Destroy(gameObject);
        }
    }
}
