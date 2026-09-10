using UnityEngine;

namespace Game
{
    // 移動タイプ「空を飛ぶ」。揚力で高さを保ちつつランダムな生存敵を追跡し、一定時間ごとに
    // 強制的に降下して地上に留まる(無防備になる)周期を繰り返す。技(スキル)とは完全に独立しており、
    // このコンポーネント自体は攻撃や効果を一切持たない(以前は攻撃が降下を即トリガーしていたが、
    // 技との連動は廃止し純粋な周期サイクルのみにしてある)。
    [RequireComponent(typeof(Rigidbody), typeof(CharacterIdentity))]
    public class FlyMovementAbility : MonoBehaviour, IMovementIntentSource
    {
        enum FlightState { Flying, Descending, Grounded }

        [Header("浮遊 (揚力は高さ誤差から自動調整)")]
        public float TargetHeight = 6f;
        public float LiftSpringStrength = 20f;
        public float LiftDamping = 5f;
        [Tooltip("この高さ以下まで降りたら「着地」とみなす")]
        public float GroundedHeightThreshold = 0.6f;

        [Header("隙 (地上に留まり無防備になる時間)")]
        [Tooltip("何秒飛行するごとに強制的に降下させるか")]
        public float PeriodicDescentInterval = 4f;
        [Tooltip("着地してから再び飛び立つまで、地上に留まる秒数。ここを長くするほど隙が大きくなる")]
        public float GroundedVulnerableDuration = 3f;

        [Header("移動 (地上キャラより低速にする)")]
        [Range(0.1f, 1f)] public float HorizontalSpeedMultiplier = 0.55f;

        public int MovementPriority => 10;

        // Grounded中はスキル側が任意で参照できる(必須ではない。例: 無防備な間だけ攻撃を控える等)。
        public bool IsGrounded => state == FlightState.Grounded;

        Rigidbody rb;
        CharacterIdentity identity;
        CharacterIdentity target;
        FlightState state = FlightState.Flying;
        float periodicDescentTimer;
        float groundedTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            identity = GetComponent<CharacterIdentity>();
        }

        void FixedUpdate()
        {
            if (target == null || !target.IsAlive)
            {
                target = TargetingUtility.FindRandomLivingEnemy(identity.Team);
            }

            UpdateFlightState();
        }

        void UpdateFlightState()
        {
            switch (state)
            {
                case FlightState.Flying:
                    periodicDescentTimer += Time.fixedDeltaTime;
                    if (periodicDescentTimer >= PeriodicDescentInterval)
                    {
                        state = FlightState.Descending;
                        return;
                    }
                    ApplyLiftForce();
                    break;

                case FlightState.Descending:
                    // 揚力オフのまま自由落下させ、着地したら無防備な滞留状態へ移行する。
                    if (transform.position.y <= GroundedHeightThreshold)
                    {
                        state = FlightState.Grounded;
                        groundedTimer = GroundedVulnerableDuration;
                    }
                    break;

                case FlightState.Grounded:
                    groundedTimer -= Time.fixedDeltaTime;
                    if (groundedTimer <= 0f)
                    {
                        state = FlightState.Flying;
                        periodicDescentTimer = 0f;
                    }
                    break;
            }
        }

        void ApplyLiftForce()
        {
            float heightError = TargetHeight - transform.position.y;
            float liftForce = heightError * LiftSpringStrength - rb.linearVelocity.y * LiftDamping;
            rb.AddForce(Vector3.up * liftForce, ForceMode.Acceleration);
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            if (target == null) return false;

            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            intent = new MovementIntent { DesiredDirection = dir.normalized, Move = true, SpeedMultiplier = HorizontalSpeedMultiplier };
            return true;
        }
    }
}
