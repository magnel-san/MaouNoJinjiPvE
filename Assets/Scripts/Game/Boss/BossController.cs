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

        [Header("アニメーション連携")]
        [Tooltip("未設定なら子オブジェクトから自動検索する")]
        [SerializeField] private Animator _animator;

        const string AnimSpawn = "Spawn";
        const string AnimSpecialAttack = "SpecialAttack";
        const string AnimKnockback = "Knockback";
        const string AnimMoveSpeed = "MoveSpeed";
        const string AnimIsMoving = "IsMoving";

        static readonly Color ShockwaveColor = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color SummonColor = new Color(0.35f, 0.05f, 0.45f);

        CharacterIdentity identity;
        CharacterStats stats;
        CharacterHealth health;
        BossMovement movement;
        Rigidbody rb;

        // 名前検索で自動取得した門のTransformを保持する変数
        private Transform _demonCastleGatePoint;

        int difficultyRound = 1;

        float shockwaveTimer;
        float summonTimer;
        float patternTimer; // 新移動パターンのタイマー
        float directionalWipeTimer;
        float fullCoverTimer;
        float centerRingTimer;

        float lastKnownHp;
        float lastCoinDropHpFraction = 1f;
        Vector3 lastPosition;

        // ボス専用技1〜3が互いに重ならないようにするための排他フラグ(技の発動〜着弾までtrue)。
        bool _specialAttackActive;

        public void SetDifficultyRound(int round) => difficultyRound = Mathf.Max(1, round);

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            stats = GetComponent<CharacterStats>();
            health = GetComponent<CharacterHealth>();
            movement = GetComponent<BossMovement>();
            rb = GetComponent<Rigidbody>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        [Header("撃破時のコインばらまき")]
        [SerializeField] private int _deathCoinCount = 24;
        [SerializeField] private float _deathCoinScatterRadius = 3.5f;

        void OnEnable()
        {
            if (health != null)
            {
                health.OnHPChanged += HandleHpChanged;
                health.OnDied += HandleDied;
            }
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.OnHPChanged -= HandleHpChanged;
                health.OnDied -= HandleDied;
            }
        }

        // ボス撃破時、大量のコインを周囲へばらまく。
        void HandleDied()
        {
            for (var i = 0; i < _deathCoinCount; i++)
            {
                var offset = Random.insideUnitCircle * _deathCoinScatterRadius;
                CoinPickup.Spawn(transform.position + new Vector3(offset.x, 0f, offset.y));
            }
        }

        void Start()
        {
            stats.OverrideAttackPower(0f);
            lastPosition = transform.position;

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
            summonTimer = cfg != null ? cfg.BossSummonCooldown : 12f;
            directionalWipeTimer = cfg != null ? cfg.BossDirectionalWipeCooldown : 9f;
            fullCoverTimer = cfg != null ? cfg.BossFullCoverCooldown : 11f;
            centerRingTimer = cfg != null ? cfg.BossCenterRingCooldown : 13f;
            patternTimer = 1f; // 4秒ごとに移動アクションを開始

            if (_animator != null) _animator.SetTrigger(AnimSpawn);
        }

        // ボス自身が被弾してHPが減った瞬間(=攻撃を受けた瞬間)にコンボを進め、
        // HPが一定割合(GameBalanceConfig.BossCoinDropHpStepPercent)刻みで削れるたびにコインを落とす。
        void HandleHpChanged(float current, float max)
        {
            if (current < lastKnownHp)
            {
                ComboTracker.RegisterHit();
                TryDropCoinsForHpChange(current, max);
            }
            lastKnownHp = current;
        }

        void TryDropCoinsForHpChange(float current, float max)
        {
            if (max <= 0f) return;

            var cfg = GameBalanceConfig.Instance;
            var step = (cfg != null ? cfg.BossCoinDropHpStepPercent : 5f) / 100f;
            if (step <= 0f) return;

            var fraction = current / max;
            while (fraction <= lastCoinDropHpFraction - step)
            {
                lastCoinDropHpFraction -= step;
                CoinPickup.Spawn(transform.position);
            }
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

            UpdateMovementAnimator();

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

            // 通常攻撃: 吹き飛ばし(単独タイマー起点はウインドアップを挟んで避けやすくする)
            shockwaveTimer -= Time.deltaTime;
            if (shockwaveTimer <= 0f)
            {
                shockwaveTimer = cfg != null ? cfg.BossShockwaveCooldown : 7f;
                try { StartCoroutine(CoShockwaveWindup(cfg)); } catch (System.Exception e) { Debug.LogException(e, this); }
            }

            // ボス専用技1〜3は互いに同時発動しないようにする(_specialAttackActiveで排他制御。
            // 通常攻撃(吹き飛ばし/召喚)やBossMovementの4パターンとは重なってよい)。
            const float specialAttackRetryDelay = 1f;

            // ボス専用技1: 予告攻撃を画面右/左/上/下全体にする攻撃パターン(ラウンド1以降で解禁)
            directionalWipeTimer -= Time.deltaTime;
            if (directionalWipeTimer <= 0f)
            {
                if (difficultyRound >= 1 && !_specialAttackActive)
                {
                    directionalWipeTimer = cfg != null ? cfg.BossDirectionalWipeCooldown : 9f;
                    try { DoDirectionalWipe(cfg); } catch (System.Exception e) { Debug.LogException(e, this); }
                }
                else
                {
                    directionalWipeTimer = specialAttackRetryDelay;
                }
            }

            // ボス専用技2: 画面全体を覆う攻撃、グー防御で0ダメージ(ラウンド2以降で解禁)
            fullCoverTimer -= Time.deltaTime;
            if (fullCoverTimer <= 0f)
            {
                if (difficultyRound >= 2 && !_specialAttackActive)
                {
                    fullCoverTimer = cfg != null ? cfg.BossFullCoverCooldown : 11f;
                    try { DoFullCoverPulse(cfg); } catch (System.Exception e) { Debug.LogException(e, this); }
                }
                else
                {
                    fullCoverTimer = specialAttackRetryDelay;
                }
            }

            // ボス専用技3: ボス中心から円状の予告攻撃(ラウンド3以降で解禁)
            centerRingTimer -= Time.deltaTime;
            if (centerRingTimer <= 0f)
            {
                if (difficultyRound >= 3 && !_specialAttackActive)
                {
                    centerRingTimer = cfg != null ? cfg.BossCenterRingCooldown : 13f;
                    try { DoCenterRing(cfg); } catch (System.Exception e) { Debug.LogException(e, this); }
                }
                else
                {
                    centerRingTimer = specialAttackRetryDelay;
                }
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

        // 移動アニメーション用: 実際の位置変化から速度を推定する(BossMovementがMovePositionで
        // 動かすためRigidbody.linearVelocityに頼らない、物理駆動でなくても正しく動く方式)。
        void UpdateMovementAnimator()
        {
            if (_animator != null)
            {
                var delta = transform.position - lastPosition;
                delta.y = 0f;
                var speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                _animator.SetFloat(AnimMoveSpeed, speed);
                _animator.SetBool(AnimIsMoving, movement.IsMoving);
            }
            lastPosition = transform.position;
        }

        IEnumerator CoShockwaveWindup(GameBalanceConfig cfg)
        {
            var windup = cfg != null ? cfg.BossKnockbackWindupSeconds : 0.6f;
            if (windup > 0f) yield return new WaitForSeconds(windup);
            DoShockwave(cfg);
        }

        // ボス専用技1: 上下左右いずれかをランダムに選び、アリーナのその半面を覆う矩形予告を出す。
        void DoDirectionalWipe(GameBalanceConfig cfg)
        {
            var warning = cfg != null ? cfg.BossDirectionalWipeWarningSeconds : 2f;
            var damage = (cfg != null ? cfg.BossDirectionalWipeDamage : 18f) * GetDamageMultiplier(cfg);
            BeginSpecialAttack(warning);

            var center = movement.AreaCenter;
            var radius = movement.AreaRadius;

            Vector2 size;
            Vector3 zoneCenter;
            switch (Random.Range(0, 4))
            {
                case 0: // 右半分
                    size = new Vector2(radius, radius * 2f);
                    zoneCenter = center + new Vector3(radius * 0.5f, 0f, 0f);
                    break;
                case 1: // 左半分
                    size = new Vector2(radius, radius * 2f);
                    zoneCenter = center + new Vector3(-radius * 0.5f, 0f, 0f);
                    break;
                case 2: // 奥半分
                    size = new Vector2(radius * 2f, radius);
                    zoneCenter = center + new Vector3(0f, 0f, radius * 0.5f);
                    break;
                default: // 手前半分
                    size = new Vector2(radius * 2f, radius);
                    zoneCenter = center + new Vector3(0f, 0f, -radius * 0.5f);
                    break;
            }
            zoneCenter.y = transform.position.y;

            RectTelegraphZone.Spawn(zoneCenter, size, warning, damage, identity, false, _telegraphWarningSound, _telegraphDetonateSound);
            TriggerSpecialAttackAnim();
            BossWarningUI.ShowInstruction("人差し指でキャラを移動させてよけろ！", warning + 0.5f);
            ScoreBorderUI.FlashRed(warning + 0.3f);
        }

        // ボス専用技2: 位置に関わらずアリーナ全体に命中する予告攻撃。グー防御中は完全無効化する。
        void DoFullCoverPulse(GameBalanceConfig cfg)
        {
            var warning = cfg != null ? cfg.BossFullCoverWarningSeconds : 1.8f;
            var damage = (cfg != null ? cfg.BossFullCoverDamage : 22f) * GetDamageMultiplier(cfg);
            BeginSpecialAttack(warning);

            var center = movement.AreaCenter;
            var radius = movement.AreaRadius;
            var size = new Vector2(radius * 2.2f, radius * 2.2f); // アリーナ全体を覆うのに十分な余裕

            RectTelegraphZone.Spawn(center, size, warning, damage, identity, true, _telegraphWarningSound, _telegraphDetonateSound);
            TriggerSpecialAttackAnim();
            BossWarningUI.ShowInstruction("グーで防御しろ", warning + 0.5f);
            ScoreBorderUI.FlashRed(warning + 0.3f);
        }

        // ボス専用技3: ボスの現在位置を中心にした大型の円状予告攻撃。
        void DoCenterRing(GameBalanceConfig cfg)
        {
            var warning = cfg != null ? cfg.BossCenterRingWarningSeconds : 1.8f;
            var radius = cfg != null ? cfg.BossCenterRingRadius : 10f;
            var damage = (cfg != null ? cfg.BossCenterRingDamage : 24f) * GetDamageMultiplier(cfg);
            BeginSpecialAttack(warning);

            GroundTelegraphZone.Spawn(transform.position, radius, warning, damage, identity, _telegraphWarningSound, _telegraphDetonateSound);
            TriggerSpecialAttackAnim();
            BossWarningUI.ShowInstruction("パーで離れろ！", warning + 0.5f);
            ScoreBorderUI.FlashRed(warning + 0.3f);
        }

        void TriggerSpecialAttackAnim()
        {
            if (_animator != null) _animator.SetTrigger(AnimSpecialAttack);
        }

        // 専用技1〜3が互いに重ならないよう、発動〜着弾までの間_specialAttackActiveを立てる。
        void BeginSpecialAttack(float warningSeconds)
        {
            _specialAttackActive = true;
            StartCoroutine(CoClearSpecialAttackFlag(warningSeconds));
        }

        IEnumerator CoClearSpecialAttackFlag(float warningSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, warningSeconds) + 0.1f);
            _specialAttackActive = false;
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

        // --- 以下、既存の攻撃処理（DoShockwave, TryDoSummon） ---
        // 通常攻撃「吹き飛ばす」。ジャンプ着地(BossMovement.CoJumpToTarget)のコールバックからも
        // 直接呼ばれる(そちらは着地予告UI自体が予備動作を兼ねるため、ここでは追加のウインドアップを挟まない)。
        public void DoShockwave(GameBalanceConfig cfg)
        {
            var radius = cfg != null ? cfg.BossShockwaveRadius : 6f;
            var damage = (cfg != null ? cfg.BossShockwaveDamage : 15f) * GetDamageMultiplier(cfg);
            var knockback = cfg != null ? cfg.BossShockwaveKnockback : 25f;

            TriggerKnockbackAnim();
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
                health.ApplyDamage(damage, ShockwaveColor, identity);
                BossAttackFx.NotifyPlayerHit(targetIdentity);

                var targetRb = targetIdentity.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    var dir = targetIdentity.transform.position - transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.onUnitSphere;
                    targetRb.AddForce(dir.normalized * knockback, ForceMode.VelocityChange);
                }
            }
        }

        void TriggerKnockbackAnim()
        {
            if (_animator != null) _animator.SetTrigger(AnimKnockback);
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

                // 子分(召喚された雑魚)の死亡時にコインを落とせるよう、プレハブ側の設定に関わらず必ず後付けする。
                if (instance.GetComponent<CoinDropOnDeath>() == null) instance.AddComponent<CoinDropOnDeath>();

                CombatFx.ImpactBurst(spawnPos + Vector3.up * 0.5f, SummonColor, 0.35f);
            }

            // 召喚リングエフェクトを発生させる
            ExplosionRingEffect.Spawn(centerEffectPos, useGatePattern ? _gateSpawnRadius : _summonSpawnRadius, SummonColor, 0.5f);
            SfxUtil.PlayAt(_summonSound, centerEffectPos);
            return true;
        }
    }
}
