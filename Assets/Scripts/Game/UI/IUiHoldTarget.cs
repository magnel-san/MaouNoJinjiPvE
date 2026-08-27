using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
  /// <summary>
  /// UiPointerControllerのホバー対象になれるUI要素が実装するインターフェース。
  /// HoldToActivateButton等で使用する。
  /// </summary>
  public interface IUiHoldTarget
  {
    RectTransform RectTransform { get; }
    void OnPointerHoldEnter();
    void OnPointerHoldExit();
  }

  /// <summary>
  /// シーン内のIUiHoldTargetを登録しておき、UiPointerControllerがスクリーン座標から
  /// 「今どのUI要素の上にいるか」をRectTransformの矩形判定だけで探せるようにする
  /// (3Dコライダー・EventSystem・GraphicRaycasterには依存しない)。
  /// </summary>
  public static class UiHoldTargetRegistry
  {
    static readonly List<IUiHoldTarget> all = new List<IUiHoldTarget>();

    public static void Register(IUiHoldTarget target)
    {
      if (!all.Contains(target)) all.Add(target);
    }

    public static void Unregister(IUiHoldTarget target)
    {
      all.Remove(target);
    }

    // 後から登録された(=手前に重なっている想定の)ものを優先して探す。
    public static IUiHoldTarget FindAt(Vector2 screenPosition, Camera eventCamera)
    {
      for (var i = all.Count - 1; i >= 0; i--)
      {
        var target = all[i];
        var rect = target?.RectTransform;
        if (rect == null || !rect.gameObject.activeInHierarchy) continue;

        if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
        {
          return target;
        }
      }
      return null;
    }
  }
}
