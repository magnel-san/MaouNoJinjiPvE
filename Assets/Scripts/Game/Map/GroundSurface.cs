using UnityEngine;

namespace Game
{
    // キャラクターが立つ地面用。適切な摩擦・反発を持つPhysicsMaterialをコライダーに設定する。
    [RequireComponent(typeof(Collider))]
    public class GroundSurface : MonoBehaviour
    {
        [Range(0f, 1f)] public float Friction = 0.4f;
        [Range(0f, 1f)] public float Bounciness = 0f;

        void Awake()
        {
            var col = GetComponent<Collider>();
            var mat = new PhysicsMaterial("GroundMaterial")
            {
                dynamicFriction = Friction,
                staticFriction = Friction,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounciness = Bounciness,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            col.sharedMaterial = mat;
        }
    }
}
