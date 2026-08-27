using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.HandTracking
{
    // MediaPipe HandLandmarkerのワールドランドマーク21点(ランドマーク番号の規約はMediaPipe公式のまま、
    // 0=手首、1-4=親指、5-8=人差し指、9-12=中指、13-16=薬指、17-20=小指、各指は付け根→指先の順)から、
    // 5指それぞれの伸展/屈曲を判定する純粋関数群。
    //
    // MediaPipe GestureRecognizerの学習済みモデル(gesture_recognizer.bytes)は、バンドル(.task)内部の
    // hand_landmarker.taskをネイティブ側が解決できずロードに失敗する既知の問題があるため
    // (google-ai-edge/mediapipe issue #5992等)、HandLandmarkerの生ランドマークから
    // こちらの自前判定でジェスチャーを組み立てる方式にしている。
    //
    // 判定方式(人差し指〜小指): 「指のまっすぐ度」で判定する。
    // 指を4点(MCP付け根→PIP→DIP→TIP指先)の関節を辿った経路長に対して、MCPからTIPまでの直線距離が
    // どれだけ近いかの比率(0〜1、まっすぐなほど1に近い)を見る。指を伸ばすとほぼ直線になり比率が1に近づき、
    // 握り込むと関節で折れ曲がって経路長に対し直線距離が短くなるため比率が下がる。
    // 手のひら中心のような外部の基準点を使わず指自身の関節だけで完結するため、
    // 手が(カメラに対して)どちらを向いていても崩れにくい(distance-to-palm-center方式より頑健)。
    //
    // 判定方式(親指): 親指はCMC(付け根)〜TIPがほぼ一直線のまま横に開閉する(意味のある「曲げ」が少ない)ため、
    // 上と同じ直線度では伸展/屈曲を判別しにくい。代わりに、指先が人差し指の付け根からどれだけ離れているかを見る
    // (握り込む=拳を握る動作では人差し指側に指先が寄り、伸ばす=横に開くと離れる)。
    //
    // ヒステリシス: 単一の閾値だけだと値がちょうど境界付近にあるときフレームごとに判定がチラつくため、
    // 「伸展→屈曲」と「屈曲→伸展」で別の閾値を使う(シュミットトリガー)。前フレームの状態をGetFingerStateへ
    // 渡すことで、一度伸展と判定した指はCurl側の閾値を下回るまで伸展のまま扱う(その逆も同様)。
    //
    // 閾値は人の手の一般的な曲げ伸ばし比率から見積もった初期値のため、実機のデバッグ表示(BattleGestureInputController
    // の_debugOverlay)を見ながらThresholdsをチューニングする前提。
    public static class HandPoseClassifier
    {
        [Serializable]
        public struct Thresholds
        {
            [Tooltip("人差し指〜小指を「伸展」と判定する直線度(MCP-TIP直線距離÷関節経路長)のしきい値")]
            public float FingerExtendStraightness;
            [Tooltip("人差し指〜小指を「屈曲」に戻すとみなす直線度のしきい値(チラつき防止のヒステリシス下限。" +
                "FingerExtendStraightnessより小さい値にすること)")]
            public float FingerCurlStraightness;
            [Tooltip("親指を「伸展」と判定する、指先/付け根の人差し指付け根からの距離比")]
            public float ThumbExtendRatio;
            [Tooltip("親指を「屈曲」に戻すとみなす距離比(ヒステリシス下限。ThumbExtendRatioより小さい値にすること)")]
            public float ThumbCurlRatio;

            public static Thresholds Default => new Thresholds
            {
                FingerExtendStraightness = 0.88f,
                FingerCurlStraightness = 0.78f,
                ThumbExtendRatio = 1.25f,
                ThumbCurlRatio = 1.10f,
            };
        }

        // 5指それぞれの伸展(true)/屈曲(false)状態。呼び出し側がこれを見て好きな形に組み合わせて判定する。
        public struct FingerState
        {
            public bool Thumb;
            public bool Index;
            public bool Middle;
            public bool Ring;
            public bool Pinky;
        }

        // デバッグ表示用の生の指標値(0〜1付近、親指のみ距離比なので1超えの値になり得る)。
        // FingerStateだけだと閾値のどちら側かしか分からず現場でのチューニングができないため、
        // 生の値も併せて返す。
        public struct FingerMetrics
        {
            public float Thumb;
            public float Index;
            public float Middle;
            public float Ring;
            public float Pinky;
        }

        // lmは21点のワールドランドマーク(Vector3、メートル、手首基準の相対座標)。
        // previousは直前フレームのFingerState(ヒステリシス用、初回はdefaultでよい)。
        public static FingerState GetFingerState(IReadOnlyList<Vector3> lm, Thresholds thresholds, FingerState previous, out FingerMetrics metrics)
        {
            metrics = default;
            if (lm == null || lm.Count < 21) return default;

            metrics.Index = Straightness(lm[5], lm[6], lm[7], lm[8]);
            metrics.Middle = Straightness(lm[9], lm[10], lm[11], lm[12]);
            metrics.Ring = Straightness(lm[13], lm[14], lm[15], lm[16]);
            metrics.Pinky = Straightness(lm[17], lm[18], lm[19], lm[20]);
            metrics.Thumb = ThumbRatio(lm[1], lm[4], lm[5]);

            return new FingerState
            {
                Index = IsExtended(metrics.Index, previous.Index, thresholds.FingerExtendStraightness, thresholds.FingerCurlStraightness),
                Middle = IsExtended(metrics.Middle, previous.Middle, thresholds.FingerExtendStraightness, thresholds.FingerCurlStraightness),
                Ring = IsExtended(metrics.Ring, previous.Ring, thresholds.FingerExtendStraightness, thresholds.FingerCurlStraightness),
                Pinky = IsExtended(metrics.Pinky, previous.Pinky, thresholds.FingerExtendStraightness, thresholds.FingerCurlStraightness),
                Thumb = IsExtended(metrics.Thumb, previous.Thumb, thresholds.ThumbExtendRatio, thresholds.ThumbCurlRatio),
            };
        }

        // 指の「まっすぐ度」(0〜1)。MCP→TIPの直線距離を、関節を辿った経路長(MCP→PIP→DIP→TIP)で割った値。
        static float Straightness(Vector3 mcp, Vector3 pip, Vector3 dip, Vector3 tip)
        {
            var pathLength = Vector3.Distance(mcp, pip) + Vector3.Distance(pip, dip) + Vector3.Distance(dip, tip);
            if (pathLength < 1e-6f) return 0f;
            return Vector3.Distance(mcp, tip) / pathLength;
        }

        // 親指先/付け根(CMC)それぞれの、人差し指付け根(MCP)からの距離の比。
        static float ThumbRatio(Vector3 thumbCmc, Vector3 thumbTip, Vector3 indexMcp)
        {
            var baseDist = Vector3.Distance(thumbCmc, indexMcp);
            if (baseDist < 1e-6f) return 0f;
            return Vector3.Distance(thumbTip, indexMcp) / baseDist;
        }

        // ヒステリシス(シュミットトリガー)判定。前フレーム伸展していた指はcurlThresholdを下回るまで
        // 伸展のまま、屈曲していた指はextendThresholdを上回るまで屈曲のまま扱う。
        static bool IsExtended(float value, bool wasExtended, float extendThreshold, float curlThreshold) =>
            wasExtended ? value > curlThreshold : value > extendThreshold;
    }
}
