using UnityEngine;

namespace Game
{
    // ボスの攻撃が味方に命中した瞬間、ごく短時間だけTime.timeScaleを0にして「ズシッ」とした
    // 手応えを出す。CameraShake.csと同じ「呼ばれるたびに自己生成のシングルトンを介して駆動する」
    // パターン。timeScaleが0の間もカウントダウン自体は進める必要があるため、
    // Time.deltaTimeではなくTime.unscaledDeltaTimeで計測する。
    public class HitStop : MonoBehaviour
    {
        static HitStop _instance;
        float _remaining;

        public static void Trigger(float seconds)
        {
            if (seconds <= 0f) return;

            if (_instance == null)
            {
                var go = new GameObject("HitStop");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitStop>();
            }

            // 既に停止中なら、より長い方を採用する(短い方で上書きして早く戻ってしまわないように)。
            _instance._remaining = Mathf.Max(_instance._remaining, seconds);
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (_remaining <= 0f) return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Time.timeScale = 1f;
            }
        }
    }
}
