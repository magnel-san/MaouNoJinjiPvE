using UnityEngine;

namespace Game
{
    // Bタイプ: 浮遊。揚力で維持すべき高さを保ちつつ、ランダムな生存敵をその敵が死ぬまで追跡する。
    // 攻撃範囲(コライダー半径)に対象が入るとクールダウンで爆弾を投下し、投下時と定期的に着地して隙を作る。
    // 「降下開始」→「着地するまで自由落下」→「着地後、指定秒だけ地上に留まり無防備になる」→「揚力再開」
    // という状態遷移にすることで、地上に留まる時間(隙の大きさ)を落下距離に関係なく正確に調整できる。
    // 独立タイプのため、他の移動系アビリティとは併用しない前提で使う。
    [RequireComponent(typeof(Rigidbody), typeof(CharacterIdentity))]
    public class FlyingBomberAbility : MonoBehaviour, IMovementIntentSource
    {
        enum FlightState { Flying, Descending, Grounded }

        [Header("浮遊 (揚力は高さ誤差から自動調整)")]
        public float TargetHeight = 6f;
        public float LiftSpringStrength = 20f;
        public float LiftDamping = 5f;
        [Tooltip("この高さ以下まで降りたら「着地」とみなす")]
        public float GroundedHeightThreshold = 0.6f;

        [Header("攻撃")]
        [Range(1, 5)] public int AttackCooldownTier = 3;

        [Header("隙 (地上に留まり無防備になる時間)")]
        [Tooltip("何秒飛行するごとに強制的に降下させるか")]
        public float PeriodicDescentInterval = 4f;
        [Tooltip("着地してから再び飛び立つまで、地上に留まる秒数。ここを長くするほど隙が大きくなる")]
        public float GroundedVulnerableDuration = 3f;

        [Header("移動 (地上キャラより低速にする)")]
        [Range(0.1f, 1f)] public float HorizontalSpeedMultiplier = 0.55f;

        [Header("爆弾")]
        public GameObject BombPrefab;
        public float BombFuseTime = 1.5f;
        public float BombKnockbackVector = 20f;
        public float BombDamage = 15f;
        public float BombExplosionRadius = 3f;
        [Tooltip("投下のたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip DropSound;

        public int MovementPriority => 10;

        Rigidbody rb;
        CharacterIdentity identity;
        CharacterIdentity target;
        SphereCollider rangeCollider;
        float cooldownTimer;
        float periodicDescentTimer;
        FlightState state = FlightState.Flying;
        float groundedTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            identity = GetComponent<CharacterIdentity>();
            // 爆弾の投下判定範囲(コライダー半径)は爆発範囲(BombExplosionRadius)と同じにする。
            // 別々の値だと、投下判定は通ったのに爆風が届かない(その逆も)というズレが起きるため、
            // 単一の値を共有させて必ず一致させる。
            rangeCollider = TargetingUtility.CreateRangeGizmoCollider(transform, "BomberAttackRange", BombExplosionRadius);
        }

        void FixedUpdate()
        {
            if (target == null || !target.IsAlive)
            {
                target = TargetingUtility.FindRandomLivingEnemy(identity.Team);
            }

            UpdateFlightState();

            cooldownTimer -= Time.fixedDeltaTime;

            // 高さ(Y)は無視し、XZ平面上の距離だけで範囲判定する。この機体は空中でTargetHeight分
            // 高い位置に浮いているため、3D距離(Vector3.Distance)で判定すると高さ差だけで
            // 範囲外になってしまい、地上の敵にほぼ永久に攻撃できなくなる不具合があった。
            bool targetInRange = false;
            if (target != null)
            {
                var delta = target.transform.position - transform.position;
                delta.y = 0f;
                targetInRange = delta.sqrMagnitude <= rangeCollider.radius * rangeCollider.radius;
            }

            if (state == FlightState.Flying && targetInRange && cooldownTimer <= 0f)
            {
                DropBomb();
                var cfg = GameBalanceConfig.Instance;
                cooldownTimer = cfg != null ? cfg.BomberAttackCooldown.Get(AttackCooldownTier) : 4f;
                BeginDescent();
            }
        }

        void BeginDescent()
        {
            state = FlightState.Descending;
            periodicDescentTimer = 0f;
        }

        void UpdateFlightState()
        {
            switch (state)
            {
                case FlightState.Flying:
                    periodicDescentTimer += Time.fixedDeltaTime;
                    if (periodicDescentTimer >= PeriodicDescentInterval)
                    {
                        BeginDescent();
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

        void DropBomb()
        {
            GameObject bombGO = BombPrefab != null
                ? Instantiate(BombPrefab, transform.position, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (BombPrefab == null)
            {
                bombGO.transform.position = transform.position;
                bombGO.transform.localScale = Vector3.one * 0.5f;
            }

            var bomb = bombGO.GetComponent<BombProjectile>();
            if (bomb == null) bomb = bombGO.AddComponent<BombProjectile>();
            bomb.Initialize(BombFuseTime, BombKnockbackVector, BombDamage, BombExplosionRadius, identity);

            SfxUtil.PlayAt(DropSound, transform.position);
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

        // デバッグ表示用: 選択時のみ投下判定範囲(=爆発半径)をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            TargetingUtility.DrawGizmoCircle(transform.position, BombExplosionRadius);
        }
    }
}
