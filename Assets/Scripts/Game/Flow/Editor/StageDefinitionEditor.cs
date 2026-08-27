using UnityEditor;
using UnityEngine;

namespace Game.Flow.EditorTools
{
  // StageDefinitionをInspectorで選択している間、各SpawnPositions(絶対ワールド座標)を
  // Sceneビュー上に円で表示し、ドラッグで直接座標を編集できるようにする。
  [CustomEditor(typeof(StageDefinition))]
  public class StageDefinitionEditor : Editor
  {
    const float Radius = 1.2f;
    static readonly Color GizmoColor = new Color(1f, 0.3f, 0.3f);

    public override void OnInspectorGUI()
    {
      DrawDefaultInspector();

      EditorGUILayout.Space();
      if (GUILayout.Button("Sceneビューでこのステージの配置地点を表示"))
      {
        FrameInSceneView();
      }
      EditorGUILayout.HelpBox(
        "円が見えない場合は、上のボタンでSceneビューのカメラを配置地点へ移動できます。\n" +
        "Stage_01アセット自体を選択している必要があります(GameFlowManager経由で参照を見ているだけでは表示されません)。",
        MessageType.Info);
    }

    void FrameInSceneView()
    {
      if (!TryGetBounds(out var bounds)) return;
      var sceneView = SceneView.lastActiveSceneView;
      if (sceneView == null) return;
      sceneView.Frame(bounds, true);
    }

    bool TryGetBounds(out Bounds bounds)
    {
      var stage = (StageDefinition)target;
      bounds = default;
      var hasAny = false;

      if (stage.Enemies == null) return false;

      foreach (var entry in stage.Enemies)
      {
        if (entry?.SpawnPositions == null) continue;
        foreach (var pos in entry.SpawnPositions)
        {
          if (!hasAny)
          {
            bounds = new Bounds(pos, Vector3.one * Radius * 2f);
            hasAny = true;
          }
          else
          {
            bounds.Encapsulate(pos);
          }
        }
      }

      return hasAny;
    }

    void OnSceneGUI()
    {
      var stage = (StageDefinition)target;
      if (stage.Enemies == null) return;

      for (var e = 0; e < stage.Enemies.Length; e++)
      {
        var entry = stage.Enemies[e];
        if (entry?.SpawnPositions == null) continue;

        var prefabName = entry.CharacterPrefab != null ? entry.CharacterPrefab.name : "(未設定)";

        for (var i = 0; i < entry.SpawnPositions.Length; i++)
        {
          var label = $"{prefabName} [{i}]";
          var changed = SpawnPointGizmoUtility.DrawHandle(
            entry.SpawnPositions[i], Quaternion.identity, label, GizmoColor, Radius, out var newPos);

          if (changed)
          {
            Undo.RecordObject(stage, "Move Stage Spawn Point");
            entry.SpawnPositions[i] = newPos;
            EditorUtility.SetDirty(stage);
          }
        }
      }
    }
  }
}
