using UnityEngine;

namespace Game
{
    // 爆発・衝撃波等の大きな一撃でカメラを揺らす。Camera.mainへ遅延アタッチし、
    // 外部要因によるカメラ位置(base)は壊さず、揺れ分のオフセットだけを毎フレーム加算/除去する
    // (前フレームに加えたオフセットを引いてから新しいオフセットを足すことで、外部が動かした位置に自己補正する)。
    // 複数の揺れが短時間に重なっても破綻しないよう、瞬間的な強さの合算ではなく
    // 「trauma」(0〜1、時間経過で減衰)を積み上げるトラウマベース方式にしている。
    public class CameraShake : MonoBehaviour
    {
        public float Decay = 1.4f;
        public float MaxOffset = 0.35f;
        public float Frequency = 18f;

        static CameraShake _instance;

        float _trauma;
        float _seed;
        Vector3 _lastOffset;

        public static void Shake(float intensity)
        {
            var instance = GetOrCreate();
            if (instance == null) return;
            instance._trauma = Mathf.Clamp01(instance._trauma + Mathf.Clamp01(intensity));
        }

        static CameraShake GetOrCreate()
        {
            if (_instance != null) return _instance;

            var cam = Camera.main;
            if (cam == null) return null;

            _instance = cam.GetComponent<CameraShake>();
            if (_instance == null)
            {
                _instance = cam.gameObject.AddComponent<CameraShake>();
                _instance._seed = Random.Range(0f, 1000f);
            }
            return _instance;
        }

        void LateUpdate()
        {
            if (_trauma <= 0f && _lastOffset == Vector3.zero) return;

            _trauma = Mathf.Max(0f, _trauma - Decay * Time.deltaTime);
            var shake = _trauma * _trauma;

            var offset = shake <= 0f ? Vector3.zero : new Vector3(
                (Mathf.PerlinNoise(_seed, Time.time * Frequency) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 50f, Time.time * Frequency) - 0.5f) * 2f,
                0f) * (MaxOffset * shake);

            transform.position += offset - _lastOffset;
            _lastOffset = offset;
        }
    }
}
