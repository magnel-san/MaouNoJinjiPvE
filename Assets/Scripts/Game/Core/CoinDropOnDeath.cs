using UnityEngine;

namespace Game
{
    // 撃破された瞬間にコインを1枚落とす。BossController.TryDoSummonが召喚直後に動的付与するため、
    // 既存の召喚プレハブを個別に編集する必要はない。
    [RequireComponent(typeof(CharacterHealth))]
    public class CoinDropOnDeath : MonoBehaviour
    {
        CharacterHealth health;

        void Awake() => health = GetComponent<CharacterHealth>();

        void OnEnable() => health.OnDied += HandleDied;
        void OnDisable() => health.OnDied -= HandleDied;

        void HandleDied() => CoinPickup.Spawn(transform.position);
    }
}
