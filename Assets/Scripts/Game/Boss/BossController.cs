using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    // ボスの行動を統括する。動かず中央に留まり(Rigidbody.isKinematic=trueで物理的にも不動、
    // CharacterMovement等の移動系コンポーネントは一切付けない)、3つの攻撃パターン
    // (衝撃波/予告地面攻撃/召喚)を独立したクールダウンタイマーで並行して繰り出す。
    // 既存アビリティと同じくUpdate()のカウントダウン方式で駆動する
    // (コルーチンはCharacterActivation.SetActive(false)中も動き続けてしまうため、
    // 配置フェーズ中に攻撃が暴発しないよう意図的に避けている。CharacterActivation側で
    // このコンポーネント自体のenabledも切り替える)。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth), typeof(CharacterStats))]
    public class BossController : MonoBehaviour
    {
        [Header("召喚するキャラ(以前の「小さい敵キャラ」)")]
        [SerializeField] private GameObject[] _summonPrefabs;
        [Tooltip("ボスを中心に、この半径のリング上へランダムに召喚する")]
        [SerializeField] private float _summonSpawnRadius = 4f;

        [Header("効果音 (未設定なら無音)")]
        [SerializeField] private AudioClip _shockwaveSound;
        [SerializeField] private AudioClip _telegraphWarningSound;
        [SerializeField] private AudioClip _telegraphDetonateSound;
        [SerializeField] private AudioClip _summonSound;

        static readonly Color ShockwaveColor = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color SummonColor = new Color(0.35f, 0.05f, 0.45f);

        CharacterIdentity identity;
        CharacterStats stats;

        // 1〜3回戦のうち何回目のボスか。GameFlowManagerがInstantiate直後(Start()が走るより前)に設定する。
        // MaxHPはGameFlowManager側で直接CharacterStats.MaxHPへ反映するため、ここでは攻撃ダメージの倍率にのみ使う。
        int difficultyRound = 1;

        float shockwaveTimer;
        float telegraphTimer;
        float summonTimer;

        public void SetDifficultyRound(int round) => difficultyRound = Mathf.Max(1, round);

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            stats = GetComponent<CharacterStats>();
        }

        void Start()
        {
            // GameBalanceConfigの5段階Tierには乗らない固定の攻撃力0(接触ダメージを無害化する)。
            // CharacterStats.Awake()がTier由来の値を書き込んだ後に上書きする必要があるため、
            // 同じAwakeフェーズではなく(コンポーネント間の順序は保証されない)Start()で行う。
            stats.OverrideAttackPower(0f);

            var cfg = GameBalanceConfig.Instance;
            shockwaveTimer = cfg != null ? cfg.BossShockwaveCooldown : 7f;
            telegraphTimer = cfg != null ? cfg.BossTelegraphCooldown : 5f;
            summonTimer = cfg != null ? cfg.BossSummonCooldown : 12f;
        }

        // 何回目のボス戦かに応じた攻撃力倍率(GameBalanceConfig.BossRoundMultiplier)。
        float GetDamageMultiplier(GameBalanceConfig cfg)
        {
            if (cfg?.BossRoundMultiplier == null || cfg.BossRoundMultiplier.Length == 0) return 1f;
            var index = Mathf.Clamp(difficultyRound - 1, 0, cfg.BossRoundMultiplier.Length - 1);
            return cfg.BossRoundMultiplier[index];
        }

        void Update()
        {
            var cfg = GameBalanceConfig.Instance;

            // 各攻撃は「まずタイマーをリセットしてから実行する」順序にする。DoXxx側で何らかの例外が
            // 起きても(catchしてログに残すだけで)次のサイクルのタイマーは必ず進むため、
            // 1回の想定外の例外だけでその攻撃が二度と発動しなくなる、という事態を防げる。

            shockwaveTimer -= Time.deltaTime;
            if (shockwaveTimer <= 0f)
            {
                shockwaveTimer = cfg != null ? cfg.BossShockwaveCooldown : 7f;
                try { DoShockwave(cfg); } catch (System.Exception e) { Debug.LogException(e, this); }
            }

            telegraphTimer -= Time.deltaTime;
            if (telegraphTimer <= 0f)
            {
                telegraphTimer = cfg != null ? cfg.BossTelegraphCooldown : 5f;
                try { DoGroundTelegraph(cfg); } catch (System.Exception e) { Debug.LogException(e, this); }
            }

            summonTimer -= Time.deltaTime;
            if (summonTimer <= 0f)
            {
                // 上限に達していて召喚できなかった場合は少し置いて再判定する(丸ごとスキップしない)。
                var fallbackCooldown = cfg != null ? cfg.BossSummonCooldown : 12f;
                summonTimer = fallbackCooldown;
                try
                {
                    if (!TryDoSummon(cfg)) summonTimer = 2f;
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        void DoShockwave(GameBalanceConfig cfg)
        {
            var radius = cfg != null ? cfg.BossShockwaveRadius : 6f;
            var damage = (cfg != null ? cfg.BossShockwaveDamage : 15f) * GetDamageMultiplier(cfg);
            var knockback = cfg != null ? cfg.BossShockwaveKnockback : 25f;

            // 大きい一撃であることを強調するため、間隔を空けて2段のリングを出す。
            ExplosionRingEffect.Spawn(transform.position, radius, ShockwaveColor, 0.6f);
            ExplosionRingEffect.Spawn(transform.position, radius * 0.55f, ShockwaveColor, 0.4f);
            CameraShake.Shake(0.7f);
            SfxUtil.PlayAt(_shockwaveSound, transform.position);

            var hits = Physics.OverlapSphere(transform.position, radius);
            var affected = new HashSet<CharacterIdentity>();
            foreach (var hit in hits)
            {
                var targetIdentity = hit.GetComponentInParent<CharacterIdentity>();
                if (targetIdentity == null || targetIdentity == identity || !affected.Add(targetIdentity)) continue;
                if (targetIdentity.Team == identity.Team) continue;

                var health = targetIdentity.GetComponent<CharacterHealth>();
                if (health == null || !health.IsAlive) continue;
                health.ApplyDamage(damage, ShockwaveColor);

                var rb = targetIdentity.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var dir = targetIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    rb.AddForce(dir.normalized * knockback, ForceMode.VelocityChange);
                }
            }
        }

        void DoGroundTelegraph(GameBalanceConfig cfg)
        {
            var livingPlayers = CharacterRegistry.All.Where(c => c != null && c.Team == Team.Player && c.IsAlive).ToList();
            if (livingPlayers.Count == 0) return;

            var minZones = cfg != null ? cfg.BossTelegraphMinZones : 1;
            var maxZones = cfg != null ? cfg.BossTelegraphMaxZones : 3;
            var zoneCount = Random.Range(minZones, maxZones + 1);
            var radius = cfg != null ? cfg.BossTelegraphRadius : 2.5f;
            var warning = cfg != null ? cfg.BossTelegraphWarningSeconds : 1.3f;
            var damage = (cfg != null ? cfg.BossTelegraphDamage : 20f) * GetDamageMultiplier(cfg);

            for (var i = 0; i < zoneCount; i++)
            {
                var pickedTarget = livingPlayers[Random.Range(0, livingPlayers.Count)];
                var jitter = Random.insideUnitCircle * 1.5f;
                var zoneCenter = pickedTarget.transform.position + new Vector3(jitter.x, 0f, jitter.y);
                GroundTelegraphZone.Spawn(zoneCenter, radius, warning, damage, identity, _telegraphWarningSound, _telegraphDetonateSound);
            }
        }

        bool TryDoSummon(GameBalanceConfig cfg)
        {
            if (_summonPrefabs == null || _summonPrefabs.Length == 0) return false;

            var maxConcurrent = cfg != null ? cfg.BossSummonMaxConcurrent : 6;
            var currentCount = CharacterRegistry.All.Count(c => c != null && c.Team == Team.Enemy && c.IsAlive && !c.IsBoss);
            if (currentCount >= maxConcurrent) return false;

            var minCount = cfg != null ? cfg.BossSummonMinCount : 2;
            var maxCount = cfg != null ? cfg.BossSummonMaxCount : 3;
            var count = Mathf.Min(Random.Range(minCount, maxCount + 1), maxConcurrent - currentCount);
            if (count <= 0) return false;

            for (var i = 0; i < count; i++)
            {
                var prefab = _summonPrefabs[Random.Range(0, _summonPrefabs.Length)];
                if (prefab == null) continue;

                var angle = Random.Range(0f, Mathf.PI * 2f);
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _summonSpawnRadius;
                var spawnPos = transform.position + offset;

                var instance = Instantiate(prefab, spawnPos, Quaternion.identity);
                var summonIdentity = instance.GetComponent<CharacterIdentity>();
                if (summonIdentity != null) summonIdentity.Team = Team.Enemy;
                // 召喚体はGameFlowManager.SetAllCharactersActive(true)の一括活性化(戦闘開始時に1回だけ実行済み)の
                // 対象になれない(その時点でまだ存在しないため)。プレハブのActiveOnStartがfalseの場合、
                // 何もしないとCharacterActivation.Start()が1フレーム後に移動コンポーネントを無効化したまま
                // 誰も再度有効化せず、召喚された敵が一切動かなくなる不具合があったため、ここで明示的に有効化する。
                var summonActivation = instance.GetComponent<CharacterActivation>();
                if (summonActivation != null) summonActivation.SetActive(true);

                CombatFx.ImpactBurst(spawnPos + Vector3.up * 0.5f, SummonColor, 0.35f);
            }

            ExplosionRingEffect.Spawn(transform.position, _summonSpawnRadius, SummonColor, 0.5f);
            SfxUtil.PlayAt(_summonSound, transform.position);
            return true;
        }
    }
}
