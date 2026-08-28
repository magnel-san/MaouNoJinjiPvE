using UnityEngine;

namespace Game
{
    // Fタイプの花火。矢と同じくRaycastで直進し(コライダーは自動でトリガー化して物理的な吹き飛ばしを防ぐ)、
    // 最初にキャラへ接触した瞬間その場で爆発(範囲ダメージ+吹き飛ばし、常時表示のリング演出付き)して消滅する。
    public class FireworkProjectile : MonoBehaviour
    {
        Vector3 direction;
        float speed;
        float explosionDamage;
        float explosionRadius;
        float knockbackVector;
        float lifetime;
        CharacterIdentity owner;

        public void Initialize(Vector3 direction, float speed, float explosionDamage, float explosionRadius,
            float knockbackVector, float lifetime, CharacterIdentity owner)
        {
            this.direction = direction.normalized;
            this.speed = speed;
            this.explosionDamage = explosionDamage;
            this.explosionRadius = explosionRadius;
            this.knockbackVector = knockbackVector;
            this.lifetime = lifetime;
            this.owner = owner;

            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.isTrigger = true;
            }
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            Vector3 start = transform.position;

            if (Physics.Raycast(start, direction, out RaycastHit hit, step))
            {
                var targetIdentity = hit.collider.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity != null && targetIdentity != owner)
                {
                    Explode();
                    return;
                }
            }

            transform.position = start + direction * step;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }

        void Explode()
        {
            var color = new Color(1f, 0.8f, 0.2f);
            ExplosionRingEffect.Spawn(transform.position, explosionRadius, color);

            var hits = Physics.OverlapSphere(transform.position, explosionRadius);
            var affected = new System.Collections.Generic.HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == owner || !affected.Add(targetIdentity)) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(explosionDamage, color, owner);

                var rb = targetIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = targetIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    rb.AddForce(dir.normalized * knockbackVector, ForceMode.VelocityChange);
                }
            }

            Destroy(gameObject);
        }
    }
}
