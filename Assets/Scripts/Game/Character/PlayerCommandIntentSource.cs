using UnityEngine;

namespace Game
{
    // 戦闘フェーズ中、プレイヤーからの全体コマンド(集合/退避、BattleCommandState参照)が有効な間、
    // そのキャラ本来のアビリティの移動判断より優先して行動を上書きする。MovementPriorityを
    // 既存アビリティ(最大でもFleeAbilityの20)より大幅に高くすることで、CharacterMovementの
    // 優先度選択(最高値が勝つ)により自動的に勝つ。コマンドが無効(None)の間は何も提案せず、
    // 通常のアビリティ側AIへそのまま行動を委ねる(=このコンポーネントは常時Enabledのままで良い)。
    [RequireComponent(typeof(CharacterIdentity))]
    public class PlayerCommandIntentSource : MonoBehaviour, IMovementIntentSource
    {
        [Tooltip("集合コマンド中、円の中心からこの距離まで近づいたら停止する")]
        public float RallyArriveRadius = 1f;
        [Tooltip("退避コマンド中、ボスの予告地面攻撃(GroundTelegraphZone)の外周からさらにこの距離まで離れようとする")]
        public float TelegraphSafetyMargin = 2f;

        public int MovementPriority => 100;

        CharacterIdentity identity;

        void Awake() => identity = GetComponent<CharacterIdentity>();

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            if (identity.Team != Team.Player) return false;

            switch (BattleCommandState.CommandType)
            {
                case PlayerCommandType.Rally: return TryGetRallyIntent(out intent);
                case PlayerCommandType.Flee: return TryGetFleeIntent(out intent);
                default: return false;
            }
        }

        bool TryGetRallyIntent(out MovementIntent intent)
        {
            intent = default;

            var toCenter = BattleCommandState.RallyWorldPosition - transform.position;
            toCenter.y = 0f;
            var dist = toCenter.magnitude;
            // BattleCommandState.RallyRadius(既定3.5m)は「だいたいこの辺りに集まれ」という
            // 表示用の円の大きさであり、実際に停止する距離としては大きすぎる(回復/遠距離維持タイプが
            // 集合命令でボスの位置へ集まらせても、ボスの手前で止まってしまい接触ダメージを与えられない
            // 原因になっていた)。実際の停止距離はRallyArriveRadius単独で決める。
            var arriveRadius = RallyArriveRadius;

            if (dist <= arriveRadius)
            {
                // 円の中には入ったので前進はしないが、円の中心の方を向かせておく(棒立ちで変な方向を向かないように)。
                intent = new MovementIntent { Move = false, FaceOverride = dist > 0.0001f ? toCenter / dist : (Vector3?)null };
                return true;
            }

            intent = new MovementIntent { DesiredDirection = toCenter / dist, Move = true };
            return true;
        }

        bool TryGetFleeIntent(out MovementIntent intent)
        {
            intent = default;

            // ボスの予告地面攻撃(赤い警告円)の危険範囲内にいる場合は、それを避ける方向を最優先で使う
            // (ただ敵から離れるだけだと、逃げた先がちょうど別の警告円の中、ということが起こり得るため)。
            if (TryGetTelegraphAvoidance(out var avoidDir))
            {
                intent = new MovementIntent { DesiredDirection = avoidDir, Move = true, FaceOverride = -avoidDir };
                return true;
            }

            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            var away = transform.position - target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
            if (away.sqrMagnitude < 0.0001f) return false;

            intent = new MovementIntent { DesiredDirection = away.normalized, Move = true };
            return true;
        }

        // 現在アクティブな警告円・警告矩形のうち、危険域に入っているものすべてから
        // 離れる方向を合成する。近いものほど強く効かせる。危険域に何もいなければfalseを返す。
        bool TryGetTelegraphAvoidance(out Vector3 avoidDirection)
        {
            avoidDirection = Vector3.zero;
            var hasDanger = false;

            foreach (var zone in GroundTelegraphZone.Active)
            {
                if (zone == null) continue;

                var toChar = transform.position - zone.Center;
                toChar.y = 0f;
                var dist = toChar.magnitude;
                var dangerRadius = zone.Radius + TelegraphSafetyMargin;
                if (dist >= dangerRadius) continue;

                var weight = 1f - Mathf.Clamp01(dist / Mathf.Max(dangerRadius, 0.0001f));
                var dir = dist > 0.0001f ? toChar / dist : new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
                avoidDirection += dir * weight;
                hasDanger = true;
            }

            if (TryGetRectTelegraphAvoidance(out var rectDir))
            {
                avoidDirection += rectDir;
                hasDanger = true;
            }

            if (!hasDanger || avoidDirection.sqrMagnitude < 0.0001f) return false;

            avoidDirection.Normalize();
            return true;
        }

        // 矩形の警告ゾーン(RectTelegraphZone)の内側にいる場合、最も近い辺へ抜け出す方向を返す。
        // 矩形は半画面規模の巨大サイズを想定しているため、円のような「危険域まで距離」ではなく
        // 「ゾーンの内側にいるかどうか」だけを見て、内側なら常に一定以上の緊急度で押し出す。
        bool TryGetRectTelegraphAvoidance(out Vector3 avoidDirection)
        {
            avoidDirection = Vector3.zero;
            var hasDanger = false;

            foreach (var zone in RectTelegraphZone.Active)
            {
                if (zone == null) continue;

                var local = transform.position - zone.Center;
                local.y = 0f;

                var distPosX = zone.HalfExtents.x - local.x;
                var distNegX = zone.HalfExtents.x + local.x;
                var distPosZ = zone.HalfExtents.y - local.z;
                var distNegZ = zone.HalfExtents.y + local.z;

                if (distPosX <= 0f || distNegX <= 0f || distPosZ <= 0f || distNegZ <= 0f) continue; // 外側にいるなら退避不要

                var minDist = Mathf.Min(Mathf.Min(distPosX, distNegX), Mathf.Min(distPosZ, distNegZ));
                Vector3 dir;
                if (minDist == distPosX) dir = Vector3.right;
                else if (minDist == distNegX) dir = Vector3.left;
                else if (minDist == distPosZ) dir = Vector3.forward;
                else dir = Vector3.back;

                var span = Mathf.Max(Mathf.Min(zone.HalfExtents.x, zone.HalfExtents.y), 0.0001f);
                var weight = Mathf.Max(0.5f, 1f - Mathf.Clamp01(minDist / span));
                avoidDirection += dir * weight;
                hasDanger = true;
            }

            return hasDanger;
        }
    }
}
