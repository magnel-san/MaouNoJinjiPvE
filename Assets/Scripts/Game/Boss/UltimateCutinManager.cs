using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 魔王の必殺技発動時にド派手なカットイン演出を制御するマネージャー
    public class UltimateCutinManager : MonoBehaviour
    {
        public static UltimateCutinManager Instance { get; private set; }

        [Header("UI設定")]
        [SerializeField] private Image _cutinImage;
        [SerializeField] private RectTransform _cutinRect;

        [Header("必殺技カットインイラスト")]
        [SerializeField] private Sprite _ultimateSprite;

        [Header("演出パラメータ")]
        [Tooltip("画面中央で決めポーズを維持する時間（秒）")]
        [SerializeField] private float _holdDuration = 0.5f;

        private Coroutine _animCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (_cutinImage == null) _cutinImage = GetComponent<Image>();
            if (_cutinRect == null) _cutinRect = GetComponent<RectTransform>();

            _cutinImage.enabled = false;
        }

        // 必殺技発動時に呼び出すメソッド
        public void PlayUltimateCutin()
        {
            if (_ultimateSprite == null) return;

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(CoPlayUltimateEffect());
        }

        private IEnumerator CoPlayUltimateEffect()
        {
            _cutinImage.sprite = _ultimateSprite;
            _cutinImage.enabled = true;

            // 初期位置：画面左外（大きくハミ出た位置）
            Vector2 startPos = new Vector2(-Screen.width, 0f);
            Vector2 centerPos = Vector2.zero;
            Vector2 exitPos = new Vector2(Screen.width, 0f);

            // 1. 画面左から中央へ超高速スライドイン！
            _cutinRect.anchoredPosition = startPos;
            _cutinRect.localScale = new Vector3(1.5f, 1.5f, 1f); // 巨大な状態から

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 12f; // 超高速
                _cutinRect.anchoredPosition = Vector2.Lerp(startPos, centerPos, t);
                _cutinRect.localScale = Vector3.Lerp(new Vector3(1.5f, 1.5f, 1f), Vector3.one, t);
                yield return null;
            }

            // 2. 画面中央でバシッと静止（必殺技の溜め演出）
            _cutinRect.anchoredPosition = centerPos;
            _cutinRect.localScale = Vector3.one;
            yield return new WaitForSeconds(_holdDuration);

            // 3. 画面右側へ斬り抜けるように高速退場
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 10f;
                _cutinRect.anchoredPosition = Vector2.Lerp(centerPos, exitPos, t);
                yield return null;
            }

            _cutinImage.enabled = false;
            _animCoroutine = null;
        }
    }
}
