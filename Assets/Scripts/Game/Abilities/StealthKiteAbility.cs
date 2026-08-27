using UnityEngine;

namespace Game
{
    // Cタイプ: 隠密。一番近くの敵から固定距離(最大〜最小)を保つように移動する。
    // 固定距離を保っている間は敵の方を向き、クールダウンで弓矢を放つ。Dタイプと併用できる
    // (Dが存在する場合はDの移動判断が優先されるが、このスクリプトは実際の距離を見て独立に射撃・旋回を行う)。
    [RequireComponent(typeof(CharacterIdentity))]
    public class StealthKiteAbility : MonoBehaviour, IMovementIntentSource, IDistanceHoldingAbility
    {
        [Header("距離維持")]
        public float MaxDistance = 8f;
        public float MinDistance = 4f;

        [Header("弓矢")]
        [Range(1, 5)] public int AttackCooldownTier = 3;
        public GameObject ArrowPrefab;
        public float ArrowSpeed = 45f;
        public float ArrowDamage = 8f;
        public float ArrowLifetime = 3f;
        [Tooltip("発射のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip FireSound;

        public int MovementPriority => 10;
        public bool IsHoldingDistance { get; private set; }

        CharacterIdentity identity;
        float cooldownTimer;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        void Update()
        {
            cooldownTimer -= Time.deltaTime;

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null)
            {
                IsHoldingDistance = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, target.transform.position);
            IsHoldingDistance = dist <= MaxDistance && dist >= MinDistance;

            if (IsHoldingDistance && cooldownTimer <= 0f)
            {
                FireArrow(target);
                var cfg = GameBalanceConfig.Instance;
                cooldownTimer = cfg != null ? cfg.StealthAttackCooldown.Get(AttackCooldownTier) : 2.5f;
            }
        }

        void FireArrow(CharacterIdentity target)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;

            GameObject arrowGO = ArrowPrefab != null
                ? Instantiate(ArrowPrefab, transform.position, Quaternion.LookRotation(dir))
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            if (ArrowPrefab == null)
            {
                arrowGO.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(dir));
                arrowGO.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
            }

            var arrow = arrowGO.GetComponent<ArrowProjectile>();
            if (arrow == null) arrow = arrowGO.AddComponent<ArrowProjectile>();
            arrow.Initialize(dir, ArrowSpeed, ArrowDamage, ArrowLifetime, identity);

            SfxUtil.PlayAt(FireSound, transform.position);
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.0001f) return false;
            Vector3 flatDirToTarget = toTarget / dist;

            Vector3 moveDir;
            bool move;
            if (dist > MaxDistance) { moveDir = flatDirToTarget; move = true; }
            else if (dist < MinDistance) { moveDir = -flatDirToTarget; move = true; }
            else { moveDir = Vector3.zero; move = false; }

            intent = new MovementIntent { DesiredDirection = moveDir, FaceOverride = flatDirToTarget, Move = move };
            return true;
        }

        // デバッグ表示用: 選択時のみ維持距離の最大・最小をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MaxDistance);
            Gizmos.color = new Color(1f, 0.25f, 0.2f);
            TargetingUtility.DrawGizmoCircle(transform.position, MinDistance);
        }
    }
}
