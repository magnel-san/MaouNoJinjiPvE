using UnityEditor;
using UnityEngine;

namespace Game.Flow.EditorTools
{
  // StageDefinitionEditor / FormationOptionEditorで共用する、
  // 「配置予定地を地面の円で表示し、ドラッグでも動かせるようにする」ためのヘルパー。
  internal static class SpawnPointGizmoUtility
  {
    // 円を描画し、ハンドルがドラッグされたらtrueを返す(newWorldPosに新しい座標を入れる)。
    public static bool DrawHandle(Vector3 worldPos, Quaternion handleRotation, string label, Color color, float radius, out Vector3 newWorldPos)
    {
      Handles.color = color;
      Handles.DrawWireDisc(worldPos, Vector3.up, radius);

      if (!string.IsNullOrEmpty(label))
      {
        Handles.Label(worldPos + Vector3.up * (radius + 0.4f), label);
      }

      EditorGUI.BeginChangeCheck();
      newWorldPos = Handles.PositionHandle(worldPos, handleRotation);
      return EditorGUI.EndChangeCheck();
    }
  }
}
