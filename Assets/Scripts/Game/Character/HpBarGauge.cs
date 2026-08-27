using UnityEngine;

namespace Game
{
    // キャラの回転に関わらず、指定ローカルY座標・XZ平面向きで360度のHPゲージリングを表示する。
    // uGUI(Canvas)には依存せず、ランタイム生成したリング状メッシュ(Unlit)で描画する。
    [RequireComponent(typeof(CharacterHealth))]
    public class HpBarGauge : MonoBehaviour
    {
        public float LocalYOffset = 0.05f;
        public float WorldDiameter = 1.2f;
        [Range(0.05f, 0.9f)] public float RingThicknessRatio = 0.28f;
        public Color FullColor = new Color(0.2f, 0.9f, 0.2f);
        public Color EmptyColor = new Color(0.9f, 0.2f, 0.2f);
        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.4f);

        const int Segments = 48;

        static Shader cachedUnlitShader;

        CharacterHealth health;
        Transform gaugeRoot;
        MeshFilter fillMeshFilter;
        Material fillMaterial;

        void Awake()
        {
            health = GetComponent<CharacterHealth>();
            BuildGauge();
        }

        void OnEnable()
        {
            if (health != null) health.OnHPChanged += HandleHPChanged;
        }

        void OnDisable()
        {
            if (health != null) health.OnHPChanged -= HandleHPChanged;
        }

        static Shader GetUnlitShader()
        {
            if (cachedUnlitShader != null) return cachedUnlitShader;
            cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (cachedUnlitShader == null) cachedUnlitShader = Shader.Find("Unlit/Color");
            return cachedUnlitShader;
        }

        void BuildGauge()
        {
            gaugeRoot = new GameObject("HpGaugeRoot").transform;

            float outerRadius = WorldDiameter * 0.5f;
            float innerRadius = outerRadius * (1f - RingThicknessRatio);
            var shader = GetUnlitShader();

            var bgGO = new GameObject("HpBackground");
            bgGO.transform.SetParent(gaugeRoot, false);
            bgGO.AddComponent<MeshFilter>().mesh = BuildRingSegmentMesh(innerRadius, outerRadius, 1f, Segments);
            var bgRenderer = bgGO.AddComponent<MeshRenderer>();
            bgRenderer.sharedMaterial = new Material(shader) { color = BackgroundColor };
            bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;

            var fillGO = new GameObject("HpFill");
            fillGO.transform.SetParent(gaugeRoot, false);
            fillGO.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            fillMeshFilter = fillGO.AddComponent<MeshFilter>();
            fillMeshFilter.mesh = BuildRingSegmentMesh(innerRadius, outerRadius, 1f, Segments);
            var fillRenderer = fillGO.AddComponent<MeshRenderer>();
            fillMaterial = new Material(shader) { color = FullColor };
            fillRenderer.sharedMaterial = fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
        }

        // 中心から見て上(90度)を起点に時計回りへfillFraction*360度分の円環セグメントメッシュを作る。
        static Mesh BuildRingSegmentMesh(float innerRadius, float outerRadius, float fillFraction, int segments)
        {
            var mesh = new Mesh { name = "HpRingSegment" };
            fillFraction = Mathf.Clamp01(fillFraction);
            if (fillFraction <= 0f) return mesh;

            int segCount = Mathf.Max(1, Mathf.CeilToInt(segments * fillFraction));
            var vertices = new Vector3[(segCount + 1) * 2];
            // 見上げても見下ろしても見えるよう、表裏両方の面(巻き順)を生成する(両面分で12)。
            var triangles = new int[segCount * 12];
            float totalAngle = fillFraction * 360f;

            for (int i = 0; i <= segCount; i++)
            {
                float t = (float)i / segCount;
                float angleDeg = 90f - t * totalAngle;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                vertices[i * 2] = dir * innerRadius;
                vertices[i * 2 + 1] = dir * outerRadius;
            }

            int triIndex = 0;
            for (int i = 0; i < segCount; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = (i + 1) * 2;
                int i3 = (i + 1) * 2 + 1;

                // 表面
                triangles[triIndex++] = i0;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i1;

                triangles[triIndex++] = i1;
                triangles[triIndex++] = i2;
                triangles[triIndex++] = i3;

                // 裏面 (巻き順を逆にして反対側からも見えるようにする)
                triangles[triIndex++] = i0;
                triangles[triIndex++] = i1;
                triangles[triIndex++] = i2;

                triangles[triIndex++] = i1;
                triangles[triIndex++] = i3;
                triangles[triIndex++] = i2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void HandleHPChanged(float current, float max)
        {
            if (fillMeshFilter == null) return;
            float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            float outerRadius = WorldDiameter * 0.5f;
            float innerRadius = outerRadius * (1f - RingThicknessRatio);
            fillMeshFilter.mesh = BuildRingSegmentMesh(innerRadius, outerRadius, pct, Segments);
            fillMaterial.color = Color.Lerp(EmptyColor, FullColor, pct);
        }

        void LateUpdate()
        {
            if (gaugeRoot == null) return;
            gaugeRoot.position = transform.position + Vector3.up * LocalYOffset;
            gaugeRoot.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void OnDestroy()
        {
            if (gaugeRoot != null) Destroy(gaugeRoot.gameObject);
        }
    }
}
