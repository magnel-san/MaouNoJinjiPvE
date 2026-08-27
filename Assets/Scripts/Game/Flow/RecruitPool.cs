using UnityEngine;

namespace Game.Flow
{
  // 候補キャラ全体のプール。1ラウンドごとにここからランダムに3体選んで3択UIに表示する。
  [CreateAssetMenu(fileName = "RecruitPool", menuName = "Game/Flow/Recruit Pool")]
  public class RecruitPool : ScriptableObject
  {
    public CharacterRecruitOption[] Options;
  }
}
