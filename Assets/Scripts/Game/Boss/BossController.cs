using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth), typeof(CharacterStats))]
    [RequireComponent(typeof(BossMovement))] // BossMovementを必須化
    public class BossController : MonoBehaviour
    {
        [Header("召喚するキャラ(以前の「小さい敵キャラ」)")]
        [SerializeField] private GameObject[] _summonPrefabs;
        [Tooltip("ボスを中心に、この半径のリング上へランダムに召喚する")]
        [SerializeField] private float _summonSpawnRadius = 4f;

        [Header("魔王城の門（召喚ポイント）設定")]
        [Tooltip("自動検索する門オブジェクトの名前")]
        [SerializeField] private string _gateObjectName = "DemonCastleGatePoint";
        [Tooltip("門から召喚する際の出現のバラつき（半径）")]
        [SerializeField] private float _gateSpawnRadius = 1.5f;

        [Header("効果音 (未設定なら無音)")]
        [SerializeField] private AudioClip _shockwaveSound;
        [SerializeField] private AudioClip _telegraphWarningSound;
        [SerializeField] private AudioClip _telegraphDetonateSound;
        [SerializeField] private AudioClip _summonSound;

        static readonly Color ShockwaveColor = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color SummonColor = new Color(0.35f, 0.05f, 0.45f);

        CharacterIdentity identity;
        CharacterStats stats;
        BossMovement movement;
        Rigidbody rb;

        // 名前検索で自動取得した門のTransformを保持する変数
        private Transform _demonCastleGatePoint;

        int difficultyRound = 1;

        float shockwaveTimer;
        float telegraphTimer;
        float summonTimer;
        float patternTimer; // 新移動パターンのタイマー

        public void SetDifficultyRound(int round) => difficultyRound = Mathf.Max(1, round);

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            stats = GetComponent<CharacterStats>();
            movement = GetComponent<BossMovement>();
            rb = GetComponent<Rigidbody>();
        }

        void Start()
        {
            stats.OverrideAttackPower(0f);

            // ★シーン内から「DemonCastleGatePoint」という名前のオブジェクトを自動検索して設定
            var gateObj = GameObject.Find(_gateObjectName);
            if (gateObj != null)
            {
                _demonCastleGatePoint = gateObj.transform;
            }
            else
            {
                Debug.LogWarning($"[BossController] 名前に '{_gateObjectName}' が含まれるオブジェクトがシーン内に見つかりません。");
            }

            var cfg = GameBalanceConfig.Instance;
            shockwaveTimer = cfg != null ? cfg.BossShockwaveCooldown : 7f;
            telegraphTimer = cfg != null ? cfg.BossTelegraphCooldown : 5f;
            summonTimer = cfg != null ? cfg.BossSummonCooldown : 12f;
            patternTimer = 1f; // 4秒ごとに移動アクションを開始
        }

        float GetDamageMultiplier(GameBalanceConfig cfg)
        {
            if (cfg?.BossRoundMultiplier == null || cfg.BossRoundMultiplier.Length == 0) return 1f;
            var index = Mathf.Clamp(difficultyRound - 1, 0, cfg.BossRoundMultiplier.Length - 1);
            return cfg.BossRoundMultiplier[index];
        }

        void Update()
        {
            var cfg = GameBalanceConfig.Instance;

            // 移動アクションタイマー（BossMovementが実行中でない場合のみ更新・発動）
            if (!movement.IsMoving)
            {
                patternTimer -= Time.deltaTime;
                if (patternTimer <= 0f)
                {
                    patternTimer = 5f; // 次の行動までの間隔
                    StartCoroutine(ExecuteRandomMovementPattern(cfg));
                }
            }

            // 既存攻撃タイマー
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

        // --- 4つの行動パターンの切り替え処理 ---
        IEnumerator ExecuteRandomMovementPattern(GameBalanceConfig cfg)
        {
            if (rb != null) rb.isKinematic = true; // 移動はMovePositionで行うためKinematicを維持

            int pattern = Random.Range(1, 5); // 1〜4をランダム選出
            Transform farthestMonster = GetFarthestMonsterTransform();

            switch (pattern)
            {
                case 1: // 1. 一番遠いモンスターに突進
                    if (farthestMonster != null)
                        yield return movement.CoDashToTarget(farthestMonster);
                    break;

                case 2: // 2. 1を3回連続で繰り返す
                    for (int i = 0; i < 3; i++)
                    {
                        farthestMonster = GetFarthestMonsterTransform();
                        if (farthestMonster != null)
                        {
                            yield return movement.CoDashToTarget(farthestMonster);
                            yield return new WaitForSeconds(0.3f); // 突進間のわずかな溜め
                        }
                    }
                    break;

                case 3: // 3. 真ん中を中心に円周上を1周走る（半径を大きめの +4.5f に変更）
                    yield return movement.CoRunCircleAroundCenter(_summonSpawnRadius + 4.5f);
                    break;

                case 4: // 4. ジャンプして一番遠いモンスターの位置に着地 ＋ 衝撃波
                    if (farthestMonster != null)
                    {
                        yield return movement.CoJumpToTarget(farthestMonster, () => DoShockwave(cfg));
                    }
                    break;
            }
        }

        // 一番遠いモンスター（敵チーム = プレイヤー側モンスター）を取得
        Transform GetFarthestMonsterTransform()
        {
            var monsters = CharacterRegistry.All
                .Where(c => c != null && c.Team == Team.Player && c.IsAlive)
                .ToList();

            if (monsters.Count == 0) return null;

            return monsters
                .OrderByDescending(c => Vector3.Distance(transform.position, c.transform.position))
                .First().transform;
        }

        // --- 以下、既存の攻撃処理（DoShockwave, DoGroundTelegraph, TryDoSummon） ---
        public void DoShockwave(GameBalanceConfig cfg)
        {
            var radius = cfg != null ? cfg.BossShockwaveRadius : 6f;
            var damage = (cfg != null ? cfg.BossShockwaveDamage : 15f) * GetDamageMultiplier(cfg);
            var knockback = cfg != null ? cfg.BossShockwaveKnockback : 25f;

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

                var targetRb = targetIdentity.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    var dir = targetIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    targetRb.AddForce(dir.normalized * knockback, ForceMode.VelocityChange);
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

            // 門のオブジェクトが取得できている場合は50%の確率で門から出現、なければ必ず勇者の周り
            bool useGatePattern = (_demonCastleGatePoint != null) && (Random.value > 0.5f);

            // 召喚のエフェクト位置（基準点）
            Vector3 centerEffectPos = useGatePattern ? _demonCastleGatePoint.position : transform.position;

            for (var i = 0; i < count; i++)
            {
                var prefab = _summonPrefabs[Random.Range(0, _summonPrefabs.Length)];
                if (prefab == null) continue;

                Vector3 spawnPos;
                if (useGatePattern)
                {
                    // パターン2: 門の周辺に少し散らして召喚
                    Vector2 randomCircle = Random.insideUnitCircle * _gateSpawnRadius;
                    spawnPos = _demonCastleGatePoint.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
                }
                else
                {
                    // パターン1: 従来通り勇者の周りのリング上に召喚
                    var angle = Random.Range(0f, Mathf.PI * 2f);
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _summonSpawnRadius;
                    spawnPos = transform.position + offset;
                }

                var instance = Instantiate(prefab, spawnPos, Quaternion.identity);
                var summonIdentity = instance.GetComponent<CharacterIdentity>();
                if (summonIdentity != null) summonIdentity.Team = Team.Enemy;

                var summonActivation = instance.GetComponent<CharacterActivation>();
                if (summonActivation != null) summonActivation.SetActive(true);

                CombatFx.ImpactBurst(spawnPos + Vector3.up * 0.5f, SummonColor, 0.35f);
            }

            // 召喚リングエフェクトを発生させる
            ExplosionRingEffect.Spawn(centerEffectPos, useGatePattern ? _gateSpawnRadius : _summonSpawnRadius, SummonColor, 0.5f);
            SfxUtil.PlayAt(_summonSound, centerEffectPos);
            return true;
        }
    }
}
