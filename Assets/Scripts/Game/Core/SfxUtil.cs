using UnityEngine;

namespace Game
{
    // 各アビリティ/ボス攻撃に効果音をアタッチできるようにする共通ヘルパー。
    // AudioSource.PlayClipAtPointは呼ぶだけで一時オブジェクトの生成・再生後の自動破棄までやってくれるため、
    // このプロジェクトの「演出は生成→自己完結→自己消滅」という既存方針(ExplosionRingEffect等)に合う。
    // clipが未設定(null)の場合は何もしない(音源が用意できるまでは無音のまま安全に動作する)。
    public static class SfxUtil
    {
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
