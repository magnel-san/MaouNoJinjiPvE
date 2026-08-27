using UnityEngine;

namespace Game
{
    // SupportHealAbilityの回復演出。支援役から対象へ向けて一瞬だけ光の筋を表示して消える
    // (LightningBoltEffectと同じ「2点間を結ぶ短命ビーム」の構造だが、直線かつ色が違うだけの
    // シンプルな見た目にしている)。
    public class HealBeamEffect : MonoBehaviour
    {
        LineRenderer line;
        float duration;
        float elapsed;
        Color baseColor;

        public static void Spawn(Vector3 from, Vector3 to, Color color, float duration = 0.3f)
        {
            var go = new GameObject("HealBeam");
            go.AddComponent<HealBeamEffect>().Initialize(from, to, color, duration);
        }

        void Initialize(Vector3 from, Vector3 to, Color color, float durationSeconds)
        {
            duration = Mathf.Max(0.05f, durationSeconds);
            baseColor = color;

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.1f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(VfxShaderUtil.GetUnlitShader()) { color = color };
            line.startColor = color;
            line.endColor = color;

            CombatFx.ImpactBurst(to, color, 0.3f);
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
