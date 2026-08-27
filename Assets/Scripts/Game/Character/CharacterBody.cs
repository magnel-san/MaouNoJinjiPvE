using UnityEngine;

namespace Game
{
    // 胴体はカプセルコライダー、足元にグリップ用の高摩擦コライダーを追加する複合コライダー構成。
    // 部位ごとに摩擦を変えることで、姿勢制御と組み合わせて「少し暴れる」移動フィールを出す。
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterBody : MonoBehaviour
    {
        [Header("空気抵抗 (推進力が常時かかり続けても無限加速しないための上限)")]
        public float LinearDamping = 0.6f;
        public float AngularDamping = 2f;

        [Header("胴体コライダー (カプセル)")]
        public float BodyRadius = 0.4f;
        public float BodyHeight = 1.8f;
        public Vector3 BodyCenter = new Vector3(0f, 0.9f, 0f);
        [Range(0f, 1f)] public float BodyFriction = 0.15f;

        [Header("足元コライダー (グリップ用・胴体よりは高摩擦)")]
        public float FootRadius = 0.35f;
        public Vector3 FootLocalPosition = new Vector3(0f, 0.2f, 0f);
        [Range(0f, 1f)] public float FootFriction = 0.45f;

        public CapsuleCollider BodyCollider { get; private set; }
        public SphereCollider FootCollider { get; private set; }

        void Awake()
        {
            SetupBodyCollider();
            SetupFootCollider();

            var rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = LinearDamping;
            rb.angularDamping = AngularDamping;
            // 転倒復帰などの姿勢制御が休止状態と競合してすり抜けないよう、自然にスリープしないようにする。
            rb.sleepThreshold = 0f;
        }

        void SetupBodyCollider()
        {
            BodyCollider = GetComponent<CapsuleCollider>();
            if (BodyCollider == null) BodyCollider = gameObject.AddComponent<CapsuleCollider>();
            BodyCollider.radius = BodyRadius;
            BodyCollider.height = BodyHeight;
            BodyCollider.center = BodyCenter;

            BodyCollider.sharedMaterial = new PhysicsMaterial("BodyMaterial")
            {
                dynamicFriction = BodyFriction,
                staticFriction = BodyFriction,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounciness = 0.05f,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        void SetupFootCollider()
        {
            var footGO = new GameObject("FootGrip");
            footGO.transform.SetParent(transform, false);
            footGO.transform.localPosition = FootLocalPosition;

            FootCollider = footGO.AddComponent<SphereCollider>();
            FootCollider.radius = FootRadius;
            FootCollider.sharedMaterial = new PhysicsMaterial("FootMaterial")
            {
                dynamicFriction = FootFriction,
                staticFriction = FootFriction,
                // Maximumだと地面側の摩擦とは無関係に常に一番高い値が採用され、
                // スティックスリップ的な「がくがく」した動きの原因になっていたためAverageに変更。
                frictionCombine = PhysicsMaterialCombine.Average,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            footGO.AddComponent<CollisionRelay>();
        }
    }
}
