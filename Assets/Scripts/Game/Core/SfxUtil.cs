using UnityEngine;

namespace Game
{
    // 各アビリティ/ボス攻撃に効果音をアタッチできるようにする共通ヘルパー。
    // 生成→再生→自己消滅を1回で行う(このプロジェクトの「演出は生成→自己完結→自己消滅」という
    // 既存方針、ExplosionRingEffect等と同じ)。clipが未設定(null)の場合は何もしない
    // (音源が用意できるまでは無音のまま安全に動作する)。
    public static class SfxUtil
    {
        // 戦闘カメラ(BattlefieldCameraPose、position≈(0,23,-26.2)で見下ろす構図)から戦場までの
        // 実距離が約35ユニットあり、AudioSource.PlayClipAtPointの既定減衰設定(Logarithmic、
        // minDistance=1)だとその距離でほぼ聞こえなくなってしまうため、実際のカメラ距離に合わせて
        // 減衰カーブを調整した専用のAudioSourceを都度生成する。
        const float MinAudibleDistance = 30f;
        const float MaxAudibleDistance = 150f;

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;

            var go = new GameObject("Sfx3D");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinAudibleDistance;
            source.maxDistance = MaxAudibleDistance;
            source.Play();
            Object.Destroy(go, clip.length);
        }

        // UI操作音向け。PlayAtは3D空間上の距離で減衰する(PlayClipAtPointが3DのAudioSourceを使うため)ので、
        // Canvas上のRectTransform座標をそのまま渡すとカメラからの距離次第でほぼ無音になってしまう。
        // spatialBlend=0(2D)の使い切りAudioSourceを生成し、常に同じ音量で鳴らす。
        public static void PlayUi(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var go = new GameObject("UiSfx");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.Play();
            Object.Destroy(go, clip.length);
        }
    }
}
