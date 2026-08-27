using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.HandTracking
{
  /// <summary>
  /// 指の「向き」は一切使わない。MediaPipeが検出した人差し指先の正規化座標
  /// (<see cref="HandTrackingController.TryGetIndexFingertipViewport"/>)を、そのままスクリーン座標
  /// (Screen.width/height倍)として扱い、UIカーソル(RectTransform)をその位置に動かす。
  /// 3Dシーンやコライダーには一切依存せず、ホバー判定もUiHoldTargetRegistry経由のRectTransform矩形判定で行う。
  ///
  /// カーソル(_cursorRect)は_canvasの直接の子である前提
  /// (RectTransformUtility.ScreenPointToLocalPointInRectangleの結果をそのままanchoredPositionに使うため)。
  /// </summary>
  public class UiPointerController : MonoBehaviour
  {
    [SerializeField] private HandTrackingController _handTrackingController;
    [Tooltip("画面上を動くカーソルのRectTransform。_canvasの直接の子として配置すること。")]
    [SerializeField] private RectTransform _cursorRect;
    [Tooltip("カーソルが乗っているCanvas。Screen Space - Camera / World Spaceの場合はworldCameraの設定を使う。")]
    [SerializeField] private Canvas _canvas;

    [Header("共通")]
    [Tooltip("右手・左手のどちらの人差し指をポインターに優先して使うか。" +
      "指定した方のデータが今無い場合は、もう片方の手にフォールバックする。")]
    [SerializeField] private bool _useRightHand = true;
    [Tooltip("ポインター位置の平滑化にかける時間(秒)。0で平滑化なし、値が大きいほど滑らかだが遅延が増える")]
    [SerializeField] private float _smoothingTime = 0.05f;

    [Header("デバッグ")]
    [Tooltip("ONの間は手のトラッキングを無視し、マウスの位置でカーソルを操作する。")]
    [SerializeField] private bool _debugUseMouse;

    private IUiHoldTarget _currentTarget;
    private Vector2? _smoothedScreenPos;

    public bool IsPointerActive { get; private set; }
    public Vector2 ScreenPosition { get; private set; }

    private Camera EventCamera =>
      _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

    private void Update()
    {
      if (!TryGetScreenPosition(out var screenPos))
      {
        SetPointerActive(false);
        return;
      }

      SetPointerActive(true);
      ApplySmoothedPosition(screenPos);
      PositionCursor(_smoothedScreenPos.Value);
      UpdateHoverTarget(UiHoldTargetRegistry.FindAt(_smoothedScreenPos.Value, EventCamera));
    }

    private bool TryGetScreenPosition(out Vector2 screenPos)
    {
      if (_debugUseMouse)
      {
        var mouse = Mouse.current;
        if (mouse == null)
        {
          screenPos = default;
          return false;
        }
        screenPos = mouse.position.ReadValue();
        return true;
      }

      if (_handTrackingController == null || !_handTrackingController.TryGetIndexFingertipViewport(_useRightHand, out var viewport))
      {
        screenPos = default;
        return false;
      }

      screenPos = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
      return true;
    }

    private void ApplySmoothedPosition(Vector2 targetPos)
    {
      if (_smoothingTime <= 0f || _smoothedScreenPos == null)
      {
        _smoothedScreenPos = targetPos;
      }
      else
      {
        var t = 1f - Mathf.Exp(-Time.deltaTime / _smoothingTime);
        _smoothedScreenPos = Vector2.Lerp(_smoothedScreenPos.Value, targetPos, t);
      }
      ScreenPosition = _smoothedScreenPos.Value;
    }

    private void PositionCursor(Vector2 screenPos)
    {
      if (_cursorRect == null) return;

      var canvasRect = _canvas != null ? _canvas.transform as RectTransform : _cursorRect.parent as RectTransform;
      if (canvasRect == null) return;

      if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, EventCamera, out var localPoint))
      {
        _cursorRect.anchoredPosition = localPoint;
      }
    }

    private void UpdateHoverTarget(IUiHoldTarget target)
    {
      if (target == _currentTarget) return;

      _currentTarget?.OnPointerHoldExit();
      _currentTarget = target;
      _currentTarget?.OnPointerHoldEnter();
    }

    private void SetPointerActive(bool active)
    {
      IsPointerActive = active;
      if (_cursorRect != null && _cursorRect.gameObject.activeSelf != active)
      {
        _cursorRect.gameObject.SetActive(active);
      }
      if (!active)
      {
        UpdateHoverTarget(null);
        _smoothedScreenPos = null;
      }
    }
  }
}
