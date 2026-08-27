using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // Gタイプ: 剣召喚。周囲を回転する剣(1〜3本)を一定時間だけ自身に付与し、
    // 時間が切れたらクールダウンを挟んで再度付与するのを繰り返す。
    // 移動は一番近くの敵へ突撃するタイプ(Aタイプと同じ移動ロジック)。剣には衝突判定(トリガー)がある。
    [RequireComponent(typeof(CharacterIdentity))]
    public class SpinningSwordsAbility : MonoBehaviour, IMovementIntentSource
    {
        [Header("剣の本数・持続時間")]
        [Range(1, 3)] public int SwordCount = 1;
        [Tooltip("持続時間は5段階でGameBalanceConfigを参照する")]
        [Range(1, 5)] public int SwordDurationTier = 3;
        [Tooltip("剣が消えてから再び付与されるまでの秒数")]
        public float Cooldown = 8f;

        [Header("剣の威力 (弱めの数値を想定)")]
        public float SwordDamage = 3f;
        public float SwordInvincibilityTime = 0.5f;

        [Header("公転設定")]
        public float OrbitRadius = 1.5f;
        public float OrbitHeight = 1f;
        public float OrbitSpeed = 220f;
        public GameObject SwordPrefab;
        [Tooltip("剣を呼び出すたびに鳴らす効果音(未設定なら無音)")]
        public AudioClip SummonSound;

        public int MovementPriority => 10;

        CharacterIdentity identity;
        readonly List<OrbitingSword> activeSwords = new List<OrbitingSword>();
        bool swordsActive;
        float stateTimer;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            SpawnSwords();
        }

        void Update()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f) return;

            if (swordsActive) DespawnSwords();
            else SpawnSwords();
        }

        void SpawnSwords()
        {
            swordsActive = true;
            var cfg = GameBalanceConfig.Instance;
            stateTimer = cfg != null ? cfg.SwordDuration.Get(SwordDurationTier) : 3f;
            SfxUtil.PlayAt(SummonSound, transform.position);

            for (int i = 0; i < SwordCount; i++)
            {
                GameObject swordGO = SwordPrefab != null
                    ? Instantiate(SwordPrefab)
                    : CreateFallbackSwordVisual();

                var orbit = swordGO.AddComponent<OrbitingSword>();
                float angleOffset = (360f / SwordCount) * i;
                orbit.Initialize(transform, identity, SwordDamage, SwordInvincibilityTime, OrbitRadius, OrbitHeight, OrbitSpeed, angleOffset);
                activeSwords.Add(orbit);
            }
        }

        static GameObject CreateFallbackSwordVisual()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Sword";
            go.transform.localScale = new Vector3(0.1f, 0.1f, 0.7f);
            return go;
        }

        void DespawnSwords()
        {
            foreach (var sword in activeSwords)
            {
                if (sword != null) Destroy(sword.gameObject);
            }
            activeSwords.Clear();
            swordsActive = false;
            stateTimer = Cooldown;
        }

        public bool TryGetMovementIntent(out MovementIntent intent)
        {
            intent = default;
            var target = TargetingUtility.FindNearestEnemy(transform.position, identity.Team);
            if (target == null) return false;

            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;

            intent = new MovementIntent { DesiredDirection = dir.normalized, Move = true };
            return true;
        }

        // デバッグ表示用: 選択時のみ公転半径をシーンビューに円で表示する。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.7f, 1f);
            TargetingUtility.DrawGizmoCircle(transform.position, OrbitRadius);
        }
    }
}
