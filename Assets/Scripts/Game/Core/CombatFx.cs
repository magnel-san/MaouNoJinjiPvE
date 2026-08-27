using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // 全キャラの被弾演出を1箇所に集約する静的ヘルパー。CharacterHealth.ApplyDamage/Die、
    // ExplosionRingEffect等の「ダメージ・撃破が実際に発生した瞬間」から呼ぶだけでよい
    // (呼び出し側を1体1体改造しなくても、全ダメージ経路がここを通るだけで演出が付く)。
    // このプロジェクトの既存演出(HpBarGauge/ExplosionRingEffect等)と同じく、
    // 外部アセットに一切依存せず全てランタイムでプロシージャルに生成する。
    public static class CombatFx
    {
        public static readonly Color DefaultDamageColor = new Color(1f, 0.85f, 0.2f);

        public static void HitFlash(Transform target, Color color, float duration = 0.15f)
        {
            if (target == null) return;
            HitFlashRunner.Trigger(target, color, duration);
        }

        public static void DamagePopup(Vector3 worldPos, float amount, Color color)
        {
            if (amount <= 0f) return;
            DamagePopupEffect.Spawn(worldPos + Vector3.up * 1.6f, Mathf.CeilToInt(amount).ToString(), color);
        }

        public static void ImpactBurst(Vector3 worldPos, Color color, float size = 0.25f)
        {
            BurstEffect.Spawn(worldPos, color, size, count: 10, speed: 3.5f, lifetime: 0.35f);
        }

        public static void DeathBurst(Vector3 worldPos, Color color)
        {
            BurstEffect.Spawn(worldPos, color, 0.4f, count: 24, speed: 5.5f, lifetime: 0.55f);
            ExplosionRingEffect.Spawn(worldPos, 1.4f, color, 0.5f);
        }

        // 対象1体につき1つだけ生成・使い回される、被弾時のエミッション点滅ランナー。
        // EnemyHighlight等が既に恒常的なEmissionColorを書き込んでいる場合でも壊さないよう、
        // 初回トリガー時に各Rendererの「地の」EmissionColorを退避し、点滅後は必ずそこへ戻す。
        // レンダラーが複数マテリアル(サブメッシュ)を持つ場合、スロットごとに元のEmissionが異なりうるため
        // (例: 目だけ発光する等)、レンダラー単位ではなくマテリアルスロット単位で退避・復元する
        // (EnemyHighlight.csと同じ理由、詳細はそちらのコメント参照)。
        class HitFlashRunner : MonoBehaviour
        {
            static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

            struct Slot
            {
                public Renderer renderer;
                public int materialIndex;
                public Color restEmission;
            }

            List<Slot> _slots;
            MaterialPropertyBlock _block;
            Coroutine _active;

            public static void Trigger(Transform target, Color color, float duration)
            {
                var runner = target.GetComponent<HitFlashRunner>();
                if (runner == null) runner = target.gameObject.AddComponent<HitFlashRunner>();
                runner.Flash(color, duration);
            }

            void Flash(Color color, float duration)
            {
                if (_slots == null)
                {
                    _block = new MaterialPropertyBlock();
                    _slots = new List<Slot>();
                    foreach (var r in GetComponentsInChildren<Renderer>())
                    {
                        if (r == null) continue;
                        var count = r.sharedMaterials.Length;
                        for (var i = 0; i < count; i++)
                        {
                            r.GetPropertyBlock(_block, i);
                            _slots.Add(new Slot { renderer = r, materialIndex = i, restEmission = _block.GetColor(EmissionColorId) });
                        }
                    }
                }

                if (_active != null) StopCoroutine(_active);
                _active = StartCoroutine(FlashRoutine(color, duration));
            }

            IEnumerator FlashRoutine(Color flashColor, float duration)
            {
                var t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    var strength = 1f - Mathf.Clamp01(t / duration);
                    foreach (var slot in _slots)
                    {
                        if (slot.renderer == null) continue;
                        slot.renderer.GetPropertyBlock(_block, slot.materialIndex);
                        _block.SetColor(EmissionColorId, Color.Lerp(slot.restEmission, flashColor * 2.5f, strength));
                        slot.renderer.SetPropertyBlock(_block, slot.materialIndex);
                    }
                    yield return null;
                }

                foreach (var slot in _slots)
                {
                    if (slot.renderer == null) continue;
                    slot.renderer.GetPropertyBlock(_block, slot.materialIndex);
                    _block.SetColor(EmissionColorId, slot.restEmission);
                    slot.renderer.SetPropertyBlock(_block, slot.materialIndex);
                }
                _active = null;
            }
        }

        // 浮き上がって消える3Dダメージ数値。TextMeshPro等の追加パッケージを使わず、
        // 常に利用可能な組み込みのTextMesh+組み込みフォントだけで完結させる。
        class DamagePopupEffect : MonoBehaviour
        {
            const float Duration = 0.8f;

            TextMesh _mesh;
            Camera _cam;
            float _elapsed;

            public static void Spawn(Vector3 worldPos, string text, Color color)
            {
                var go = new GameObject("DamagePopup");
                go.transform.position = worldPos;
                go.AddComponent<DamagePopupEffect>().Initialize(text, color);
            }

            void Initialize(string text, Color color)
            {
                _cam = Camera.main;

                _mesh = gameObject.AddComponent<TextMesh>();
                _mesh.text = text;
                _mesh.color = color;
                _mesh.characterSize = 0.12f;
                _mesh.fontSize = 48;
                _mesh.anchor = TextAnchor.MiddleCenter;
                _mesh.alignment = TextAlignment.Center;
                _mesh.font = VfxShaderUtil.GetDefaultFont();

                var meshRenderer = GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _mesh.font.material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                if (_cam != null) transform.rotation = _cam.transform.rotation;
            }

            void Update()
            {
                _elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_elapsed / Duration);

                transform.position += Vector3.up * (Time.deltaTime * 1.4f);
                if (_cam != null) transform.rotation = _cam.transform.rotation;

                var c = _mesh.color;
                c.a = 1f - t;
                _mesh.color = c;

                if (t >= 1f) Destroy(gameObject);
            }
        }

        // 弾ける粒子バースト。ParticleSystemを完全にコードだけで組み立てる(手組みアセット不要)。
        class BurstEffect
        {
            public static void Spawn(Vector3 position, Color color, float size, int count, float speed, float lifetime)
            {
                var go = new GameObject("ImpactBurst");
                go.transform.position = position;

                var ps = go.AddComponent<ParticleSystem>();
                // ParticleSystemはAddComponent直後、playOnAwakeの既定値により既に再生が始まっていることがあり、
                // 再生中はmain.durationの変更が例外になる。設定前に必ず一度止めてから組み立てる。
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.duration = lifetime;
                main.loop = false;
                main.startLifetime = lifetime;
                main.startSpeed = speed;
                main.startSize = size;
                main.startColor = color;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.stopAction = ParticleSystemStopAction.Destroy;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = grad;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(VfxShaderUtil.GetTransparentShader());
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                ps.Play();
            }
        }
    }
}
