using UnityEngine;

namespace Game
{
    [DisallowMultipleComponent]
    public class CharacterIdentity : MonoBehaviour
    {
        public Team Team = Team.Player;
        [Tooltip("ボス(FocusBoss/FocusNonBoss狙い撃ちコマンドやボスHP UIが参照する目印)")]
        public bool IsBoss = false;
        [Tooltip("勇者プレファブかどうか(CharacterStatsがGameBalanceConfigのHero/Monster倍率のどちらを" +
            "適用するか判定するのに使う。勇者以外は全てモンスター扱い)")]
        public bool IsHero = false;
        public bool IsAlive { get; set; } = true;

        void OnEnable() => CharacterRegistry.Register(this);
        void OnDisable() => CharacterRegistry.Unregister(this);
    }
}
