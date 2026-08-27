using UnityEngine;

namespace Game
{
    // 上下(直立)と前後(進行方向)の姿勢制御。
    // 純粋な物理トルクではなく、常に直立+目標方向を向く姿勢へ毎ステップ回転補間する
    // 「見かけの物理」方式(Rigidbody.MoveRotationによるキネマティック的な制御)。
    // 接地摩擦や転倒からの復帰に左右されず、常に安定して直立・旋回できる。
    // フラフラした見た目はゲームプレイに影響しない演出用のノイズ揺れとして別途重ねる。
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterPosture : MonoBehaviour
    {
        [Header("直立・旋回の追従速度 (1秒あたりの回転補間割合)")]
        public float TurnSpeed = 12f;

        [Header("見た目の揺れ (ゲームプレイに影響しない演出用)")]
        public float WobbleAmplitude = 8f;
        public float WobbleFrequency = 1.2f;

        [HideInInspector] public Vector3 DesiredFacingDirection = Vector3.forward;

        Rigidbody rb;
        float noiseSeed;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            noiseSeed = Random.Range(0f, 1000f);
        }

        void FixedUpdate()
        {
            Vector3 flatFacing = DesiredFacingDirection.sqrMagnitude > 0.0001f
                ? Vector3.ProjectOnPlane(DesiredFacingDirection, Vector3.up)
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatFacing.sqrMagnitude < 0.0001f) flatFacing = Vector3.forward;

            Quaternion uprightFacing = Quaternion.LookRotation(flatFacing.normalized, Vector3.up);
            Quaternion target = uprightFacing * ComputeWobble();

            Quaternion next = Quaternion.Slerp(rb.rotation, target, TurnSpeed * Time.fixedDeltaTime);
            rb.angularVelocity = Vector3.zero;
            rb.MoveRotation(next);
        }

        // ゲームプレイに影響しないローカル空間のピッチ・ロール揺れ。Perlinノイズで滑らかに変化させる。
        Quaternion ComputeWobble()
        {
            float t = Time.time * WobbleFrequency;
            float pitch = (Mathf.PerlinNoise(noiseSeed, t) - 0.5f) * 2f * WobbleAmplitude;
            float roll = (Mathf.PerlinNoise(noiseSeed + 100f, t) - 0.5f) * 2f * WobbleAmplitude;
            return Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
