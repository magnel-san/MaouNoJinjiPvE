using System.Collections;
using UnityEngine;

namespace Game
{
    // 最終決戦の勇者がHP0になった瞬間の演出: カメラを勇者へフォーカスし、回転しながら
    // 縮小してコインをばらまき、最後に消える。
    // CharacterHealth.Die()が移動系コンポーネントを一括で無効化する対象(CharacterMovement等)に
    // このクラスは含まれていないため、死後もこの演出だけは安全に動作する。
    [RequireComponent(typeof(CharacterHealth))]
    public class FinalHeroDeathReaction : MonoBehaviour
    {
        [SerializeField] private float _sequenceDuration = 2.5f;
        [SerializeField] private float _spinSpeedDegPerSec = 360f;
        [SerializeField] private int _coinCount = 40;
        [SerializeField] private float _coinScatterRadius = 3f;
        [SerializeField] private Vector3 _cameraFocusOffset = new Vector3(0f, 3f, -8f);
        [SerializeField] private float _cameraMoveSpeed = 4f;
        [SerializeField] private Animator _animator;

        const string AnimKnockback = "Knockback";

        CharacterHealth health;

        public bool SequenceFinished { get; private set; }

        void Awake()
        {
            health = GetComponent<CharacterHealth>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        void OnEnable() => health.OnDied += HandleDied;
        void OnDisable() => health.OnDied -= HandleDied;

        void HandleDied()
        {
            if (_animator != null) _animator.SetTrigger(AnimKnockback);
            CoinPickup.CollectAll(); // 演出開始時点で場に残っている全てのコインを即座に回収する
            StartCoroutine(CoDeathSequence());
        }

        IEnumerator CoDeathSequence()
        {
            var cameraFollower = Object.FindAnyObjectByType<RallyCameraFollower>();
            if (cameraFollower != null) cameraFollower.enabled = false;
            var cam = Camera.main;

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
            SequenceFinished = true;
        }
    }
}
