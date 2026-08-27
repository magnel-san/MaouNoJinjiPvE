using System;
using UnityEngine;

namespace Game
{
    // 足元コライダーなど、Rigidbodyを持たない子コライダーの衝突イベントを親スクリプトへ中継する。
    public class CollisionRelay : MonoBehaviour
    {
        public event Action<Collision> Enter;
        public event Action<Collision> Stay;

        void OnCollisionEnter(Collision collision) => Enter?.Invoke(collision);
        void OnCollisionStay(Collision collision) => Stay?.Invoke(collision);
    }
}
