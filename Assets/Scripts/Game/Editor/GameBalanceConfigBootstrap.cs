using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // GameBalanceConfig.asset が Assets/Resources に存在しない場合、エディタ読み込み時に自動作成する。
    [InitializeOnLoad]
    internal static class GameBalanceConfigBootstrap
    {
        const string ResourceFolder = "Assets/Resources";
        const string AssetPath = ResourceFolder + "/GameBalanceConfig.asset";

        static GameBalanceConfigBootstrap()
        {
            EditorApplication.delayCall += EnsureAssetExists;
        }

        [MenuItem("Game/Create Game Balance Config")]
        static void EnsureAssetExists()
        {
            if (AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(AssetPath) != null) return;

            if (!AssetDatabase.IsValidFolder(ResourceFolder))
            {
                Directory.CreateDirectory(ResourceFolder);
                AssetDatabase.Refresh();
            }

            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Game] {AssetPath} を作成しました。");
        }
    }
}
