using UnityEngine;

namespace Game
{
    // 実行時生成する演出用マテリアル/フォントの共通解決ヘルパー。
    // HpBarGauge/ExplosionRingEffect等で個別にキャッシュされていたシェーダ解決を、
    // 新規VFX(CombatFx等)向けに1箇所へ集約する。
    public static class VfxShaderUtil
    {
        static Shader cachedUnlitShader;
        static Shader cachedTransparentShader;
        static Font cachedDefaultFont;
        static Sprite cachedPanelSprite;
        static Sprite cachedGradientFillSprite;

        // 不透明・薄いライン系の演出向け(ExplosionRingEffent等、既存コードと同じ解決順)。
        public static Shader GetUnlitShader()
        {
            if (cachedUnlitShader != null) return cachedUnlitShader;
            cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (cachedUnlitShader == null) cachedUnlitShader = Shader.Find("Unlit/Color");
            return cachedUnlitShader;
        }

        // アルファのフェード/半透明が前提の演出向け(パーティクル・警告円等)。
        // URP UnlitのTransparentサーフェス設定をスクリプトから正確に再現するのは
        // プロパティ名がバージョン依存で壊れやすいため、常にアルファブレンドされる
        // 組み込みシェーダ(Sprites/Default)を使う。
        public static Shader GetTransparentShader()
        {
            if (cachedTransparentShader != null) return cachedTransparentShader;
            cachedTransparentShader = Shader.Find("Sprites/Default");
            if (cachedTransparentShader == null) cachedTransparentShader = GetUnlitShader();
            return cachedTransparentShader;
        }

        public static Font GetDefaultFont()
        {
            if (cachedDefaultFont != null) return cachedDefaultFont;
            // Unity 2022.2以降は組み込みフォント名がArial.ttf→LegacyRuntime.ttfに変更されているため両対応する。
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedDefaultFont == null) cachedDefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedDefaultFont;
        }

        // HPバー/ゲージの外枠に使う、縁取り付きの9-sliceパネル画像。白ベースで生成しているため
        // Image.colorで自由に色味を変えられる(BossHpBarUI/UltimateGaugeUI参照)。
        // Sliced表示にすることで、バーの横幅が変わっても縁の太さが伸びずに保たれる。
        public static Sprite GetPanelSprite()
        {
            if (cachedPanelSprite != null) return cachedPanelSprite;

            const int size = 16;
            const int border = 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isBorder = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = isBorder ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 90);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            cachedPanelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return cachedPanelSprite;
        }

        // HPバー/ゲージの塗りつぶし部分に使う、縦方向の明暗グラデーション画像(上が明るく下が暗い)。
        // 単色の塗りより立体感が出る。白ベースなのでImage.colorで自由に色味を変えられる。
        // Image.Type.Filled(Horizontal)と組み合わせて使う想定(横幅は伸縮されるだけなので1px幅でよい)。
        public static Sprite GetGradientFillSprite()
        {
            if (cachedGradientFillSprite != null) return cachedGradientFillSprite;

            const int height = 32;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[height];
            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1); // 0=下端, 1=上端
                var shade = Mathf.Lerp(0.7f, 1.15f, t);
                pixels[y] = new Color(shade, shade, shade, 1f);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            cachedGradientFillSprite = Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f), 100f);
            return cachedGradientFillSprite;
        }
    }
}
