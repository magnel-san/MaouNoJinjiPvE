using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
  /// <summary>
  /// 円状ポインター合わせゲージ。UiPointerControllerのカーソルが指定秒数だけこの上に留まると発火する。
  /// 3Dコライダーには依存せず、UiHoldTargetRegistryへ登録してRectTransformの矩形判定でホバーを検出する。
  /// </summary>
  [RequireComponent(typeof(RectTransform))]
  public class HoldToActivateButton : MonoBehaviour, IUiHoldTarget
  {
    [Tooltip("保持完了までの秒数")]
    [SerializeField] private float _holdSeconds = 3f;
    [Tooltip("ポインターを重ねている間を可視化する円形ゲージ (Image.fillAmountをType=Radial360で使用)")]
    [SerializeField] private Image _gaugeImage;
    [Tooltip("発火後、自動でゲージをリセットするか")]
    [SerializeField] private bool _resetAfterTrigger = true;
    [Tooltip("決定(保持完了)時に鳴らすUI効果音。未設定なら無音。このボタン経由の決定操作全て" +
      "(タイトル/ルール次へ、利き手選択、キャラ採用選択等)に共通で使われる")]
    [SerializeField] private AudioClip _confirmSound;

    public event Action OnTriggered;
    /// <summary>ホバー開始/終了のたびに発火する(true=開始, false=終了)。陣形プレビュー等、保持完了を待たずに使いたい場合用。</summary>
    public event Action<bool> OnHoverChanged;

    private RectTransform _rectTransform;
    private bool _isHovering;
    private float _heldSeconds;
    private bool _triggeredOnce;

    public RectTransform RectTransform =>
      _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

    public float HoldSeconds
    {
      get => _holdSeconds;
      set => _holdSeconds = value;
    }

    public bool IsHovering => _isHovering;

    /// <summary>実行時に生成したImageをゲージとして割り当てる(手組みのプレハブではInspectorで直接指定すればよい)。</summary>
    public void SetGaugeImage(Image image) => _gaugeImage = image;

    private void OnEnable()
    {
      UiHoldTargetRegistry.Register(this);
    }

    private void OnDisable()
    {
      UiHoldTargetRegistry.Unregister(this);
      _isHovering = false;
      _heldSeconds = 0f;
      UpdateGauge();
    }

    private void Update()
    {
      if (!_isHovering)
      {
        if (_heldSeconds > 0f)
        {
          _heldSeconds = 0f;
          UpdateGauge();
        }
        return;
      }

      if (_triggeredOnce && !_resetAfterTrigger) return;

      _heldSeconds += Time.deltaTime;
      UpdateGauge();

      if (_heldSeconds >= _holdSeconds)
      {
        Fire();
      }
    }

    private void UpdateGauge()
    {
      if (_gaugeImage != null)
      {
        _gaugeImage.fillAmount = _holdSeconds <= 0f ? 0f : Mathf.Clamp01(_heldSeconds / _holdSeconds);
      }
    }

    private void Fire()
    {
      _heldSeconds = 0f;
      _triggeredOnce = true;
      UpdateGauge();
      SfxUtil.PlayUi(_confirmSound);
      OnTriggered?.Invoke();
    }

    /// <summary>保持を進めずに即座に発火させたい場合(デバッグ等)に呼ぶ。</summary>
    public void TriggerImmediately() => Fire();

    public void OnPointerHoldEnter()
    {
      _isHovering = true;
      OnHoverChanged?.Invoke(true);
    }

    public void OnPointerHoldExit()
    {
      _isHovering = false;
      OnHoverChanged?.Invoke(false);
    }
  }
}
