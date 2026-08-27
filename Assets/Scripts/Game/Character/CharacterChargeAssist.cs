using System.Linq;
using UnityEngine;

namespace Game
{
    // 全キャラ共通の膠着回避機能。感知範囲内に敵が一定時間留まり続けると、
    // その敵へ向けて軽い突進(速度変化)を1回加える。
    // 追いかけっこが同じ距離を保ったまま回り続けて絶対に接触しない、といったスタックを断ち切るための保険。
    [RequireComponent(typeof(CharacterIdentity), typeof(Rigidbody))]
    public class CharacterChargeAssist : MonoBehaviour
    {
        [Header("感知範囲・突進 (膠着状態を崩すための全キャラ共通の保険)")]
        public float SenseRadius = 5f;
        [Tooltip("この秒数、同じ敵が感知範囲内に留まり続けると突進する")]
        public float TimeBeforeCharge = 3f;
        [Tooltip("突進の勢い(速度変化, m/s)")]
        public float ChargeForce = 8f;
        [Tooltip("突進後、再び判定を始めるまでのクールダウン")]
        public float ChargeCooldown = 2f;

        CharacterIdentity identity;
        Rigidbody rb;
        IDistanceHoldingAbility[] distanceHolders;
        CharacterIdentity sensedTarget;
        float senseTimer;
        float cooldownTimer;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            rb = GetComponent<Rigidbody>();
            TargetingUtility.CreateRangeGizmoCollider(transform, "ChargeSenseRange", SenseRadius);
        }

        void Start()
        {
            // StealthKite/Firework/ChainLightningのような「距離を保って戦う」アビリティを持つ場合、
            // そちらが今まさに距離を保てている(IsHoldingDistance)間は、このスタック回避用の突進を働かせない。
            distanceHolders = GetComponents<MonoBehaviour>().OfType<IDistanceHoldingAbility>().ToArray();
        }

        void FixedUpdate()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.fixedDeltaTime;
                return;
            }

            if (distanceHolders != null)
            {
                foreach (var holder in distanceHolders)
                {
                    if (holder.IsHoldingDistance)
                    {
                        senseTimer = 0f;
                        return;
                    }
                }
            }

            var nearest = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            bool inRange = nearest != null && Vector3.Distance(transform.position, nearest.transform.position) <= SenseRadius;

            if (!inRange || nearest != sensedTarget)
            {
                sensedTarget = inRange ? nearest : null;
                senseTimer = 0f;
                return;
            }

            senseTimer += Time.fixedDeltaTime;
            if (senseTimer < TimeBeforeCharge) return;

            Vector3 dir = nearest.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                rb.AddForce(dir.normalized * ChargeForce, ForceMode.VelocityChange);
            }

            senseTimer = 0f;
            cooldownTimer = ChargeCooldown;
        }

        // デバッグ表示用: 選択時のみ感知範囲をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.5f);
            TargetingUtility.DrawGizmoCircle(transform.position, SenseRadius);
        }
    }
}
