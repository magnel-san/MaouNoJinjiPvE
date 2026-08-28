using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Rigidbody), typeof(CharacterIdentity))]
    public class BossMovement : MonoBehaviour
    {
        [Header("移動設定")]
        [Tooltip("突進(行動パターン1/2)の速度。攻撃として避けがいがあるよう、通常移動より速くしてある")]
        [SerializeField] private float _dashSpeed = 24f;
        [Tooltip("突進の最低移動距離（距離が近くてもこの長さは突き抜けて走る）")]
        [SerializeField] private float _minDashDistance = 6f;
        [Tooltip("円運動の走る速度 (m/s) ※数値に応じてしっかり速度が変わります")]
        [SerializeField] private float _circleSpeed = 18f;
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

        [Header("移動制限エリア（円形）設定")]
        [Tooltip("円の中心位置（未設定ならVector3.zero＝マップ中央）")]
        [SerializeField] private Vector3 _areaCenter = Vector3.zero;
        [Tooltip("ボスが出られない円の半径。端っこに行かせないための封じ込め")]
        [SerializeField] private float _areaRadius = 13.5f;
        [Tooltip("壁からのマージン（ボスの半径分あけて手前で止まる）")]
        [SerializeField] private float _wallMargin = 1.5f;

        /// <summary>アリーナ円の中心（ワールド座標）。ボス専用技など他スクリプトから参照する。</summary>
        public Vector3 AreaCenter => _areaCenter;
        /// <summary>アリーナ円の実効半径（壁マージン控除後）。ボス専用技など他スクリプトから参照する。</summary>
        public float AreaRadius => Mathf.Max(0.1f, _areaRadius - _wallMargin);

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

            // 円の中心からの単純な引き戻し(ClampToAreaBounds)だと、ボスが既に境界付近にいて
            // さらに外側へ突進しようとした場合に「開始位置とほぼ同じ点」へ縮退し、
            // 突進が実質何も起きない(見た目は予告だけ出て移動しない)バグになるため、
            // 開始位置から突進方向への直線と円の交点を使って止める(必ず前進距離が残る)。
            Vector3 finalDestination = ClampDestinationToAreaBounds(startPos, rawDestination);
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

            Vector3 targetPos = ClampDestinationToAreaBounds(startPos, target.position);
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

        // ボスが円形アリーナの外へ出ないよう、中心からの距離を半径以内へクランプする
        // (端っこの方に行かないようにする、というユーザー要望に対応)。
        private Vector3 ClampToAreaBounds(Vector3 targetPosition)
        {
            float radius = AreaRadius;
            Vector3 offset = targetPosition - _areaCenter;
            offset.y = 0f;

            float dist = offset.magnitude;
            if (dist <= radius || dist < 1e-4f) return targetPosition;

            Vector3 clampedOffset = offset * (radius / dist);
            return new Vector3(_areaCenter.x + clampedOffset.x, targetPosition.y, _areaCenter.z + clampedOffset.z);
        }

        // 単発の突進/ジャンプ先を、開始位置(startPos)から見た直線上で円の境界に止める。
        // ClampToAreaBoundsと違い、開始位置がどこにあっても必ずstartPosから前進した位置になる
        // (境界付近から外側へ突進する場合に移動距離がゼロへ縮退するのを防ぐ)。
        private Vector3 ClampDestinationToAreaBounds(Vector3 startPos, Vector3 destination)
        {
            float radius = AreaRadius;

            Vector3 startOffset = startPos - _areaCenter;
            startOffset.y = 0f;
            Vector3 destOffset = destination - _areaCenter;
            destOffset.y = 0f;

            if (destOffset.magnitude <= radius) return destination; // 目的地が既に円内ならそのまま

            Vector3 delta = destOffset - startOffset;
            float segmentLength = delta.magnitude;
            if (segmentLength < 1e-4f)
            {
                // 開始位置自体が円の外(通常起こらない異常系)。中心方向へ半径分だけ戻す。
                var pulled = startOffset.sqrMagnitude > 1e-6f ? startOffset.normalized * radius : Vector3.zero;
                return new Vector3(_areaCenter.x + pulled.x, destination.y, _areaCenter.z + pulled.z);
            }
            Vector3 dir = delta / segmentLength;

            // |startOffset + dir*t| = radius を解く(t^2 + 2bt + c = 0)。startOffsetは円内にある前提なので
            // 判別式は必ず非負で、前方の交点はt = -b + sqrt(b^2 - c)。
            float b = Vector3.Dot(startOffset, dir);
            float c = startOffset.sqrMagnitude - radius * radius;
            float discriminant = Mathf.Max(0f, b * b - c);
            float t = Mathf.Clamp(-b + Mathf.Sqrt(discriminant), 0f, segmentLength);

            Vector3 result = startOffset + dir * t;
            return new Vector3(_areaCenter.x + result.x, destination.y, _areaCenter.z + result.z);
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
                    health.ApplyDamage(_contactDamage, Color.red, _identity);
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
            TargetingUtility.DrawGizmoCircle(_areaCenter, _areaRadius);
        }
    }
}
