using UnityEngine;

namespace Game.Flow
{
  // 1体分: どのキャラプレハブを、どの座標(複数可)に配置するか。
  [System.Serializable]
  public class StageEnemyEntry
  {
    public GameObject CharacterPrefab;
    [Tooltip("このキャラを配置するワールド座標。複数指定するとその数だけ同じキャラを配置する")]
    public Vector3[] SpawnPositions = new Vector3[] { Vector3.zero };
  }

  // 1ステージ分の敵配置データ。あらかじめシーンに敵を置く代わりに、
  // GameFlowManagerがここに記録された座標へ敵キャラを実行時にInstantiateする。
  [CreateAssetMenu(fileName = "StageDefinition", menuName = "Game/Flow/Stage Definition")]
  public class StageDefinition : ScriptableObject
  {
    public string StageName;
    public StageEnemyEntry[] Enemies;
  }
}
