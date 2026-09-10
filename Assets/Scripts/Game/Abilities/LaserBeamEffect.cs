using UnityEngine;

namespace Game
{
    // LaserBeamSkillの見た目。LightningBoltEffectと同じ「2点間を結ぶ短命ビーム」の構造だが、
    // ジグザグさせず、太い色付きの外側グロー+細い白色の芯の2本のLineRendererを重ねることで
    // 「レーザー」らしい直線的で発光感のある見た目にしている。
    public class LaserBeamEffect : MonoBehaviour
    {
        const float Duration = 0.18f;

        LineRenderer glow;
        LineRenderer core;
        float elapsed;
        Color baseColor;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("LaserBeam");
            go.AddComponent<LaserBeamEffect>().Initialize(from, to, color);
        }

        void Initialize(Vector3 from, Vector3 to, Color color)
        {
            baseColor = color;

            glow = BuildLine("Glow", from, to, color, 0.35f);
            core = BuildLine("Core", from, to, Color.white, 0.1f);

            CombatFx.ImpactBurst(from, color, 0.2f);
            CombatFx.ImpactBurst(to, color, 0.3f);
        }

        LineRenderer BuildLine(string name, Vector3 from, Vector3 to, Color color, float width)
        {
            var lineGo = new GameObject(name);
            lineGo.transform.SetParent(transform, false);

            var line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(VfxShaderUtil.GetUnlitShader()) { color = color };
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / Duration);

            // 発生直後にパッと太く光り、すぐ収束しながらフェードアウトする「レーザーの明滅」を表現する。
            var shrink = Mathf.Lerp(1f, 0.15f, t);
            glow.widthMultiplier = 0.35f * shrink;
            core.widthMultiplier = 0.1f * shrink;

            var alpha = 1f - t;
            SetAlpha(glow, baseColor, alpha);
            SetAlpha(core, Color.white, alpha);

            if (t >= 1f) Destroy(gameObject);
        }

        static void SetAlpha(LineRenderer line, Color color, float alpha)
        {
            var c = color;
            c.a = color.a * alpha;
            line.startColor = c;
            line.endColor = c;
        }
    }
}
