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

        [Header("Knockback Direction Control")]
        [Tooltip("上方向(Y軸)への吹き飛ばし成分の上限率(0.0～1.0)。例えば0.3ならY成分を最大30%に制限します")]
        [Range(0f, 1f)]
        private float MaxUpwardAngleRatio = 0.15f;

        [Tooltip("削減された上方向への力を横方向(水平ベクトル)に還元・変換するかどうか")]
        private bool RedirectUpwardToHorizontal = false;

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
                health.ApplyDamage(otherCombat.stats.AttackPower, CombatFx.DefaultDamageColor, otherIdentity);
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

            // --- ノックバック方向(Y軸制御および横方向への変換)の調整 ---
            if (dir.y > MaxUpwardAngleRatio)
            {
                float excessY = dir.y - MaxUpwardAngleRatio;
                dir.y = MaxUpwardAngleRatio;

                if (RedirectUpwardToHorizontal)
                {
                    Vector3 horizontalDir = new Vector3(dir.x, 0f, dir.z);
                    if (horizontalDir.sqrMagnitude > 0.0001f)
                    {
                        horizontalDir.Normalize();
                        // 削られたY成分の大きさを水平方向に追加付与
                        dir += horizontalDir * excessY;
                    }
                }
            }

            // 最終的なベクトル長を1に揃える
            dir.Normalize();

            if (rb != null) rb.AddForce(dir * stats.KnockbackVectorStrength, ForceMode.Impulse);
        }
    }
}
