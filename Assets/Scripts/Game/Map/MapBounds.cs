using UnityEngine;

namespace Game
{
    // マップは矩形。左下(MinCorner)〜右上(MaxCorner)の座標(x,z)で場外を定義する。
    public class MapBounds : MonoBehaviour
    {
        [Header("場外の座標 (左下 x,z / 右上 x,z)")]
        public Vector2 MinCorner = new Vector2(-20f, -20f);
        public Vector2 MaxCorner = new Vector2(20f, 20f);

        [Header("場外から内側への危険地帯の間隔")]
        public float DangerMargin = 3f;

        [Header("落下死のY座標")]
        public float FallDeathY = -10f;

        [Header("ゲーム中の表示 (LineRendererで実際にGame View/ビルドにも表示する)")]
        [SerializeField] private bool _showBoundsInGame = true;
        [SerializeField] private float _lineWidth = 0.3f;
        [SerializeField] private Color _boundaryColor = Color.red;
        [SerializeField] private Color _dangerZoneColor = Color.yellow;

        static Shader cachedUnlitShader;

        void Awake()
        {
            if (!_showBoundsInGame) return;

            BuildLine("BoundaryLine", MinCorner, MaxCorner, _boundaryColor);
            BuildLine("DangerZoneLine",
                MinCorner + new Vector2(DangerMargin, DangerMargin),
                MaxCorner - new Vector2(DangerMargin, DangerMargin),
                _dangerZoneColor);
        }

        void BuildLine(string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = _lineWidth;
            lr.positionCount = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(GetUnlitShader()) { color = color };

            float y = transform.position.y + 0.05f;
            lr.SetPosition(0, new Vector3(min.x, y, min.y));
            lr.SetPosition(1, new Vector3(max.x, y, min.y));
            lr.SetPosition(2, new Vector3(max.x, y, max.y));
            lr.SetPosition(3, new Vector3(min.x, y, max.y));
        }

        static Shader GetUnlitShader()
        {
            if (cachedUnlitShader != null) return cachedUnlitShader;
            cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (cachedUnlitShader == null) cachedUnlitShader = Shader.Find("Unlit/Color");
            return cachedUnlitShader;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = _boundaryColor;
            DrawRect(MinCorner, MaxCorner);

            Gizmos.color = _dangerZoneColor;
            DrawRect(MinCorner + new Vector2(DangerMargin, DangerMargin), MaxCorner - new Vector2(DangerMargin, DangerMargin));
        }

        void DrawRect(Vector2 min, Vector2 max)
        {
            Vector3 a = new Vector3(min.x, transform.position.y, min.y);
            Vector3 b = new Vector3(max.x, transform.position.y, min.y);
            Vector3 c = new Vector3(max.x, transform.position.y, max.y);
            Vector3 d = new Vector3(min.x, transform.position.y, max.y);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
