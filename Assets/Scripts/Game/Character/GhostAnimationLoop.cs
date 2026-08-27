using UnityEngine;

namespace Game
{
    // 見た目(ゴーストオブジェクト)にアニメーションが存在する場合、自動でループ再生する。
    public class GhostAnimationLoop : MonoBehaviour
    {
        void Start()
        {
            var legacyAnimation = GetComponentInChildren<Animation>();
            if (legacyAnimation != null && legacyAnimation.clip != null)
            {
                legacyAnimation.wrapMode = WrapMode.Loop;
                legacyAnimation.clip.wrapMode = WrapMode.Loop;
                legacyAnimation.Play();
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.speed = animator.speed <= 0f ? 1f : animator.speed;
            }
        }
    }
}
