using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
  /// <summary>
  /// 画面を横方向に分割し、カード画像を並べる「多択UI」(旧・3択固定から可変枚数対応に変更)。
  /// ヴァンパイアサバイバーズライクの選択演出を、HoldToActivateButton(円形ゲージでの保持選択)で行う。
  /// カード画像の上に、選択進捗を示す360度ゲージ画像(_gaugeSprite)を中央に重ねて表示する。
  /// Show()に渡すSprite配列の枚数がそのままカード枚数になる(呼び出しごとに変えてよい)。
  ///
  /// 画像比率の目安 (Inspectorで用意する素材向け):
  ///  - カード画像 (_cardSprites): 縦長 3:4 を推奨。列の中央に余白付きで収まるサイズ感。
  ///  - ゲージ画像 (_gaugeSprite): 正方形 1:1 必須 (Radial360で歪まないようにするため)。
  /// </summary>
  public class ThreeChoiceCardUI : MonoBehaviour
  {
    [Header("配置")]
    [Tooltip("カード列を並べる領域。未設定ならこのオブジェクト自身のRectTransformを使う。")]
    [SerializeField] private RectTransform _root;
    [Tooltip("カード間の余白(px)")]
    [SerializeField] private float _spacing = 24f;
    [Tooltip("カード列の上下の余白(px)")]
    [SerializeField] private float _verticalPadding = 48f;

    [Header("見た目")]
    [Tooltip("360度ゲージのオーバーレイ画像。正方形(1:1)のスプライトを指定すること。")]
    [SerializeField] private Sprite _gaugeSprite;
    [SerializeField] private Color _gaugeColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("ゲージ直径のカード幅に対する比率")]
    [Range(0.1f, 1f)] [SerializeField] private float _gaugeSizeRatio = 0.4f;

    [Header("選択")]
    [Tooltip("選択確定までの保持秒数")]
    [SerializeField] private float _holdSeconds = 2.5f;

    private RectTransform _layoutRoot;
    private readonly List<Image> _cardImages = new List<Image>();
    private readonly List<HoldToActivateButton> _holdButtons = new List<HoldToActivateButton>();
    private int _activeCount;

    /// <summary>いずれかのカードが選択確定した際、選ばれたインデックス(0始まり)を通知する。</summary>
    public event Action<int> OnOptionSelected;

    /// <summary>カーソルが乗っているカードが変わるたびに通知する。乗っていない間は-1。</summary>
    public event Action<int> OnOptionHoverChanged;

    /// <summary>カード画像を指定してUIを構築・表示する。枚数は配列の長さに応じて可変(呼び出しごとに変えられる)。</summary>
    public void Show(Sprite[] cardSprites)
    {
      if (cardSprites == null || cardSprites.Length == 0)
      {
        Debug.LogError("[ThreeChoiceCardUI] cardSpritesには1枚以上のSpriteを渡してください。");
        return;
      }

      if (_layoutRoot == null) BuildLayout();

      EnsureCardCount(cardSprites.Length);
      _activeCount = cardSprites.Length;

      for (var i = 0; i < _cardImages.Count; i++)
      {
        var active = i < cardSprites.Length;
        _cardImages[i].gameObject.SetActive(active);
        if (!active) continue;

        _cardImages[i].sprite = cardSprites[i];
        _holdButtons[i].enabled = true;
        _holdButtons[i].HoldSeconds = _holdSeconds;
      }

      gameObject.SetActive(true);
    }

    public void Hide()
    {
      gameObject.SetActive(false);
    }

    private void BuildLayout()
    {
      var parent = _root != null ? _root : (RectTransform)transform;

      var layoutGO = new GameObject("ThreeChoiceLayout", typeof(RectTransform));
      _layoutRoot = layoutGO.GetComponent<RectTransform>();
      _layoutRoot.SetParent(parent, false);
      _layoutRoot.anchorMin = Vector2.zero;
      _layoutRoot.anchorMax = Vector2.one;
      _layoutRoot.offsetMin = new Vector2(0f, _verticalPadding);
      _layoutRoot.offsetMax = new Vector2(0f, -_verticalPadding);

      var layoutGroup = layoutGO.AddComponent<HorizontalLayoutGroup>();
      layoutGroup.spacing = _spacing;
      layoutGroup.childAlignment = TextAnchor.MiddleCenter;
      layoutGroup.childForceExpandWidth = true;
      layoutGroup.childForceExpandHeight = true;
      layoutGroup.childControlWidth = true;
      layoutGroup.childControlHeight = true;
    }

    private void EnsureCardCount(int count)
    {
      while (_cardImages.Count < count)
      {
        BuildCard(_cardImages.Count);
      }
    }

    private void BuildCard(int index)
    {
      var cardGO = new GameObject($"Card_{index}", typeof(RectTransform));
      var cardRect = cardGO.GetComponent<RectTransform>();
      cardRect.SetParent(_layoutRoot, false);

      var cardImage = cardGO.AddComponent<Image>();
      cardImage.preserveAspect = true;
      _cardImages.Add(cardImage);

      var gaugeGO = new GameObject("Gauge", typeof(RectTransform));
      var gaugeRect = gaugeGO.GetComponent<RectTransform>();
      gaugeRect.SetParent(cardRect, false);
      gaugeRect.anchorMin = gaugeRect.anchorMax = new Vector2(0.5f, 0.5f);
      gaugeRect.pivot = new Vector2(0.5f, 0.5f);
      gaugeRect.sizeDelta = Vector2.zero; // BuildLayoutのLateUpdateではなく初回選択時に実サイズへ追従させる

      var gaugeImage = gaugeGO.AddComponent<Image>();
      gaugeImage.sprite = _gaugeSprite;
      gaugeImage.color = _gaugeColor;
      gaugeImage.type = Image.Type.Filled;
      gaugeImage.fillMethod = Image.FillMethod.Radial360;
      gaugeImage.fillOrigin = (int)Image.Origin360.Top;
      gaugeImage.fillClockwise = true;
      gaugeImage.fillAmount = 0f;
      gaugeImage.raycastTarget = false;

      var sizer = gaugeGO.AddComponent<GaugeSizer>();
      sizer.Initialize(cardRect, gaugeRect, _gaugeSizeRatio);

      var holdButton = cardGO.AddComponent<HoldToActivateButton>();
      holdButton.SetGaugeImage(gaugeImage);
      holdButton.HoldSeconds = _holdSeconds;

      var capturedIndex = index;
      holdButton.OnTriggered += () => HandleOptionTriggered(capturedIndex);
      holdButton.OnHoverChanged += hovering => OnOptionHoverChanged?.Invoke(hovering ? capturedIndex : -1);

      _holdButtons.Add(holdButton);
    }

    private void HandleOptionTriggered(int index)
    {
      // 確定したカード以外は選べないようにする(誤って2枚同時に確定しないため)。
      for (var i = 0; i < _activeCount; i++)
      {
        if (i != index) _holdButtons[i].enabled = false;
      }
      OnOptionSelected?.Invoke(index);
    }

    // カードの実サイズ(HorizontalLayoutGroup確定後)に応じて、ゲージを正方形で追従させる。
    private class GaugeSizer : MonoBehaviour
    {
      private RectTransform _card;
      private RectTransform _gauge;
      private float _ratio;

      public void Initialize(RectTransform card, RectTransform gauge, float ratio)
      {
        _card = card;
        _gauge = gauge;
        _ratio = ratio;
      }

      private void LateUpdate()
      {
        if (_card == null || _gauge == null) return;
        var size = Mathf.Min(_card.rect.width, _card.rect.height) * _ratio;
        _gauge.sizeDelta = new Vector2(size, size);
      }
    }
  }
}
