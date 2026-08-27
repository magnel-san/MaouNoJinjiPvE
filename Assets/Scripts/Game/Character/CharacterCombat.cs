using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // 自身のコライダーが敵のコライダーに触れた際、ダメージと中点からの放射ノックバックを与える。
    // 同じ相手からの連続攻撃は無敵時間の間0ダメージになるが、吹き飛ばし(ノックバック)自体は
    // 無敵時間中の接触でも発生する(密着したまま固まらないように)。
    // ただし衝突は毎物理ステップ(OnCollisionStay)通知されるため、ダメージと同じ頻度で
    // 適用すると力が蓄積しすぎるので、吹き飛ばしはダメージより短い専用の間隔でレート制限する。
    [RequireComponent(typeof(CharacterStats), typeof(CharacterHealth), typeof(CharacterIdentity))]
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterCombat : MonoBehaviour
    {
        [Tooltip("同じ相手からの吹き飛ばしを再度加えるまでの最短間隔(秒)。無敵時間より短くして構わない")]
        public float KnockbackInterval = 0.25f;

        CharacterStats stats;
        CharacterHealth health;
        CharacterIdentity identity;
        Rigidbody rb;

        readonly Dictionary<CharacterCombat, float> lastDamageTimeByAttacker = new Dictionary<CharacterCombat, float>();
        readonly Dictionary<CharacterCombat, float> lastKnockbackTimeByAttacker = new Dictionary<CharacterCombat, float>();

        void Awake()
        {
            stats = GetComponent<CharacterStats>();
            health = GetComponent<CharacterHealth>();
            identity = GetComponent<CharacterIdentity>();
            rb = GetComponent<Rigidbody>();
        }

        void Start()
        {
            // 足元コライダーなど、Rigidbody非所持の子コライダーからの衝突イベントも中継してもらう。
            foreach (var relay in GetComponentsInChildren<CollisionRelay>())
            {
                relay.Enter += TryHandleContact;
                relay.Stay += TryHandleContact;
            }
        }

        void OnCollisionEnter(Collision collision) => TryHandleContact(collision);
        void OnCollisionStay(Collision collision) => TryHandleContact(collision);

        void TryHandleContact(Collision collision)
        {
            if (!health.IsAlive) return;

            var otherCombat = collision.collider.GetComponentInParent<CharacterCombat>();
            if (otherCombat == null || otherCombat == this) return;

            var otherIdentity = otherCombat.identity;
            if (otherIdentity == null || otherIdentity.Team == identity.Team) return;
            if (!otherCombat.health.IsAlive) return;

            bool canDamage = !lastDamageTimeByAttacker.TryGetValue(otherCombat, out float lastDamageTime)
                || Time.time - lastDamageTime >= stats.InvincibilityTime;

            if (canDamage)
            {
                lastDamageTimeByAttacker[otherCombat] = Time.time;
                health.ApplyDamage(otherCombat.stats.AttackPower);
            }

            bool canKnockback = !lastKnockbackTimeByAttacker.TryGetValue(otherCombat, out float lastKnockbackTime)
                || Time.time - lastKnockbackTime >= KnockbackInterval;

            if (canKnockback)
            {
                lastKnockbackTimeByAttacker[otherCombat] = Time.time;
                ApplyRadialKnockback(otherCombat);
            }
        }

        void ApplyRadialKnockback(CharacterCombat other)
        {
            Vector3 midpoint = (transform.position + other.transform.position) * 0.5f;
            Vector3 dir = transform.position - midpoint;
            if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
            dir.Normalize();

            if (rb != null) rb.AddForce(dir * stats.KnockbackVectorStrength, ForceMode.Impulse);
        }
    }
}
