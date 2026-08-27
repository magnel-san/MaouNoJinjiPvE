using UnityEngine;

namespace Game
{
    // ChainLightningAbilityの1ホップ分の見た目。2点間をランダムにジグザグさせたLineRendererを
    // 一瞬だけ表示してフェードアウトする(ExplosionRingEffectと同じ「実行時生成→自己消滅」の演出パターン)。
    public class LightningBoltEffect : MonoBehaviour
    {
        const int Segments = 8;

        LineRenderer line;
        float duration;
        float elapsed;
        Color baseColor;

        public static void Spawn(Vector3 from, Vector3 to, Color color, float duration = 0.15f)
        {
            var go = new GameObject("LightningBolt");
            go.AddComponent<LightningBoltEffect>().Initialize(from, to, color, duration);
        }

        void Initialize(Vector3 from, Vector3 to, Color color, float durationSeconds)
        {
            duration = Mathf.Max(0.05f, durationSeconds);
            baseColor = color;

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = Segments + 1;
            line.widthMultiplier = 0.08f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(VfxShaderUtil.GetUnlitShader()) { color = color };
            line.startColor = color;
            line.endColor = color;

            var direction = to - from;
            var length = direction.magnitude;
            var forward = length > 0.0001f ? direction / length : Vector3.forward;
            var perpendicular = Vector3.Cross(forward, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.0001f) perpendicular = Vector3.right;
            perpendicular.Normalize();

            for (var i = 0; i <= Segments; i++)
            {
                var t = (float)i / Segments;
                var point = Vector3.Lerp(from, to, t);
                if (i > 0 && i < Segments)
                {
                    point += perpendicular * ((Random.value - 0.5f) * 0.4f) + Vector3.up * ((Random.value - 0.5f) * 0.3f);
                }
                line.SetPosition(i, point);
            }

            CombatFx.ImpactBurst(to, color, 0.18f);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);

            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            line.startColor = c;
            line.endColor = c;

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
