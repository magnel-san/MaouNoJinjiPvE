using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // プロジェクト内にAudioClipフィールドが散らばっていて(各スキル・CharacterHealth・BossController等
    // 多数のスクリプトにそれぞれ点在)、どこで何の音が設定されているか把握しづらいという問題に対応する。
    // Assets/Prefabs以下の全プレファブ + GameBalanceConfigが持つAudioClip型フィールド(public、または
    // [SerializeField]のprivate)をリフレクションで洗い出し、1つのウィンドウで一覧・再割り当てできるようにする。
    // RecruitPoolAuditTool.csと同じ[MenuItem]パターン。プレファブ資産への書き込みはCharacterStats等の
    // 単純フィールド編集と同じ「AssetDatabase.LoadAssetAtPath + リフレクション + SetDirty/SaveAssets」方式
    // (LoadPrefabContentsは構造変更が要らないここでは不要)。
    public class AudioClipManagerWindow : EditorWindow
    {
        class ClipEntry
        {
            public UnityEngine.Object Target;
            public FieldInfo Field;
            public string GroupName;
            public string Label;
        }

        List<ClipEntry> entries = new List<ClipEntry>();
        readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
        Vector2 scroll;
        string searchFilter = "";

        [MenuItem("Game/Audio/Audio Clip Manager")]
        static void Open()
        {
            var window = GetWindow<AudioClipManagerWindow>("Audio Clip Manager");
            window.Rescan();
        }

        void OnEnable()
        {
            if (entries.Count == 0) Rescan();
        }

        void Rescan()
        {
            entries = ScanAll();
            foldouts.Clear();
        }

        static List<ClipEntry> ScanAll()
        {
            var result = new List<ClipEntry>();
            var seenPaths = new HashSet<string>();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seenPaths.Add(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                foreach (var comp in go.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue; // missing script
                    ScanObjectFields(comp, go.name, result);
                }
            }

            var configGuids = AssetDatabase.FindAssets("t:GameBalanceConfig");
            foreach (var guid in configGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(path);
                if (cfg == null) continue;
                ScanObjectFields(cfg, cfg.name, result);
            }

            return result.OrderBy(e => e.GroupName).ThenBy(e => e.Label).ToList();
        }

        static void ScanObjectFields(UnityEngine.Object obj, string groupName, List<ClipEntry> result)
        {
            var fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(AudioClip)) continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;

                result.Add(new ClipEntry
                {
                    Target = obj,
                    Field = field,
                    GroupName = groupName,
                    Label = $"{obj.GetType().Name}.{field.Name}"
                });
            }
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("再スキャン", EditorStyles.toolbarButton, GUILayout.Width(90))) Rescan();
                GUILayout.FlexibleSpace();
                GUILayout.Label("検索:", GUILayout.Width(32));
                searchFilter = GUILayout.TextField(searchFilter, GUILayout.Width(200));
            }

            EditorGUILayout.Space();

            var visible = entries.Where(e => e.Target != null).ToList();
            EditorGUILayout.LabelField($"{visible.Count} 件のAudioClipフィールドを検出 (未設定: {visible.Count(e => e.Field.GetValue(e.Target) == null)}件)", EditorStyles.miniLabel);

            var filtered = visible.Where(e =>
                string.IsNullOrEmpty(searchFilter) ||
                e.GroupName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.Label.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var group in filtered.GroupBy(e => e.GroupName))
            {
                var key = group.Key;
                foldouts.TryGetValue(key, out var open);
                foldouts[key] = EditorGUILayout.Foldout(open, key, true);
                if (!foldouts[key]) continue;

                EditorGUI.indentLevel++;
                foreach (var entry in group)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.Label, GUILayout.Width(260));
                    var current = (AudioClip)entry.Field.GetValue(entry.Target);
                    var updated = (AudioClip)EditorGUILayout.ObjectField(current, typeof(AudioClip), false);
                    if (updated != current)
                    {
                        entry.Field.SetValue(entry.Target, updated);
                        EditorUtility.SetDirty(entry.Target);
                        AssetDatabase.SaveAssets();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
