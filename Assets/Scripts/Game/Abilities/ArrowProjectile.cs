using UnityEngine;

namespace Game
{
    // Cタイプの矢。大きなベクトルで直進し、キャラに触れたらノックバックを与えずダメージのみ与える。
    // 高速移動でのすり抜けを避けるため、毎フレームRaycastで着弾判定を行う。消滅時間後に自動で消える。
    public class ArrowProjectile : MonoBehaviour
    {
        Vector3 direction;
        float speed;
        float damage;
        float lifetime;
        CharacterIdentity owner;

        public void Initialize(Vector3 direction, float speed, float damage, float lifetime, CharacterIdentity owner)
        {
            this.direction = direction.normalized;
            this.speed = speed;
            this.damage = damage;
            this.lifetime = lifetime;
            this.owner = owner;

            // 着弾判定はRaycastのみで行うため、矢自身のコライダーは物理的な押し出しが起きないよう
            // 必ずトリガーにしておく(キャラに接触しても吹き飛ばされないようにするため)。
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
                    var health = targetIdentity.GetComponent<CharacterHealth>();
                    if (health != null && health.IsAlive)
                    {
                        health.ApplyDamage(damage, new Color(1f, 1f, 0.6f));
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            transform.position = start + direction * step;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }
    }
}
