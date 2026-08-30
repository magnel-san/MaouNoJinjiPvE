using UnityEngine;

namespace Game
{
    // 撃破された瞬間にコインを落とす。BossController.TryDoSummonが召喚直後に動的付与するため、
    // 既存の召喚プレハブを個別に編集する必要はない。
    // 敵の落とすコインは全体的に倍化する方針のため、1体につき2枚落とす。
    [RequireComponent(typeof(CharacterHealth))]
    public class CoinDropOnDeath : MonoBehaviour
    {
        const int CoinDropCount = 2;

        CharacterHealth health;

        void Awake() => health = GetComponent<CharacterHealth>();

        void OnEnable() => health.OnDied += HandleDied;
        void OnDisable() => health.OnDied -= HandleDied;

        void HandleDied()
        {
            for (var i = 0; i < CoinDropCount; i++)
            {
                var offset = Random.insideUnitCircle * 0.4f;
                CoinPickup.Spawn(transform.position + new Vector3(offset.x, 0f, offset.y));
            }
        }
    }
}
