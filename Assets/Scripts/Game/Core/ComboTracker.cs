using System;
using UnityEngine;

namespace Game
{
    // ボスへの攻撃が1秒以内に継続している間、コンボ数を数える。CameraShake/CommandAnnouncerと
    // 同じ「呼ばれた時に自己生成するシングルトン」パターン。RegisterHit()は攻撃の発生源
    // (BossControllerがCharacterHealth.OnHPChanged経由で呼ぶ)からdeltaTimeに関係なく
    // 好きなタイミングで呼べる。
    public class ComboTracker : MonoBehaviour
    {
        const float ComboWindowSeconds = 1f;
        const int ComboScorePerHit = 5;

        static ComboTracker _instance;

        public static int Combo { get; private set; }
        public static event Action<int> OnComboChanged;

        float windowRemaining;

        public static void RegisterHit()
        {
            EnsureExists();
            _instance.windowRemaining = ComboWindowSeconds;
            Combo++;
            OnComboChanged?.Invoke(Combo);
            ScoreManager.AddComboScore(ComboScorePerHit);
        }

        static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("ComboTracker");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ComboTracker>();
        }

        void Update()
        {
            if (Combo <= 0) return;

            windowRemaining -= Time.deltaTime;
            if (windowRemaining <= 0f)
            {
                Combo = 0;
                OnComboChanged?.Invoke(Combo);
            }
        }
    }
}
