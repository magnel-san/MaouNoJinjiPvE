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
    }
}
