using UnityEngine;

namespace Game
{
    public class RallyCameraFollower : MonoBehaviour
    {
        [Header("検索対象のオブジェクト名（プレハブ生成後の名前）")]
        [SerializeField] private string _bossObjectName = "Boss";

        [Header("カメラのオフセット設定")]
        [Tooltip("勇者と円の中間地点から、カメラをどれくらい離すか")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 18f, -15f);

        [Header("カメラの注視割合")]
        [Tooltip("0 = 勇者を中央, 1 = 円を中央, 0.75 = かなり円寄り")]
        [Range(0f, 1f)]
        [SerializeField] private float _focusRatio = 0.75f; // ★ 0.5から0.75（円寄り）に変更

        [Header("カメラ移動の滑らかさ")]
        [SerializeField] private float _smoothSpeed = 5f;

        [Header("自動ズーム（カメラ引き）設定")]
        [Tooltip("通常の視野角（画角）")]
        [SerializeField] private float _baseFov = 60f;
        [Tooltip("画面外へ出そうな時に引き伸ばす最大視野角")]
        [SerializeField] private float _maxFov = 85f;
        [Tooltip("ズーム変化の滑らかさ")]
        [SerializeField] private float _zoomSmoothSpeed = 3f;
        [Tooltip("画面端と判定するマージン（0.1 = 画面端10%の手前）")]
        [SerializeField] private float _screenMargin = 0.1f;

        private BossController _boss;
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.fieldOfView = _baseFov;
            }
        }

        public void SetBossTarget(BossController boss)
        {
            _boss = boss;
        }

        private void LateUpdate()
        {
            if (_boss == null) FindBossInstance();

            // 1. 位置の更新(ボス出現前の練習フェーズ等でボスが見つからない間は、
            //    生存しているキャラ全員の重心を追従することでカメラが完全に停止しないようにする)。
            UpdateCameraPosition();

            // 2. モンスターが画面外に出ないよう画角（FOV）を自動ズームアウト調整
            UpdateDynamicZoom();
        }

        private void FindBossInstance()
        {
            _boss = FindFirstObjectByType<BossController>();

            if (_boss == null && !string.IsNullOrEmpty(_bossObjectName))
            {
                var bossGo = GameObject.Find(_bossObjectName);
                if (bossGo != null)
                {
                    _boss = bossGo.GetComponent<BossController>();
                }
            }
        }

        private void UpdateCameraPosition()
        {
            Vector3 focusBasePos;
            if (_boss != null)
            {
                focusBasePos = _boss.transform.position;
            }
            else if (!TryGetLivingCharactersCentroid(out focusBasePos))
            {
                return; // 追従対象が(ボスも生存キャラも)何も無ければ、現在位置のまま何もしない
            }

            Vector3 targetPos;
            bool isRallyActive = BattleCommandState.CommandType == PlayerCommandType.Rally;

            if (isRallyActive)
            {
                Vector3 rallyPos = BattleCommandState.RallyWorldPosition;
                // _focusRatio が 0.75 のため、円のカーソル側にぐっと寄った位置をカメラが注視します
                Vector3 focusPoint = Vector3.Lerp(focusBasePos, rallyPos, _focusRatio);
                targetPos = focusPoint + _cameraOffset;
            }
            else
            {
                targetPos = focusBasePos + _cameraOffset;
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _smoothSpeed);
        }

        // ボスがまだ出現していない(練習フェーズ等)場合のフォールバック: 生存している全キャラ
        // (プレイヤー側モンスター・練習用の雑魚等)の重心を返す。1体も居なければfalseを返す。
        private bool TryGetLivingCharactersCentroid(out Vector3 centroid)
        {
            centroid = Vector3.zero;
            var count = 0;
            var healths = FindObjectsByType<CharacterHealth>(FindObjectsInactive.Exclude);
            foreach (var hp in healths)
            {
                if (!hp.IsAlive) continue;
                centroid += hp.transform.position;
                count++;
            }
            if (count == 0) return false;
            centroid /= count;
            return true;
        }

        // モンスターが画面外へ行った際にカメラを引く（FOVを広げる）処理
        private void UpdateDynamicZoom()
        {
            if (_cam == null) return;

            // 生存している全キャラクター（モンスター等）を取得
            var allHealths = FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
            
            float targetFov = _baseFov;
            float maxOutDistance = 0f;

            foreach (var hp in allHealths)
            {
                if (!hp.IsAlive) continue;

                // 3D空間座標を画面上の2D比率座標（0.0〜1.0）に変換
                Vector3 viewportPos = _cam.WorldToViewportPoint(hp.transform.position);

                // カメラの後ろにいる場合はスキップ
                if (viewportPos.z < 0) continue;

                // 画面端（マージン考慮）からの食み出し具合を計算
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

            // 画面外にモンスターが出そうであれば、食み出し量に応じてFOVを拡大（カメラを引き）する
            if (maxOutDistance > 0f)
            {
                targetFov = Mathf.Lerp(_baseFov, _maxFov, maxOutDistance * 4f); // 4倍補正で素早く引く
            }

            // FOV（視野角）を滑らかに変更
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * _zoomSmoothSpeed);
        }
    }
}
