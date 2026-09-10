using UnityEngine;

namespace Game
{
    // スキル「自己ダメージ軽減」。移動ロジックとは無関係に、自身の被ダメージを軽減率(%)でカットする。
    // HPを高めに設定した「硬いアタッカー」等に付ける想定。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth))]
    public class SelfDamageReductionSkill : MonoBehaviour
    {
        [Range(1, 3)] public int DamageReductionTier = 2;

        CharacterHealth health;

        void Awake() => health = GetComponent<CharacterHealth>();

        void Start()
        {
            var cfg = GameBalanceConfig.Instance;
            if (cfg != null) health.DamageReductionPercent = cfg.DamageReductionPercent.Get(DamageReductionTier);
        }
    }
}
