using System.Collections.Generic;

namespace Game
{
    // シーン内の全キャラクターへの参照を保持し、索敵クエリをFindObjectOfType無しで行えるようにする。
    public static class CharacterRegistry
    {
        static readonly List<CharacterIdentity> all = new List<CharacterIdentity>();

        public static IReadOnlyList<CharacterIdentity> All => all;

        public static void Register(CharacterIdentity character)
        {
            if (!all.Contains(character)) all.Add(character);
        }

        public static void Unregister(CharacterIdentity character)
        {
            all.Remove(character);
        }
    }
}
