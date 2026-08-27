using UnityEngine;

namespace Game
{
    // 集合(Rally)コマンド中、対象地点に地面沿いの円を表示する。ExplosionRingEffect等と同じ
    // 「LineRendererで実行時に円を生成する」演出パターンを踏襲した、シーン常駐の単一インスタンス。
    public class RallyCircleIndicator : MonoBehaviour
    {
        const int Segments = 48;
        const float LineWidth = 0.2f;
        const float GroundY = 0.05f;
        static readonly Color RingColor = new Color(0.3f, 0.9f, 1f, 0.9f);

        static RallyCircleIndicator _instance;

        LineRenderer _line;

        // 呼ぶだけでよい。既に存在する場合は何もしない(複数の入力コントローラーから安全に呼べる)。
        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("RallyCircleIndicator");
            _instance = go.AddComponent<RallyCircleIndicator>();
        }

        void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.widthMultiplier = LineWidth;
            _line.positionCount = Segments;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.material = new Material(VfxShaderUtil.GetUnlitShader()) { color = RingColor };
            _line.enabled = false;
        }

        void Update()
        {
            var show = BattleCommandState.CommandType == PlayerCommandType.Rally;
            _line.enabled = show;
            if (!show) return;

            var center = BattleCommandState.RallyWorldPosition;
            var radius = Mathf.Max(BattleCommandState.RallyRadius, 0.1f);
            for (var i = 0; i < Segments; i++)
            {
                var t = (float)i / Segments * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(center.x + Mathf.Cos(t) * radius, GroundY, center.z + Mathf.Sin(t) * radius));
            }
        }
    }
}
