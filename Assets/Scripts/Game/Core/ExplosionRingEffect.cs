using UnityEngine;

namespace Game
{
    // 爆発・吹き飛ばしの範囲を示す、地面に広がって消えるリング演出。デバッグ表示ではなく、
    // ビルドでも常に見える実際のゲームプレイ用エフェクト。
    public class ExplosionRingEffect : MonoBehaviour
    {
        LineRenderer line;
        float duration;
        float elapsed;
        float targetRadius;
        Color baseColor;

        public static void Spawn(Vector3 position, float radius, Color color, float duration = 0.4f)
        {
            var go = new GameObject("ExplosionRingEffect");
            go.transform.position = position;
            go.AddComponent<ExplosionRingEffect>().Initialize(radius, color, duration);

            // 呼び出し元(爆弾/花火/魔法陣/ボス攻撃)を個別に改造しなくても済むよう、
            // 弾けとカメラ揺れは半径に応じてここでまとめて付与する(大きい爆発ほど派手にする)。
            CombatFx.ImpactBurst(position + Vector3.up * 0.3f, color, Mathf.Clamp(radius * 0.15f, 0.2f, 0.6f));
            CameraShake.Shake(Mathf.Clamp01(radius / 6f));
        }

        void Initialize(float radius, Color color, float durationSeconds)
        {
            targetRadius = Mathf.Max(0.05f, radius);
            duration = Mathf.Max(0.05f, durationSeconds);
            baseColor = color;

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = 0.12f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            // このマテリアルはエフェクト専用に生成した使い切りインスタンスなので、
            // さらに複製されないようsharedMaterialへ直接割り当てる。
            line.sharedMaterial = new Material(shader);

            UpdateCircle(targetRadius * 0.05f);
            ApplyColor(baseColor);
        }

        void UpdateCircle(float radius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = (float)i / line.positionCount * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, 0.05f, Mathf.Sin(t) * radius));
            }
        }

        void ApplyColor(Color c)
        {
            line.startColor = c;
            line.endColor = c;
            var mat = line.sharedMaterial;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float eased = 1f - (1f - t) * (1f - t);
            UpdateCircle(Mathf.Lerp(targetRadius * 0.05f, targetRadius, eased));

            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            ApplyColor(c);

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
