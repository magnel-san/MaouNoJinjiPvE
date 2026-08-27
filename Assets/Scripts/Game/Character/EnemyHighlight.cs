using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // 敵チームのキャラをハイライト表示する。MaterialPropertyBlockでBaseColorを混色し、
    // 対応シェーダであればEmissionも試みる (マテリアル側でEmissionが有効化されている場合のみ発光する)。
    //
    // レンダラーごとに複数マテリアル(サブメッシュ、例: 体+目のような色違いパーツ)を持つモデルがあるため、
    // 「レンダラー単位」ではなく「レンダラー内のマテリアルスロット単位」で元の色を記録・復元する。
    // これを怠ると、あるレンダラーの1番目のマテリアルの色を全スロットへ一律適用してしまい、
    // 本来別の色だったパーツまで塗り替えてしまう(=「一部だけ違う色になる」現象の原因になる)。
    // また色プロパティ名はURP系マテリアルなら_BaseColor、Standard/レガシー系なら_Colorのことが多いため
    // 両対応し、どちらも持たないマテリアルには一切触れない(誤って白色などに固定してしまわないため)。
    [RequireComponent(typeof(CharacterIdentity))]
    public class EnemyHighlight : MonoBehaviour
    {
        public Transform GhostVisualRoot;
        public Color HighlightColor = new Color(1f, 0.25f, 0.25f);
        [Range(0f, 1f)] public float TintStrength = 0.65f;
        [Range(0f, 5f)] public float EmissionIntensity = 1.5f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        struct Slot
        {
            public Renderer renderer;
            public int materialIndex;
            public int colorPropertyId;
            public bool hasColorProperty;
            public Color originalColor;
        }

        CharacterIdentity identity;
        MaterialPropertyBlock block;
        readonly List<Slot> slots = new List<Slot>();

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            var root = GhostVisualRoot != null ? GhostVisualRoot : transform;
            block = new MaterialPropertyBlock();

            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    var hasBase = mat != null && mat.HasProperty(BaseColorId);
                    var hasLegacy = !hasBase && mat != null && mat.HasProperty(LegacyColorId);

                    var slot = new Slot
                    {
                        renderer = r,
                        materialIndex = i,
                        hasColorProperty = hasBase || hasLegacy,
                        colorPropertyId = hasBase ? BaseColorId : LegacyColorId,
                    };
                    slot.originalColor = slot.hasColorProperty ? mat.GetColor(slot.colorPropertyId) : Color.white;
                    slots.Add(slot);
                }
            }
        }

        void Start() => Apply(identity.Team == Team.Enemy);

        public void Apply(bool highlighted)
        {
            foreach (var slot in slots)
            {
                if (slot.renderer == null) continue;

                slot.renderer.GetPropertyBlock(block, slot.materialIndex);

                if (slot.hasColorProperty)
                {
                    var baseColor = highlighted ? Color.Lerp(slot.originalColor, HighlightColor, TintStrength) : slot.originalColor;
                    block.SetColor(slot.colorPropertyId, baseColor);
                }

                block.SetColor(EmissionColorId, highlighted ? HighlightColor * EmissionIntensity : Color.black);
                slot.renderer.SetPropertyBlock(block, slot.materialIndex);
            }
        }
    }
}
