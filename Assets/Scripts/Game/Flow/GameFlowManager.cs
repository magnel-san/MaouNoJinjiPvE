using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.HandTracking;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Flow
{
  /// <summary>
  /// ゲーム全体の流れを統括する。
  /// タイトル→ルール説明→カメラを戦場へ→(3択でキャラ採用2体→配置→ボス戦)を3回繰り返す。
  /// 各回、勝敗に関わらず次の回へ進み、2回目・3回目はキャラが2体ずつ増え、ボスも強くなる。
  /// </summary>
  public class GameFlowManager : MonoBehaviour
  {
    [Header("採用プール")]
    [SerializeField] private RecruitPool _recruitPool;
    [Tooltip("1回のボス戦の前に採用する人数")]
    [SerializeField] private int _recruitBatchSize = 2;
    [Tooltip("1ラウンドで表示する候補キャラの人数")]
    [SerializeField] private int _recruitOptionsPerRound = 3;

    [Header("ボス戦")]
    [Tooltip("ボスのプレハブ(Boss.prefab)。ラウンドごとにここから直接Instantiateする")]
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private Vector3 _bossSpawnPosition = new Vector3(0f, 2f, 0f);
    [Tooltip("ボス戦を行う回数。1回ごとに採用キャラが_recruitBatchSize体増え、" +
      "GameBalanceConfig.BossRoundMultiplierに応じてボスが強くなる")]
    [SerializeField] private int _battleRoundCount = 3;
    [Tooltip("各ラウンドの勝敗表示を出しておく秒数(自動で閉じて次のラウンドへ進む)")]
    [SerializeField] private float _roundResultDisplaySeconds = 3f;
    [Tooltip("ボスを撃破してから次に進むまでの猶予秒数。この間キャラ・入力は有効のままなので、" +
      "落ちているコインを拾いに行ける")]
    [SerializeField] private float _bossDeathGraceSeconds = 5f;

    [Header("ボス前の練習フェーズ")]
    [Tooltip("各ラウンドのボス出現前、雑魚のみが指定秒数だけ湧く練習時間(要素数=_battleRoundCount)。" +
      "0にするとそのラウンドは練習フェーズをスキップする")]
    [SerializeField] private float[] _practicePhaseSeconds = { 15f, 15f, 15f };
    [Tooltip("練習フェーズ中に召喚する雑魚プレハブ")]
    [SerializeField] private GameObject[] _practiceMinionPrefabs;
    [Tooltip("練習フェーズ中、雑魚を召喚する間隔(秒)")]
    [SerializeField] private float _practiceSpawnInterval = 3f;
    [Tooltip("「警告：ボスN」フルスクリーン演出を表示しておく秒数")]
    [SerializeField] private float _bossWarningDisplaySeconds = 3f;

    [Header("最終決戦(勇者)")]
    [Tooltip("3回のボス戦の後に出現する最終決戦のプレハブ。通常のボス(Boss.prefab)と同じものを想定")]
    [SerializeField] private GameObject _finalHeroPrefab;
    [Tooltip("「両手をパーにして必殺技(ビーム)を撃て！」の大きな指示UIを表示しておく秒数")]
    [SerializeField] private float _finalBattleInstructionDisplaySeconds = 4f;
    [Tooltip("ゲームクリア画面(GAME CLEARの一言表示)を出しておく秒数")]
    [SerializeField] private float _gameClearDisplaySeconds = 2.5f;

    [System.Serializable]
    public class ScoreRating
    {
      public int MinScore;
      public string RatingLabel;
      [TextArea] public string Comment;
    }

    [Header("リザルト評価(スコアに応じた魔王からの一言。MinScore以下で最も近い項目が採用される)")]
    [SerializeField] private ScoreRating[] _scoreRatings =
    {
      new ScoreRating { MinScore = 0, RatingLabel = "D", Comment = "うーん、もう少し励め" },
      new ScoreRating { MinScore = 200, RatingLabel = "C", Comment = "まあまあだな" },
      new ScoreRating { MinScore = 500, RatingLabel = "B", Comment = "なかなかやるな" },
      new ScoreRating { MinScore = 1000, RatingLabel = "A", Comment = "見事だ！" },
      new ScoreRating { MinScore = 2000, RatingLabel = "S", Comment = "完璧だ、我が軍の誇りだ！" },
    };

    [Header("タイトル / ルール画面")]
    [SerializeField] private GameObject _titleRoot;
    [SerializeField] private HoldToActivateButton _titleNextButton;
    [SerializeField] private GameObject _rulesRoot;
    [SerializeField] private HoldToActivateButton _rulesNextButton;

    [Header("3択UI")]
    [SerializeField] private ThreeChoiceCardUI _threeChoiceUI;

    [Header("配置フェーズ")]
    [SerializeField] private GameObject _portraitRoot;
    [SerializeField] private Image _portraitImage;
    [Tooltip("配置キャラのワールド座標基準点。下のオフセットはここからの相対座標として扱う")]
    [SerializeField] private Transform _deploymentAnchor;
    [Tooltip("採用したキャラを並べる固定配置地点(_deploymentAnchorからのローカルオフセット)。" +
      "3ラウンド合計で最大6体になるため6個用意している。採用数がこの数より多い場合は最後のオフセットを使い回す")]
    [SerializeField] private Vector3[] _fixedDeploymentOffsets = new Vector3[]
    {
      // _deploymentAnchorのローカルZ軸方向へ横一列に広げ、ボス方向(ローカルX)へわずかに弧を描く。
      new Vector3(0f, 0f, -7f),
      new Vector3(1f, 0f, -4.2f),
      new Vector3(1.8f, 0f, -1.4f),
      new Vector3(1.8f, 0f, 1.4f),
      new Vector3(1f, 0f, 4.2f),
      new Vector3(0f, 0f, 7f),
    };
    [Tooltip("新規採用キャラ1体ごとの表示間隔(秒)。0なら間を置かず一斉に配置する")]
    [SerializeField] private float _placementStepInterval = 0.4f;

    [Header("カメラ")]
    [SerializeField] private Camera _mainCamera;
    [Tooltip("戦場を映すカメラの位置・向き。未設定ならカメラ移動をスキップする")]
    [SerializeField] private Transform _battlefieldCameraPose;
    [SerializeField] private float _cameraMoveDuration = 1.5f;

    [Header("ゲーム開始")]
    [SerializeField] private GameObject _gameStartRoot;
    [SerializeField] private HoldToActivateButton _gameStartButton;

    [Header("勝敗(ラウンドごとに短時間表示してから次ラウンドへ)")]
    [SerializeField] private GameObject _winRoot;
    [SerializeField] private GameObject _loseRoot;
    [SerializeField] private float _battleCheckInterval = 0.5f;

    [Header("戦闘中の全体コマンド入力")]
    [Tooltip("BattleCursorInputDebug/BattleGestureInputControllerを乗せたGameObject。" +
      "戦闘開始直前に有効化し、勝敗確定後に無効化する(それ以外のフェーズでは常時無効)")]
    [SerializeField] private GameObject _battleInputRoot;

    private readonly List<CharacterRecruitOption> _recruitedCharacters = new List<CharacterRecruitOption>();
    private int _lastRecruitedCountThisRound;
    private bool _lastRoundWon;

    private void Start()
    {
      StartCoroutine(RunGameFlow());
    }

    private IEnumerator RunGameFlow()
    {
      // 他の全キャラのStart()(CharacterActivation.ActiveOnStartの反映)が終わるのを待ってから上書きする。
      yield return null;

      // スコア/コンボ/画面枠/警告UIはゲーム全体を通して常駐させるため、最初にまとめて用意しておく
      // (最初のコイン取得より前に用意しておかないと、ScoreBorderUIがOnScoreChangedの初回通知を
      // 取りこぼしてしまうため)。
      ScoreBorderUI.EnsureExists();
      ScoreUI.EnsureExists();
      ComboUI.EnsureExists();
      BossWarningUI.EnsureExists();

      // リザルト画面でEnterキーが押されたら、ここへ戻ってタイトルからやり直す。
      while (true)
      {
        yield return RunSingleGameAsync();
        ResetForNewGame();
      }
    }

    private IEnumerator RunSingleGameAsync()
    {
      SetAllCharactersActive(false);
      SetBattleInputActive(false);

      yield return ShowScreenAndWait(_titleRoot, _titleNextButton);
      yield return ShowScreenAndWait(_rulesRoot, _rulesNextButton);
      yield return HandPreferenceSelectUI.EnsureExists().WaitForSelection();

      yield return MoveCameraToBattlefield();

      for (var round = 1; round <= _battleRoundCount; round++)
      {
        yield return RunRecruitmentLoop(_recruitBatchSize);
        yield return RespawnAllPlayerCharacters(_lastRecruitedCountThisRound);

        yield return ShowScreenAndWait(_gameStartRoot, _gameStartButton);

        yield return RunPracticePhaseAsync(round);
        yield return BossWarningUI.ShowBossIntroAsync($"警告！ボス{round}", _bossWarningDisplaySeconds);

        DamageStatsTracker.Reset();
        SpawnBossForRound(round);
        SetAllCharactersActive(true);
        SetBattleInputActive(true);

        yield return WaitForRoundEnd();

        SetBattleInputActive(false);
        SetAllCharactersActive(false);

        yield return ShowRoundResult(_lastRoundWon);

        DespawnAllEnemies();
      }

      yield return RunFinalHeroEncounterAsync();
    }

    // リザルト画面でEnterキーを押した後、タイトルからやり直せるよう全状態をクリアする。
    private void ResetForNewGame()
    {
      foreach (var identity in CharacterRegistry.All.ToList())
      {
        if (identity == null) continue;
        Destroy(identity.gameObject);
      }

      _recruitedCharacters.Clear();
      _lastRecruitedCountThisRound = 0;
      _lastRoundWon = false;

      ScoreManager.Reset();
      DamageStatsTracker.Reset();

      // 最終決戦の撃破演出中に無効化したカメラ追従を元に戻す。
      var cameraFollower = FindAnyObjectByType<RallyCameraFollower>();
      if (cameraFollower != null) cameraFollower.enabled = true;
    }

    // 各ボス出現前、指定秒数だけ雑魚のみを湧かせ、操作感を試せる時間を作る。
    // 秒数0または召喚プレハブ未設定ならスキップする。
    private IEnumerator RunPracticePhaseAsync(int round)
    {
      if (_practiceMinionPrefabs == null || _practiceMinionPrefabs.Length == 0) yield break;

      var index = Mathf.Clamp(round - 1, 0, Mathf.Max(_practicePhaseSeconds.Length - 1, 0));
      var duration = _practicePhaseSeconds != null && _practicePhaseSeconds.Length > 0 ? _practicePhaseSeconds[index] : 0f;
      if (duration <= 0f) yield break;

      SetAllCharactersActive(true);
      SetBattleInputActive(true);

      var elapsed = 0f;
      var spawnTimer = 0f;
      while (elapsed < duration)
      {
        var dt = Time.deltaTime;
        elapsed += dt;
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
          spawnTimer = _practiceSpawnInterval;
          SpawnPracticeMinion();
        }
        yield return null;
      }

      SetBattleInputActive(false);
      SetAllCharactersActive(false);
      DespawnAllEnemies();
    }

    private void SpawnPracticeMinion()
    {
      if (_practiceMinionPrefabs == null || _practiceMinionPrefabs.Length == 0) return;

      var prefab = _practiceMinionPrefabs[Random.Range(0, _practiceMinionPrefabs.Length)];
      if (prefab == null) return;

      var angle = Random.Range(0f, Mathf.PI * 2f);
      var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4f;
      var instance = Instantiate(prefab, _bossSpawnPosition + offset, Quaternion.identity);

      var identity = instance.GetComponent<CharacterIdentity>();
      if (identity != null) identity.Team = Team.Enemy;

      var activation = instance.GetComponent<CharacterActivation>();
      if (activation != null) activation.SetActive(true);

      if (instance.GetComponent<CoinDropOnDeath>() == null) instance.AddComponent<CoinDropOnDeath>();
    }

    // 3回のボス戦の後の最終決戦。プレハブ未設定ならスキップする。
    // 通常のボスと同じプレハブを使い、そのAI(専用技・4パターン移動)は止めて、代わりに
    // 固定位置からのチャージ→ビーム攻撃(FinalHeroController)に差し替える。
    // 必殺ゲージは常に100%表示にし、両手パーを維持している間カメラから敵へビームを撃てる
    // (FinalBattleBeamController、回数制限無し)。
    private IEnumerator RunFinalHeroEncounterAsync()
    {
      if (_finalHeroPrefab == null) yield break;

      yield return BossWarningUI.ShowBossIntroAsync("警告！勇者、見参！", _bossWarningDisplaySeconds);

      var instance = Instantiate(_finalHeroPrefab, _bossSpawnPosition, Quaternion.Euler(0f, 180f, 0f));
      var identity = instance.GetComponent<CharacterIdentity>();
      if (identity != null)
      {
        identity.Team = Team.Enemy;
        identity.IsBoss = true;
      }

      var regularBossController = instance.GetComponent<BossController>();
      if (regularBossController != null) regularBossController.enabled = false;
      var regularBossMovement = instance.GetComponent<BossMovement>();
      if (regularBossMovement != null) regularBossMovement.enabled = false;

      if (instance.GetComponent<FinalHeroController>() == null) instance.AddComponent<FinalHeroController>();
      var deathReaction = instance.GetComponent<FinalHeroDeathReaction>();
      if (deathReaction == null) deathReaction = instance.AddComponent<FinalHeroDeathReaction>();
      var heroHpBar = instance.GetComponent<BossHpBarUI>();
      if (heroHpBar == null) heroHpBar = instance.AddComponent<BossHpBarUI>();
      heroHpBar.SetName("勇者");

      var ultimateGauge = _battleInputRoot != null ? _battleInputRoot.GetComponentInChildren<UltimateGaugeController>(true) : null;
      var beamController = _battleInputRoot != null ? _battleInputRoot.GetComponentInChildren<FinalBattleBeamController>(true) : null;
      if (beamController == null && _battleInputRoot != null) beamController = _battleInputRoot.AddComponent<FinalBattleBeamController>();
      if (beamController != null) beamController.SetTarget(identity);

      DamageStatsTracker.Reset();
      SetAllCharactersActive(true);
      SetBattleInputActive(true);
      if (ultimateGauge != null) ultimateGauge.SetFinalBattleMode(true);
      if (beamController != null) beamController.enabled = true;

      yield return BossWarningUI.ShowBossIntroAsync("両手をパーにして魔王の必殺技(ビーム)を撃て！", _finalBattleInstructionDisplaySeconds);

      yield return new WaitUntil(() =>
        (identity == null || !identity.IsAlive) ||
        !CharacterRegistry.All.Any(c => c != null && c.Team == Team.Player && c.IsAlive));

      var heroDefeated = identity == null || !identity.IsAlive;

      if (beamController != null) beamController.enabled = false;
      if (ultimateGauge != null) ultimateGauge.SetFinalBattleMode(false);
      SetBattleInputActive(false);
      SetAllCharactersActive(false);

      if (heroDefeated)
      {
        // 撃破演出(カメラフォーカス+回転+縮小+コインばらまき)が終わるまで待つ。
        if (deathReaction != null) yield return new WaitUntil(() => deathReaction.SequenceFinished);

        GameClearUI.Show(ScoreManager.TotalScore, _gameClearDisplaySeconds);
        yield return new WaitForSeconds(_gameClearDisplaySeconds);

        var rating = ResolveRating(ScoreManager.TotalScore);
        yield return GameResultUI.ShowAsync(DamageStatsTracker.TotalDamage, ScoreManager.CoinScore,
          ScoreManager.ComboScore, ScoreManager.TotalScore, rating.RatingLabel, rating.Comment);
      }
    }

    // スコアに応じた評価を、MinScore以下で最も近い項目から選ぶ。
    private ScoreRating ResolveRating(int score)
    {
      ScoreRating best = null;
      if (_scoreRatings != null)
      {
        foreach (var r in _scoreRatings)
        {
          if (r == null || score < r.MinScore) continue;
          if (best == null || r.MinScore > best.MinScore) best = r;
        }
      }
      return best ?? new ScoreRating { MinScore = 0, RatingLabel = "-", Comment = "" };
    }

    private void SetBattleInputActive(bool active)
    {
      if (_battleInputRoot != null) _battleInputRoot.SetActive(active);
      if (!active) BattleCommandState.Clear();
    }

    private static IEnumerator ShowScreenAndWait(GameObject root, HoldToActivateButton button)
    {
      if (root == null || button == null) yield break;

      var triggered = false;
      void Handler() => triggered = true;
      button.OnTriggered += Handler;

      root.SetActive(true);
      yield return new WaitUntil(() => triggered);

      button.OnTriggered -= Handler;
      root.SetActive(false);
    }

    // countで指定した人数を採用するまで3択(_recruitOptionsPerRound枚)を繰り返す。
    // 既に採用済みのキャラ(前ラウンドまでの分も含む)は候補から除外する。
    private IEnumerator RunRecruitmentLoop(int count)
    {
      _lastRecruitedCountThisRound = 0;
      if (_recruitPool?.Options == null || _threeChoiceUI == null) yield break;

      var available = new List<CharacterRecruitOption>(
        _recruitPool.Options.Where(o => o != null && !_recruitedCharacters.Contains(o)));

      for (var i = 0; i < count; i++)
      {
        if (available.Count == 0) break;

        // プールが尽きかけている場合は、残っている分だけ表示する(候補が枚数より少なくても選択自体は続けられるようにする)。
        var showCount = Mathf.Min(_recruitOptionsPerRound, available.Count);
        var shown = PickRandom(available, showCount);
        var sprites = shown.Select(o => o.ResumeSprite).ToArray();

        var selectedIndex = -1;
        void Handler(int idx) => selectedIndex = idx;
        _threeChoiceUI.OnOptionSelected += Handler;

        _threeChoiceUI.Show(sprites);
        yield return new WaitUntil(() => selectedIndex >= 0);

        _threeChoiceUI.OnOptionSelected -= Handler;
        _threeChoiceUI.Hide();

        var picked = shown[selectedIndex];
        _recruitedCharacters.Add(picked);
        available.Remove(picked);
        _lastRecruitedCountThisRound++;
      }
    }

    private IEnumerator MoveCameraToBattlefield()
    {
      if (_mainCamera == null || _battlefieldCameraPose == null) yield break;

      var startPos = _mainCamera.transform.position;
      var startRot = _mainCamera.transform.rotation;
      var t = 0f;

      while (t < _cameraMoveDuration)
      {
        t += Time.deltaTime;
        var frac = Mathf.Clamp01(t / _cameraMoveDuration);
        _mainCamera.transform.SetPositionAndRotation(
          Vector3.Lerp(startPos, _battlefieldCameraPose.position, frac),
          Quaternion.Slerp(startRot, _battlefieldCameraPose.rotation, frac));
        yield return null;
      }

      _mainCamera.transform.SetPositionAndRotation(_battlefieldCameraPose.position, _battlefieldCameraPose.rotation);
    }

    // 前ラウンドまでの生存キャラをいったん全て破棄し、これまで採用した全員を並べ直す
    // (勝敗に関わらず次ラウンドへ進むため、負けていても全員フルHPで再登場する)。
    // 既に前ラウンドから居るメンバーは演出無しで即座に、今ラウンドの新規採用分だけ
    // ポートレート表示付きで1体ずつ並べる。
    private IEnumerator RespawnAllPlayerCharacters(int newlyRecruitedCount)
    {
      foreach (var identity in CharacterRegistry.All.Where(c => c != null && c.Team == Team.Player).ToList())
      {
        Destroy(identity.gameObject);
      }
      yield return null; // Destroyの反映(CharacterRegistryからの登録解除)を待つ

      var veteranCount = Mathf.Max(0, _recruitedCharacters.Count - newlyRecruitedCount);

      for (var i = 0; i < veteranCount; i++)
      {
        SpawnCharacterAtFixedSlot(_recruitedCharacters[i], i);
      }

      for (var i = veteranCount; i < _recruitedCharacters.Count; i++)
      {
        var character = _recruitedCharacters[i];

        if (_portraitImage != null) _portraitImage.sprite = character.ResumeSprite;
        if (_portraitRoot != null) _portraitRoot.SetActive(true);

        SpawnCharacterAtFixedSlot(character, i);

        if (_placementStepInterval > 0f) yield return new WaitForSeconds(_placementStepInterval);

        if (_portraitRoot != null) _portraitRoot.SetActive(false);
      }
    }

    private void SpawnBossForRound(int round)
    {
      if (_bossPrefab == null) return;

      // ボスプレハブのデフォルト向きだとプレイヤー側と逆(背中)を向いてしまうため、Y軸180度回転させて出現させる。
      var instance = Instantiate(_bossPrefab, _bossSpawnPosition, Quaternion.Euler(0f, 180f, 0f));

      var identity = instance.GetComponent<CharacterIdentity>();
      if (identity != null) identity.Team = Team.Enemy;

      // MaxHPは5段階Tierに乗らない値のため、CharacterHealth.Start()より前(Instantiate直後の
      // このタイミング)で直接書き換える。ダメージ側の倍率はBossController.SetDifficultyRoundが
      // 攻撃発動のたびに参照するので、そちらはタイミングを気にせず設定すればよい。
      var stats = instance.GetComponent<CharacterStats>();
      if (stats != null)
      {
        stats.MaxHP *= GetBossRoundMultiplier(round);
      }

      var bossController = instance.GetComponent<BossController>();
      if (bossController != null) bossController.SetDifficultyRound(round);
    }

    private static float GetBossRoundMultiplier(int round)
    {
      var cfg = GameBalanceConfig.Instance;
      if (cfg?.BossRoundMultiplier == null || cfg.BossRoundMultiplier.Length == 0) return 1f;
      var index = Mathf.Clamp(round - 1, 0, cfg.BossRoundMultiplier.Length - 1);
      return cfg.BossRoundMultiplier[index];
    }

    private void SpawnCharacterAtFixedSlot(CharacterRecruitOption character, int slotIndex)
    {
      if (character?.CharacterPrefab == null) return;

      var worldPos = GetFixedDeploymentWorldPosition(slotIndex);
      var anchorRot = _deploymentAnchor != null ? _deploymentAnchor.rotation : Quaternion.identity;

      var instance = Instantiate(character.CharacterPrefab, worldPos, anchorRot);

      // 全体コマンド(集合/退避)を受け付けられるよう、プレハブ側の設定に関わらず必ず後付けする。
      // Instantiate直後・CharacterMovement.Start()が走るより前に追加するため、通常のIMovementIntentSource
      // 走査に確実に混ざる。
      if (instance.GetComponent<PlayerCommandIntentSource>() == null) instance.AddComponent<PlayerCommandIntentSource>();
      // グー(防御)コマンドの盾ビジュアルも同様に、プレハブ側の設定に関わらず必ず後付けする。
      if (instance.GetComponent<CharacterGuard>() == null) instance.AddComponent<CharacterGuard>();

      var activation = instance.GetComponent<CharacterActivation>();
      if (activation != null) activation.SetActive(false);
    }

    private Vector3 GetFixedDeploymentWorldPosition(int slotIndex)
    {
      var anchorPos = _deploymentAnchor != null ? _deploymentAnchor.position : Vector3.zero;
      var anchorRot = _deploymentAnchor != null ? _deploymentAnchor.rotation : Quaternion.identity;

      if (_fixedDeploymentOffsets == null || _fixedDeploymentOffsets.Length == 0) return anchorPos;

      // 採用数が配置枠より多い場合は最後の枠を使い回す(重なって表示される)。
      var clampedIndex = Mathf.Min(slotIndex, _fixedDeploymentOffsets.Length - 1);
      return anchorPos + anchorRot * _fixedDeploymentOffsets[clampedIndex];
    }

    private static void SetAllCharactersActive(bool active)
    {
      foreach (var identity in CharacterRegistry.All.ToList())
      {
        if (identity == null) continue;
        var activation = identity.GetComponent<CharacterActivation>();
        if (activation != null) activation.SetActive(active);
      }
    }

    private static void DespawnAllEnemies()
    {
      foreach (var identity in CharacterRegistry.All.Where(c => c != null && c.Team == Team.Enemy).ToList())
      {
        Destroy(identity.gameObject);
      }
    }

    // 1ラウンド分の戦闘終了を待つ。勝敗はyield break後に_lastRoundWonへ入っている。
    private IEnumerator WaitForRoundEnd()
    {
      while (true)
      {
        var playerAlive = CharacterRegistry.All.Any(c => c != null && c.Team == Team.Player && c.IsAlive);
        var enemyAlive = CharacterRegistry.All.Any(c => c != null && c.Team == Team.Enemy && c.IsAlive);

        if (!playerAlive)
        {
          _lastRoundWon = false;
          yield break;
        }
        if (!enemyAlive)
        {
          _lastRoundWon = true;
          // ボス撃破後、落ちているコインを拾いに行けるよう猶予秒数だけ待つ(キャラ・入力は有効のまま)。
          if (_bossDeathGraceSeconds > 0f) yield return new WaitForSeconds(_bossDeathGraceSeconds);
          yield break;
        }

        yield return new WaitForSeconds(_battleCheckInterval);
      }
    }

    // 勝敗に関わらず(このゲームは3回戦うため)、結果を少しだけ表示してから自動で次ラウンドへ進む。
    private IEnumerator ShowRoundResult(bool won)
    {
      RoundResultStatsUI.Show(DamageStatsTracker.Snapshot, _roundResultDisplaySeconds);

      var root = won ? _winRoot : _loseRoot;
      if (root == null) yield break;

      root.SetActive(true);
      yield return new WaitForSeconds(_roundResultDisplaySeconds);
      root.SetActive(false);
    }

    private static List<T> PickRandom<T>(List<T> source, int count)
    {
      var pool = new List<T>(source);
      var picked = new List<T>();
      var rng = new System.Random();
      while (picked.Count < count && pool.Count > 0)
      {
        var index = rng.Next(pool.Count);
        picked.Add(pool[index]);
        pool.RemoveAt(index);
      }
      return picked;
    }

    // デバッグ表示用: 固定配置地点(水色、デプロイ地点からの相対座標)とボスの出現位置(赤)を
    // シーンビューに円で常時表示する。GameFlowManagerの選択状態に関わらず表示するOnDrawGizmosを使う。
    private void OnDrawGizmos()
    {
      Gizmos.color = new Color(1f, 0.3f, 0.3f);
      TargetingUtility.DrawGizmoCircle(_bossSpawnPosition, 2f);

      if (_fixedDeploymentOffsets != null)
      {
        Gizmos.color = new Color(0.2f, 0.85f, 1f);
        var anchorPos = _deploymentAnchor != null ? _deploymentAnchor.position : Vector3.zero;
        var anchorRot = _deploymentAnchor != null ? _deploymentAnchor.rotation : Quaternion.identity;

        foreach (var offset in _fixedDeploymentOffsets)
        {
          TargetingUtility.DrawGizmoCircle(anchorPos + anchorRot * offset, 1.2f);
        }
      }
    }
  }
}
