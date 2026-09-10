using UnityEngine;

namespace Game
{
    [DisallowMultipleComponent]
    public class CharacterIdentity : MonoBehaviour
    {
        // Team/IsBossは常にスポーン時にコード側(GameFlowManager等)が上書きするため、
        // プレファブ側でInspector設定する必要が無い(整理のため非表示にしてある)。
        [HideInInspector] public Team Team = Team.Player;
        [HideInInspector] public bool IsBoss = false;
        // IsHeroだけは実行時にコードから上書きされる箇所が無く、プレファブごとに手動設定する必要があるため
        // (勇者系プレファブでtrueにする)、他と違いInspectorに表示したままにしてある。
        [Tooltip("勇者プレファブかどうか(CharacterStatsがGameBalanceConfigのHero/Monster倍率のどちらを" +
            "適用するか判定するのに使う。勇者以外は全てモンスター扱い)")]
        public bool IsHero = false;
        public bool IsAlive { get; set; } = true;

        void OnEnable() => CharacterRegistry.Register(this);
        void OnDisable() => CharacterRegistry.Unregister(this);
    }
}
