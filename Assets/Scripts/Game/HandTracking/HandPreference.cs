namespace Game.HandTracking
{
    // プレイヤーが選択した利き手(カーソル・ジェスチャーコマンドに使う手)。
    // HandPreferenceSelectUIが起動時に1度だけ選ばせ、BattleGestureInputControllerがこれを読む。
    public static class HandPreference
    {
        public static bool PreferRightHand = true;
        public static bool HasBeenChosen { get; private set; }

        public static void Choose(bool preferRightHand)
        {
            PreferRightHand = preferRightHand;
            HasBeenChosen = true;
        }
    }
}
