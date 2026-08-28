using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Rigidbody), typeof(CharacterIdentity))]
    public class BossMovement : MonoBehaviour
    {
        [Header("移動設定")]
        [SerializeField] private float _dashSpeed = 20f;
        [Tooltip("突進の最低移動距離（距離が近くてもこの長さは突き抜けて走る）")]
        [SerializeField] private float _minDashDistance = 6f; 
        [Tooltip("円運動の走る速度 (m/s) ※数値に応じてしっかり速度が変わります")]
        [SerializeField] private float _circleSpeed = 2000f; 
        [Tooltip("ジャンプの最高高度")]
        [SerializeField] private float _jumpHeight = 10f;
        [SerializeField] private float _jumpDuration = 1.2f;

        [Header("予兆（警告）時間設定")]
        [Tooltip("突進前：赤い警告ラインを出してから突進するまでの待機時間（秒）")]
        [SerializeField] private float _dashWarningDuration = 1.5f;
        [Tooltip("円運動前：赤い円周ラインを出してから走り出すまでの待機時間（秒）")]
        [SerializeField] private float _circleWarningDuration = 1.5f;
        [Tooltip("ジャンプ前：着地予兆エリアを出してから跳び上がるまでの逃げ時間（秒）")]
        [SerializeField] private float _jumpWarningDuration = 1.0f;

        [Header("移動制限エリア（四角い枠）設定")]
        [Tooltip("枠の中心位置（未設定ならVector3.zero＝マップ中央）")]
        [SerializeField] private Vector3 _areaCenter = Vector3.zero;
        [Tooltip("移動可能エリアの横幅(X)と奥行き(Z)")]
        [SerializeField] private Vector2 _areaSize = new Vector2(30f, 30f);
        [Tooltip("壁からのマージン（ボスの半径分あけて手前で止まる）")]
        [SerializeField] private float _wallMargin = 1.5f;

        [Header("円運動時の走行時接触ノックバック設定")]
        [SerializeField] private float _contactDamage = 10f;
        [SerializeField] private float _contactKnockback = 15f;
        [SerializeField] private float _contactRadius = 1.2f;

        [Header("直線突進時の超微量ノックバック設定")]
        [SerializeField] private float _dashTinyKnockback = 3.0f;

        private Rigidbody _rb;
        private CharacterIdentity _identity;
        private Vector3 _centerPosition;
        private bool _isCircleKnockback = false;
        private bool _isDashKnockback = false;
        
        private LineRenderer _indicatorLine;
        private GameObject _circleAreaObject; // 塗りつぶし円エリア用オブジェクト

        public bool IsMoving { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _identity = GetComponent<CharacterIdentity>();
            SetupLineRenderer();
            SetupCircleAreaObject();
        }

        void Start()
        {
            _centerPosition = transform.position;
        }

        private void SetupLineRenderer()
        {
            _indicatorLine = gameObject.AddComponent<LineRenderer>();
            _indicatorLine.startWidth = 1.6f;
            _indicatorLine.endWidth = 1.6f;
            _indicatorLine.enabled = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _indicatorLine.material = new Material(shader);
            }
            Color redColor = new Color(1f, 0.1f, 0.1f, 0.6f);
            _indicatorLine.startColor = redColor;
            _indicatorLine.endColor = redColor;
        }

        // 塗りつぶし用の「赤い円形エリア」を動的に構築
        private void SetupCircleAreaObject()
        {
            _circleAreaObject = new GameObject("JumpLandingWarningZone");
            _circleAreaObject.transform.SetParent(transform);

            MeshFilter mf = _circleAreaObject.AddComponent<MeshFilter>();
            MeshRenderer mr = _circleAreaObject.AddComponent<MeshRenderer>();

            // 36分割の円ポリゴンメッシュを自動作成
            Mesh mesh = new Mesh();
            int segments = 36;
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i == segments - 1) ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            // 半透明の赤マテリアルを設定
            Shader shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent"));
            mat.color = new Color(1f, 0.15f, 0.15f, 0.45f); // 半透明の赤
            mr.material = mat;

            _circleAreaObject.SetActive(false);
        }

        void FixedUpdate()
        {
            if (_isCircleKnockback)
            {
                CheckContactKnockback(_contactKnockback);
            }
            else if (_isDashKnockback)
            {
                CheckContactKnockback(_dashTinyKnockback);
            }
        }

        // --- 行動パターン 1 & 2: 直線突進 ---
        public IEnumerator CoDashToTarget(Transform target)
        {
            if (target == null) yield break;

            IsMoving = true;

            Vector3 startPos = transform.position;
            Vector3 targetPos = target.position;
            startPos.y = transform.position.y;
            targetPos.y = transform.position.y;

            Vector3 dir = (targetPos - startPos).normalized;
            if (dir == Vector3.zero) dir = transform.forward;

            transform.forward = dir;

            float rawDistance = Vector3.Distance(startPos, targetPos);
            float actualDistance = Mathf.Max(rawDistance, _minDashDistance);
            Vector3 rawDestination = startPos + dir * actualDistance;

            Vector3 finalDestination = ClampToAreaBounds(rawDestination);
            float finalDistance = Vector3.Distance(startPos, finalDestination);

            _indicatorLine.positionCount = 2;
            _indicatorLine.SetPosition(0, startPos + Vector3.up * 0.1f);
            _indicatorLine.SetPosition(1, finalDestination + Vector3.up * 0.1f);
            _indicatorLine.enabled = true;

            // 予告表示時間を可変に（_dashWarningDuration 秒待機）
            yield return new WaitForSeconds(_dashWarningDuration);

            _indicatorLine.enabled = false;
            _isDashKnockback = true;

            float traveled = 0f;
            while (traveled < finalDistance && finalDistance > 0.01f)
            {
                float step = _dashSpeed * Time.deltaTime;
                Vector3 nextPos = Vector3.MoveTowards(transform.position, finalDestination, step);
                traveled += step;

                _rb.MovePosition(nextPos);
                yield return null;
            }

            _isDashKnockback = false;
            IsMoving = false;
        }

        // --- 行動パターン 3: 自然に円周上へ移動してから1周走る（速度反映補正版） ---
        public IEnumerator CoRunCircleAroundCenter(float radius)
        {
            IsMoving = true;

            Vector3 offsetFromCenter = transform.position - _centerPosition;
            offsetFromCenter.y = 0;
            if (offsetFromCenter == Vector3.zero) offsetFromCenter = Vector3.forward;

            float closestAngle = Mathf.Atan2(offsetFromCenter.z, offsetFromCenter.x);
            Vector3 entryPoint = _centerPosition + new Vector3(Mathf.Cos(closestAngle), 0f, Mathf.Sin(closestAngle)) * radius;
            entryPoint = ClampToAreaBounds(entryPoint);

            // 予兆ライン表示
            int segments = 36;
            _indicatorLine.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = closestAngle + (i / (float)segments) * Mathf.PI * 2f;
                Vector3 point = _centerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                point = ClampToAreaBounds(point);
                point.y = transform.position.y + 0.1f;
                _indicatorLine.SetPosition(i, point);
            }
            _indicatorLine.enabled = true;

            // 予告表示時間を可変に（_circleWarningDuration 秒待機）
            yield return new WaitForSeconds(_circleWarningDuration);
            _indicatorLine.enabled = false;

            // 1. 円周上の入口地点まで移動
            _isCircleKnockback = true;
            while (Vector3.Distance(transform.position, entryPoint) > 0.2f)
            {
                Vector3 moveDir = (entryPoint - transform.position).normalized;
                if (moveDir != Vector3.zero) transform.forward = moveDir;

                Vector3 nextPos = Vector3.MoveTowards(transform.position, entryPoint, _circleSpeed * Time.deltaTime);
                _rb.MovePosition(nextPos);
                yield return null;
            }

            // 2. 円周上を _circleSpeed (m/s) の速度で1周走る
            float currentAngle = closestAngle;
            float totalTraveledAngle = 0f;

            while (totalTraveledAngle < Mathf.PI * 2f)
            {
                // 実速度 (_circleSpeed) に直接比例して角度を進める計算
                float deltaAngle = (_circleSpeed / Mathf.Max(radius, 0.1f)) * Time.deltaTime;
                totalTraveledAngle += deltaAngle;
                currentAngle += deltaAngle;

                Vector3 targetCirclePos = _centerPosition + new Vector3(Mathf.Cos(currentAngle), 0f, Mathf.Sin(currentAngle)) * radius;
                targetCirclePos = ClampToAreaBounds(targetCirclePos);
                targetCirclePos.y = transform.position.y;

                Vector3 moveDir = (targetCirclePos - transform.position).normalized;
                if (moveDir != Vector3.zero) transform.forward = moveDir;

                Vector3 nextPos = Vector3.MoveTowards(transform.position, targetCirclePos, _circleSpeed * Time.deltaTime);
                _rb.MovePosition(nextPos);
                yield return null;
            }

            _isCircleKnockback = false;
            IsMoving = false;
        }

        // --- 行動パターン 4: 着地地点に塗りつぶし円エリアを出して大ジャンプ ---
        public IEnumerator CoJumpToTarget(Transform target, System.Action onLandingCallback)
        {
            if (target == null) yield break;

            IsMoving = true;
            Vector3 startPos = transform.position;
            
            Vector3 targetPos = ClampToAreaBounds(target.position);
            targetPos.y = startPos.y;

            // 1. 着地地点に「赤い丸い面エリア」を表示
            float landingRadius = 1.8f; // 円の半径
            _circleAreaObject.transform.position = targetPos + Vector3.up * 0.05f;
            _circleAreaObject.transform.localScale = new Vector3(landingRadius, 1f, landingRadius);
            _circleAreaObject.transform.SetParent(null); // 親から離して固定
            _circleAreaObject.SetActive(true);

            // 2. 逃げる時間（溜め動作）を与えるため、ジャンプを開始せずに待機する
            if (_jumpWarningDuration > 0f)
            {
                // targetPosの方を向いて溜める演出
                Vector3 lookDir = (targetPos - startPos).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero) transform.forward = lookDir;

                yield return new WaitForSeconds(_jumpWarningDuration);
            }

            // 3. 待機が終わったら大ジャンプ開始
            float elapsed = 0f;
            while (elapsed < _jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _jumpDuration;

                Vector3 currentXZ = Vector3.Lerp(startPos, targetPos, t);
                float yOffset = 4f * _jumpHeight * t * (1f - t);
                currentXZ.y = startPos.y + yOffset;

                _rb.MovePosition(currentXZ);
                yield return null;
            }

            // 着地時にエリア表示を消す
            _circleAreaObject.SetActive(false);
            _circleAreaObject.transform.SetParent(transform);

            transform.position = targetPos;
            IsMoving = false;

            onLandingCallback?.Invoke();
        }

        private Vector3 ClampToAreaBounds(Vector3 targetPosition)
        {
            float minX = _areaCenter.x - (_areaSize.x * 0.5f) + _wallMargin;
            float maxX = _areaCenter.x + (_areaSize.x * 0.5f) - _wallMargin;
            float minZ = _areaCenter.z - (_areaSize.y * 0.5f) + _wallMargin;
            float maxZ = _areaCenter.z + (_areaSize.y * 0.5f) - _wallMargin;

            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);

            return targetPosition;
        }

        private void CheckContactKnockback(float force)
        {
            var hits = Physics.OverlapSphere(transform.position, _contactRadius);
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == _identity) continue;
                if (targetIdentity.Team == _identity.Team) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health != null && health.IsAlive)
                {
                    health.ApplyDamage(_contactDamage, Color.red);
                }

                var rb = targetIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 pushDir = targetIdentity.transform.position - transform.position;
                    pushDir.y = 0.05f;
                    rb.AddForce(pushDir.normalized * force, ForceMode.VelocityChange);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 size = new Vector3(_areaSize.x, 0.1f, _areaSize.y);
            Gizmos.DrawWireCube(_areaCenter, size);
        }
    }
}
