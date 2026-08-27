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

        SpawnBossForRound(round);
        SetAllCharactersActive(true);
        SetBattleInputActive(true);

        yield return WaitForRoundEnd();

        SetBattleInputActive(false);
        SetAllCharactersActive(false);

        yield return ShowRoundResult(_lastRoundWon);

        DespawnAllEnemies();
      }
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

      var instance = Instantiate(_bossPrefab, _bossSpawnPosition, Quaternion.identity);

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
          yield break;
        }

        yield return new WaitForSeconds(_battleCheckInterval);
      }
    }

    // 勝敗に関わらず(このゲームは3回戦うため)、結果を少しだけ表示してから自動で次ラウンドへ進む。
    private IEnumerator ShowRoundResult(bool won)
    {
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
