using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // ゲーム開始時にAI/移動をオンオフできるようにする。オフの間は物理演算(重力・衝突)は継続する。
    public class CharacterActivation : MonoBehaviour
    {
        public bool ActiveOnStart = true;

        MonoBehaviour[] toggleTargets;
        // GameFlowManagerがInstantiate直後(このコンポーネント自身のStart()が走るより前、
        // 同一フレーム中)にSetActive()を明示的に呼ぶケースがある(例: ボスはSpawnBossForRound直後に
        // SetAllCharactersActive(true)を同フレームで呼ぶ)。その場合、1フレーム遅れて発火する
        // Start()がActiveOnStartへ無条件に戻してしまうと、外部からのSetActive(true)が
        // 次のフレームで勝手に打ち消される(ボスが一切攻撃しなくなるバグの原因だった)。
        // 明示的な呼び出しが既にあった場合はStart()側の上書きをスキップすることで防ぐ。
        bool hasExplicitState;

        void Awake()
        {
            var list = new List<MonoBehaviour>();
            foreach (var b in GetComponents<MonoBehaviour>())
            {
                if (b == this) continue;
                if (b is IMovementIntentSource || b is CharacterMovement || b is CharacterPosture
                    || b is BoundaryAvoidance || b is CharacterChargeAssist || b is BossController)
                {
                    list.Add(b);
                }
            }
            toggleTargets = list.ToArray();
        }

        void Start()
        {
            if (!hasExplicitState) SetActive(ActiveOnStart);
        }

        public void SetActive(bool active)
        {
            hasExplicitState = true;
            foreach (var b in toggleTargets) b.enabled = active;
        }
    }
}
