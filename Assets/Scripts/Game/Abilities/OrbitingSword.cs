using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // Gタイプの周囲を回る剣本体。所有者を中心に一定半径・速度で公転し、
    // 敵(別チーム)に接触するとダメージを与える。対象ごとに無敵時間を設け、連続接触での連打を防ぐ。
    public class OrbitingSword : MonoBehaviour
    {
        Transform owner;
        CharacterIdentity ownerIdentity;
        float damage;
        float invincibilityTime;
        float orbitRadius;
        float orbitHeight;
        float orbitSpeed;
        float currentAngleDeg;

        readonly Dictionary<CharacterIdentity, float> lastHitTime = new Dictionary<CharacterIdentity, float>();

        public void Initialize(Transform owner, CharacterIdentity ownerIdentity, float damage, float invincibilityTime,
            float orbitRadius, float orbitHeight, float orbitSpeed, float startAngleDeg)
        {
            this.owner = owner;
            this.ownerIdentity = ownerIdentity;
            this.damage = damage;
            this.invincibilityTime = invincibilityTime;
            this.orbitRadius = orbitRadius;
            this.orbitHeight = orbitHeight;
            this.orbitSpeed = orbitSpeed;
            currentAngleDeg = startAngleDeg;

            // 見た目用プレハブに元からコライダーが付いていてもトリガー化し、無ければ追加する。
            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                var col = gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.35f;
            }
            else
            {
                foreach (var c in colliders) c.isTrigger = true;
            }

            UpdatePosition();
        }

        void Update()
        {
            // 所有者が死んでも(SpinningSwordsAbility.enabledがCharacterHealth.Die()で無効化されるだけで
            // ownerのGameObject自体は消えないため)ownerはnullにならない。IsAliveも見て、死後は
            // 剣が消えずに周囲を回り続け、敵を延々と攻撃し続ける事故を防ぐ。
            if (owner == null || ownerIdentity == null || !ownerIdentity.IsAlive) { Destroy(gameObject); return; }
            currentAngleDeg += orbitSpeed * Time.deltaTime;
            UpdatePosition();
        }

        void UpdatePosition()
        {
            float rad = currentAngleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius;
            transform.position = owner.position + Vector3.up * orbitHeight + offset;
            transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
        }

        void OnTriggerEnter(Collider other) => TryDamage(other);
        void OnTriggerStay(Collider other) => TryDamage(other);

        void TryDamage(Collider other)
        {
            var identity = other.GetComponentInParent<CharacterIdentity>();
            if (identity == null || identity == ownerIdentity || identity.Team == ownerIdentity.Team) return;
            if (!identity.IsAlive) return;

            if (lastHitTime.TryGetValue(identity, out float last) && Time.time - last < invincibilityTime) return;
            lastHitTime[identity] = Time.time;

            var health = identity.GetComponent<CharacterHealth>();
            if (health != null && health.IsAlive) health.ApplyDamage(damage, new Color(0.6f, 0.9f, 1f), ownerIdentity);
        }
    }
}
