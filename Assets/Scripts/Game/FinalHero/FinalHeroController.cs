using System.Collections;
using UnityEngine;

namespace Game
{
    // 最終決戦の勇者。固定位置に出現し、動かない(BossControllerのUpdateタイマー方式を踏襲)。
    // チャージ(専用攻撃アニメ)→前方へ細長いビームを撃つ攻撃をクールダウンで繰り返す。
    // 既存の勇者プレハブ(剣士勇者等)にGameFlowManagerが動的に後付けする想定のコンポーネント。
    [RequireComponent(typeof(CharacterIdentity), typeof(CharacterHealth), typeof(CharacterStats))]
    public class FinalHeroController : MonoBehaviour
    {
        [Header("チャージ→ビーム攻撃")]
        [SerializeField] private float _chargeCooldown = 6f;
        [Tooltip("両手をパーにしてチャージする溜め時間(秒)。この間によける")]
        [SerializeField] private float _chargeSeconds = 1.5f;
        [SerializeField] private float _beamWidth = 3f;
        [SerializeField] private float _beamLength = 40f;
        [SerializeField] private float _beamDamage = 30f;

        [Header("撃破時のコインばらまき")]
        [SerializeField] private int _deathCoinCount = 24;
        [SerializeField] private float _deathCoinScatterRadius = 3.5f;

        [Header("アニメーション連携")]
        [Tooltip("未設定なら子オブジェクトから自動検索する")]
        [SerializeField] private Animator _animator;

        const string AnimSpecialAttack = "SpecialAttack";

        static readonly Color BeamColor = new Color(1f, 0.85f, 0.3f);

        CharacterIdentity identity;
        CharacterStats stats;
        CharacterHealth health;
        float chargeTimer;
        float lastKnownHp;
        float lastCoinDropHpFraction = 1f;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            stats = GetComponent<CharacterStats>();
            health = GetComponent<CharacterHealth>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

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

        void Start()
        {
            // 接触ダメージは無害化する(ボスと同じ方針)。実際の攻撃はビームのみで行う。
            stats.OverrideAttackPower(0f);
            chargeTimer = _chargeCooldown * 0.5f; // 出現直後すぐには撃たせず、少し様子を見せる
        }

        // 通常のボス(BossController)を無効化しているため、コンボ判定・HP割合コインドロップは
        // ここで自前で行う(BossController.HandleHpChangedと同じロジック)。
        void HandleHpChanged(float current, float max)
        {
            if (current < lastKnownHp)
            {
                ComboTracker.RegisterHit();
                TryDropCoinsForHpChange(current, max);
            }
            lastKnownHp = current;
        }

        // 敵の落とすコインは全体的に倍化する方針のため、1段階につき2枚落とす。
        const int CoinsPerHpStep = 2;

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
                for (var i = 0; i < CoinsPerHpStep; i++)
                {
                    var offset = Random.insideUnitCircle * 0.5f;
                    CoinPickup.Spawn(transform.position + new Vector3(offset.x, 0f, offset.y));
                }
            }
        }

        // 撃破時、大量のコインを周囲へばらまく(FinalHeroDeathReactionの演出中の散布とは別に、即座に落とす分)。
        void HandleDied()
        {
            for (var i = 0; i < _deathCoinCount; i++)
            {
                var offset = Random.insideUnitCircle * _deathCoinScatterRadius;
                CoinPickup.Spawn(transform.position + new Vector3(offset.x, 0f, offset.y));
            }
        }

        void Update()
        {
            chargeTimer -= Time.deltaTime;
            if (chargeTimer <= 0f)
            {
                chargeTimer = _chargeCooldown;
                StartCoroutine(CoChargeAndFire());
            }
        }

        IEnumerator CoChargeAndFire()
        {
            if (_animator != null) _animator.SetTrigger(AnimSpecialAttack);
            BossWarningUI.ShowInstruction("よけろ！", _chargeSeconds + 0.5f);
            ScoreBorderUI.FlashRed(_chargeSeconds + 0.3f);
            CombatFx.ImpactBurst(transform.position + Vector3.up * 1.2f, BeamColor, 0.5f);

            yield return new WaitForSeconds(_chargeSeconds);

            FireBeam();
        }

        // ビームは瞬間発射(チャージ演出そのものが着弾までの予告を兼ねる)。
        // 前方の細長いレーン内にいるプレイヤーキャラに命中、レーン外なら「よけた」扱いにする。
        void FireBeam()
        {
            if (!identity.IsAlive) return; // チャージ中に撃破された場合、死後にビームが出ないようにする

            CombatFx.ImpactBurst(transform.position + transform.forward * (_beamLength * 0.5f), BeamColor, 0.8f);

            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || !c.IsAlive || c.Team == identity.Team) continue;

                var local = transform.InverseTransformPoint(c.transform.position);
                var inLane = local.z >= 0f && local.z <= _beamLength && Mathf.Abs(local.x) <= _beamWidth * 0.5f;

                var health = c.GetComponent<CharacterHealth>();
                if (inLane && health != null && health.IsAlive)
                {
                    health.ApplyDamage(_beamDamage, BeamColor, identity);
                    BossAttackFx.NotifyPlayerHit(c);
                }
                else if (c.Team == Team.Player)
                {
                    BossAttackFx.NotifyPlayerDodged(c);
                }
            }
        }
    }
}
