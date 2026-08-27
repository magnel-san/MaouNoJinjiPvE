using UnityEngine;

namespace Game
{
    // ゲーム全体で流し続けるBGMを再生する。SfxUtil(単発効果音を鳴らして消える方式)とは別に、
    // 1トラックをループ再生し続けるだけのシンプルな実装。シーンに1つ置いておけば、
    // シーン読み込み時から自動でBGMが流れる(Awake生成のAudioSourceで自己完結)。
    public class BgmPlayer : MonoBehaviour
    {
        [Tooltip("ループ再生するBGM。未設定なら何も再生しない")]
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;
        [SerializeField] private bool _playOnAwake = true;

        AudioSource source;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = _bgmClip;
            source.loop = true;
            source.volume = _volume;
            source.playOnAwake = false;

            if (_playOnAwake) Play();
        }

        public void Play()
        {
            if (source.clip == null) return;
            source.Play();
        }

        public void Stop() => source.Stop();

        // 曲を差し替えたい場合(ボス戦だけ別曲にする等)に外部から呼べるようにしておく。
        public void SetClip(AudioClip clip, bool playImmediately = true)
        {
            source.clip = clip;
            if (playImmediately) Play();
        }

        public void SetVolume(float volume) => source.volume = Mathf.Clamp01(volume);
    }
}
