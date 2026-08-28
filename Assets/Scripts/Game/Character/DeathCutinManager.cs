using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    // 敵死亡時に漫画風カットインを左右交互/ランダムにハイスピードで表示するマネージャー
    public class DeathCutinManager : MonoBehaviour
    {
        public static DeathCutinManager Instance { get; private set; }

        [Header("表示用UI（Image）")]
        [SerializeField] private Image _cutinImage;
        [SerializeField] private RectTransform _cutinRect;

        [Header("カットイン素材イラスト")]
        [SerializeField] private List<Sprite> _cutinSprites = new List<Sprite>();

        [Header("演出パラメータ")]
        [Tooltip("画面に表示されている全体の維持時間（秒）")]
        [SerializeField] private float _displayDuration = 0.35f;
        [Tooltip("出現・消失アニメーションのスピード")]
        [SerializeField] private float _animSpeed = 15f;

        private Coroutine _currentAnim;
        private Vector2 _screenSize;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (_cutinImage == null) _cutinImage = GetComponent<Image>();
            if (_cutinRect == null) _cutinRect = GetComponent<RectTransform>();

            _screenSize = new Vector2(Screen.width, Screen.height);
            _cutinImage.enabled = false;
        }

        // 外部（CharacterHealth）から呼び出すメインメソッド
        public void PlayDeathCutin()
        {
            if (_cutinSprites == null || _cutinSprites.Count == 0) return;

            // 連打・連続撃破対策：再生中のアニメーションがあれば中断して即次を流す
            if (_currentAnim != null)
            {
                StopCoroutine(_currentAnim);
            }

            _currentAnim = StartCoroutine(CoPlayCutinEffect());
        }

        private IEnumerator CoPlayCutinEffect()
        {
            // 1. ランダムな画像を選択
            Sprite selectedSprite = _cutinSprites[Random.Range(0, _cutinSprites.Count)];
            _cutinImage.sprite = selectedSprite;
            _cutinImage.enabled = true;

            // 2. 左右と上下スライドのパターンをランダム決定
            bool isLeft = Random.value > 0.5f;          // 左側か右側か
            bool isFromTop = Random.value > 0.5f;       // 上から下か、下から上か

            // 画面端の目標位置と初期位置を設定
            // 画面サイズの 42% 〜 45%（0.45f）の位置に変更
            float targetX = isLeft ? -(_screenSize.x * 0.9f) : (_screenSize.x * 0.9f);
            float startY = isFromTop ? (_screenSize.y * 0.8f) : -(_screenSize.y * 0.8f);
            float targetY = isFromTop ? (_screenSize.y * 0.1f) : -(_screenSize.y * 0.1f);

            Vector2 startPos = new Vector2(targetX, startY);
            Vector2 targetPos = new Vector2(targetX, targetY);

            // 漫画らしい勢いを出すため、少し角度をつける（左なら右傾き、右なら左傾き）
            float tiltAngle = isLeft ? -12f : 12f;
            _cutinRect.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);

            // 3. インパクト（出現）アニメーション：勢いよくスライドインしながら縮小バウンド
            _cutinRect.anchoredPosition = startPos;
            _cutinRect.localScale = Vector3.one * 1.6f; // 最初は巨大化しておく

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * _animSpeed;
                _cutinRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                _cutinRect.localScale = Vector3.Lerp(Vector3.one * 1.6f, Vector3.one, t);
                yield return null;
            }

            // 4. 短くピタッと停止（キープ時間）
            yield return new WaitForSeconds(_displayDuration);

            // 5. 高速退場（さらにスライドしながら消える）
            Vector2 exitPos = targetPos + new Vector2(isLeft ? -300f : 300f, isFromTop ? -200f : 200f);
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (_animSpeed * 1.2f);
                _cutinRect.anchoredPosition = Vector2.Lerp(targetPos, exitPos, t);
                _cutinRect.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                yield return null;
            }

            _cutinImage.enabled = false;
            _currentAnim = null;
        }
    }
}
