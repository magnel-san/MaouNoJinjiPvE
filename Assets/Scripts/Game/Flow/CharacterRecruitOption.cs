using UnityEngine;

namespace Game.Flow
{
  // 3択UIで選ばれる候補キャラ1体分。履歴書画像と、実際に配置するキャラプレハブを紐づける。
  [CreateAssetMenu(fileName = "CharacterRecruitOption", menuName = "Game/Flow/Character Recruit Option")]
  public class CharacterRecruitOption : ScriptableObject
  {
    public string DisplayName;
    [Tooltip("3択UIに表示する履歴書画像")]
    public Sprite ResumeSprite;
    [Tooltip("配置フェーズで実際にInstantiateするキャラのプレハブ (Assets/Prefabs/Characters内)")]
    public GameObject CharacterPrefab;
  }
}
