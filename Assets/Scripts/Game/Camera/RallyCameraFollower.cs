using UnityEngine;

namespace Game
{
    /// <summary>
    /// 魔王側のモンスター（PlayerCommandIntentSourceを持つキャラ）のみを追従・ズーム対象にするカメラコントローラー
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RallyCameraFollower : MonoBehaviour
    {
        [Header("カメラのオフセット設定（距離調整）")]
        [Tooltip("魔王側モンスターの重心地点から、カメラをどれくらい離すか")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 12f, -10f);

        [Header("カメラの注視割合")]
        [Tooltip("0 = モンスター重心を中央, 1 = 円を中央, 0.75 = かなり円寄り")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusRatio = 0.75f;

        [Header("カメラ移動の滑らかさ")]
        [SerializeField] private float _smoothSpeed = 5f;

        [Header("自動ズーム（カメラ引き）設定")]
        [Tooltip("通常の視野角（寄りの画角）")]
        [SerializeField] private float _baseFov = 45f;
        [Tooltip("画面外へ出そうな時に引き伸ばす最大視野角")]
        [SerializeField] private float _maxFov = 75f;
        [Tooltip("ズーム変化の滑らかさ")]
        [SerializeField] private float _zoomSmoothSpeed = 3f;
        [Tooltip("画面端と判定するマージン（0.1 = 画面端10%の手前）")]
        [SerializeField] private float _screenMargin = 0.1f;

        private Camera _cam;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.fieldOfView = _baseFov;
            }

            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            // 1. 位置の更新（魔王側モンスターのみ追従）
            UpdateCameraPosition();

            // 2. 魔王側モンスターが画面外に出そうな時だけ動的ズームアウト
            UpdateDynamicZoom();
        }

        /// <summary>
        /// リトライ時やクリア時にカメラの位置・画角を初期化する
        /// </summary>
        public void ResetCameraPosition()
        {
            if (TryGetDemonMonstersCentroid(out Vector3 monsterCentroid))
            {
                transform.position = monsterCentroid + _cameraOffset;
            }
            else
            {
                transform.position = _initialPosition;
                transform.rotation = _initialRotation;
            }

            if (_cam != null)
            {
                _cam.fieldOfView = _baseFov;
            }
        }

        private void UpdateCameraPosition()
        {
            // // 魔王側のモンスター群の重心を取得（勇者は無視）
            // if (!TryGetDemonMonstersCentroid(out Vector3 focusBasePos))
            // {
            //     return; // 魔王側モンスターがいなければ現在位置を維持
            // }

            // Vector3 targetPos;
            // bool isRallyActive = BattleCommandState.CommandType == PlayerCommandType.Rally;

            // if (isRallyActive)
            // {
            //     Vector3 rallyPos = BattleCommandState.RallyWorldPosition;
            //     Vector3 focusPoint = Vector3.Lerp(focusBasePos, rallyPos, _focusRatio);
            //     targetPos = focusPoint + _cameraOffset;
            // }
            // else
            // {
            //     targetPos = focusBasePos + _cameraOffset;
            // }

            // transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _smoothSpeed);
            if (!TryGetDemonMonstersCentroid(out Vector3 focusBasePos))
            {
                return; 
            }

            // ラリー中かどうかに関係なく、常にモンスターの重心位置 + オフセットを目標位置にする
            Vector3 targetPos = focusBasePos + _cameraOffset;

            // カメラ位置を滑らかに移動
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _smoothSpeed);

        }

        /// <summary>
        /// 生存しており、かつ PlayerCommandIntentSource を持っている「魔王側モンスター」のみの重心を計算
        /// </summary>
        private bool TryGetDemonMonstersCentroid(out Vector3 centroid)
        {
            centroid = Vector3.zero;
            int count = 0;
            var healths = FindObjectsByType<CharacterHealth>(FindObjectsInactive.Exclude);

            foreach (var hp in healths)
            {
                // 非生存、または 魔王側モンスター（PlayerCommandIntentSource持ち）でない場合は除外
                if (!hp.IsAlive || !IsDemonMonster(hp)) continue;

                centroid += hp.transform.position;
                count++;
            }

            if (count == 0) return false;

            centroid /= count;
            return true;
        }

        private void UpdateDynamicZoom()
        {
            if (_cam == null) return;

            var allHealths = FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
            float targetFov = _baseFov;
            float maxOutDistance = 0f;

            foreach (var hp in allHealths)
            {
                // 非生存、または 魔王側モンスターでない場合はズーム計算から除外
                if (!hp.IsAlive || !IsDemonMonster(hp)) continue;

                Vector3 viewportPos = _cam.WorldToViewportPoint(hp.transform.position);
                if (viewportPos.z < 0) continue;

                float minLimit = _screenMargin;
                float maxLimit = 1f - _screenMargin;

                float xOut = 0f;
                if (viewportPos.x < minLimit) xOut = minLimit - viewportPos.x;
                else if (viewportPos.x > maxLimit) xOut = viewportPos.x - maxLimit;

                float yOut = 0f;
                if (viewportPos.y < minLimit) yOut = minLimit - viewportPos.y;
                else if (viewportPos.y > maxLimit) yOut = viewportPos.y - maxLimit;

                float totalOut = Mathf.Max(xOut, yOut);
                if (totalOut > maxOutDistance)
                {
                    maxOutDistance = totalOut;
                }
            }

            if (maxOutDistance > 0f)
            {
                targetFov = Mathf.Lerp(_baseFov, _maxFov, maxOutDistance * 4f);
            }

            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * _zoomSmoothSpeed);
        }

        /// <summary>
        /// PlayerCommandIntentSource を持っているかをチェックして魔王側のモンスターか判定する
        /// </summary>
        private bool IsDemonMonster(CharacterHealth hp)
        {
            return hp.GetComponent<PlayerCommandIntentSource>() != null;
        }
    }
}
