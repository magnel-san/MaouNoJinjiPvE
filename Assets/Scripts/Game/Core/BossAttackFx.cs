using UnityEngine;

namespace Game
{
    // ボスの予告攻撃(GroundTelegraphZone/RectTelegraphZone等)が味方に命中/よけられた/グー防御で
    // 無効化された瞬間の演出をここに一元化する。命中時はダメージ数値のポップアップとは別にOUCHを
    // 重ねて出し、ヒットストップ+大きめのカメラ揺れで「効いた」手応えを強調する。よけた場合はDODGE、
    // グー防御で0ダメージにできた場合はGUARD+はじく効果音。
    public static class BossAttackFx
    {
        const float HitStopSeconds = 0.05f;
        const float HitCameraShake = 0.6f;

        public static void NotifyPlayerHit(CharacterIdentity target)
        {
            if (target == null) return;

            CombatFx.OuchPopup(target.transform.position);
            HitStop.Trigger(HitStopSeconds);
            CameraShake.Shake(HitCameraShake);
        }

        public static void NotifyPlayerDodged(CharacterIdentity target)
        {
            if (target == null) return;

            CombatFx.DodgePopup(target.transform.position);
        }

        // グー防御でダメージを完全無効化できた時。OUCHの代わりにGUARDを表示し、はじく効果音を鳴らす。
        public static void NotifyPlayerGuarded(CharacterIdentity target)
        {
            if (target == null) return;

            CombatFx.GuardPopup(target.transform.position);
            var cfg = GameBalanceConfig.Instance;
            if (cfg != null) SfxUtil.PlayAt(cfg.GuardBlockSound, target.transform.position);
        }
    }
}
