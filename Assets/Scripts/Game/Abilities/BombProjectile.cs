using UnityEngine;

namespace Game
{
    // Bタイプの爆弾。投下後、フューズタイム(クールダウン)を経て爆発し、
    // 放射状にベクトルを与えて吹き飛ばし、ダメージを与えてから消滅する。
    [RequireComponent(typeof(Rigidbody))]
    public class BombProjectile : MonoBehaviour
    {
        float fuseTime;
        float knockbackVector;
        float damage;
        float explosionRadius;
        CharacterIdentity owner;
        bool exploded;

        public void Initialize(float fuseTime, float knockbackVector, float damage, float explosionRadius, CharacterIdentity owner)
        {
            this.fuseTime = fuseTime;
            this.knockbackVector = knockbackVector;
            this.damage = damage;
            this.explosionRadius = explosionRadius;
            this.owner = owner;
        }

        void Update()
        {
            if (exploded) return;
            fuseTime -= Time.deltaTime;
            if (fuseTime <= 0f) Explode();
        }

        void Explode()
        {
            exploded = true;

            var color = new Color(1f, 0.55f, 0.1f);
            ExplosionRingEffect.Spawn(transform.position, explosionRadius, color);

            var hits = Physics.OverlapSphere(transform.position, explosionRadius);
            var affected = new System.Collections.Generic.HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == owner || !affected.Add(targetIdentity)) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, color, owner);

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
