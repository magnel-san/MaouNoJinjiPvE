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

    [Header("レア度・ロール(3択の抽選重み・比率調整用。Editor > Game > Flow > Audit Recruit Pool Ranks/Rolesで集計確認可)")]
    [Tooltip("金6体(20%)/銀15体(50%)/銅9体(30%)の比率になるよう、プール全体で手動調整する")]
    public CharacterRank Rank = CharacterRank.Silver;
    [Tooltip("アタッカー:タンク:ヒーラー = 5:3:2の比率になるよう、プール全体で手動調整する")]
    public CharacterRole Role = CharacterRole.Attacker;
  }
}
