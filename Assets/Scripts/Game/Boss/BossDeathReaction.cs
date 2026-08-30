using System.Collections;
using UnityEngine;

namespace Game
{
    // ボス(1〜3)撃破時の演出。最終決戦の勇者(FinalHeroDeathReaction)と同じく、回転しながら
    // 縮小しつつコインを大量にばらまいて消える。最終決戦とは違いこの後も次のラウンドが続くため、
    // 演出用に止めたカメラ追従は演出終了時に必ず元へ戻す。
    [RequireComponent(typeof(CharacterHealth))]
    public class BossDeathReaction : MonoBehaviour
    {
        [SerializeField] private float _sequenceDuration = 3f;
        [SerializeField] private float _spinSpeedDegPerSec = 360f;
        [Tooltip("見てはっきり分かるくらい多めに設定してある")]
        [SerializeField] private int _coinCount = 60;
        [SerializeField] private float _coinScatterRadius = 3.5f;
        [SerializeField] private Vector3 _cameraFocusOffset = new Vector3(0f, 3f, -8f);
        [SerializeField] private float _cameraMoveSpeed = 4f;

        CharacterHealth health;

        void Awake() => health = GetComponent<CharacterHealth>();

        void OnEnable() => health.OnDied += HandleDied;
        void OnDisable() => health.OnDied -= HandleDied;

        void HandleDied() => StartCoroutine(CoDeathSequence());

        IEnumerator CoDeathSequence()
        {
            var cameraFollower = Object.FindAnyObjectByType<RallyCameraFollower>();
            if (cameraFollower != null) cameraFollower.enabled = false;
            var cam = Camera.main;

            // 演出でカメラを動かす前の位置・向きを覚えておき、演出終了時に必ずここへ戻す
            // (WINの結果画面が出る前には操作しやすい位置に戻っている必要があるため)。
            var originalCamPos = cam != null ? cam.transform.position : default;
            var originalCamRot = cam != null ? cam.transform.rotation : default;

            var startScale = transform.localScale;
            var elapsed = 0f;
            var coinTimer = 0f;
            var coinInterval = _sequenceDuration / Mathf.Max(1, _coinCount);

            while (elapsed < _sequenceDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / _sequenceDuration);

                transform.Rotate(Vector3.up, _spinSpeedDegPerSec * Time.deltaTime, Space.World);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                if (cam != null)
                {
                    var desired = transform.position + _cameraFocusOffset;
                    cam.transform.position = Vector3.Lerp(cam.transform.position, desired, Time.deltaTime * _cameraMoveSpeed);
                    var lookDir = (transform.position + Vector3.up) - cam.transform.position;
                    if (lookDir.sqrMagnitude > 0.0001f)
                    {
                        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                            Quaternion.LookRotation(lookDir, Vector3.up), Time.deltaTime * _cameraMoveSpeed);
                    }
                }

                coinTimer -= Time.deltaTime;
                if (coinTimer <= 0f)
                {
                    coinTimer = coinInterval;
                    var offset = Random.insideUnitCircle * _coinScatterRadius;
                    CoinPickup.Spawn(transform.position + new Vector3(offset.x, 0.5f, offset.y));
                }

                yield return null;
            }

            gameObject.SetActive(false);

            // WINの結果画面が出る前に、カメラを演出開始前の位置・向きへ即座にリセットする。
            // (最終決戦と違い、この後も次のラウンドが続くため、ここで操作しやすい状態に戻しておく)
            ResetCamera(cam, originalCamPos, originalCamRot);

            if (cameraFollower != null) cameraFollower.enabled = true;
        }

        // カメラを指定した位置・向きへ即座に配置し直す(演出前の状態への復帰用)。
        static void ResetCamera(Camera cam, Vector3 position, Quaternion rotation)
        {
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
