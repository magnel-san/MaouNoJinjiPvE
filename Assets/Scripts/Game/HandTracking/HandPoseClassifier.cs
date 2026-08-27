using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.HandTracking
{
    // MediaPipe HandLandmarkerのワールドランドマーク21点(ランドマーク番号の規約はMediaPipe公式のまま、
    // 0=手首、1-4=親指、5-8=人差し指、9-12=中指、13-16=薬指、17-20=小指、各指は付け根→指先の順)から、
    // 静的な手の形(HandPose)を判定する純粋関数群。距離の「比」だけで判定するため、
    // ワールドランドマークがどの座標系(HandTrackingController.ConvertToUnityVector適用前/後)でも
    // 結果は変わらない(スケール不変)。カメラとの距離にも依存しない。
    //
    // 判定方式: 各指について、指先が「手のひら中心」からどれだけ離れているかを、
    // その指の付け根(MCP関節)が手のひら中心からどれだけ離れているかと比較する。
    // 指を伸ばすと指先が付け根よりずっと遠くなり、指を握ると指先が付け根付近まで戻ってくるため、
    // 比が閾値を超えるかどうかで伸展/屈曲を判定できる。親指だけは動き方が横方向的で同じ比較が効きにくいため、
    // 小指付け根からの距離で判定する(親指を畳む=拳を握る動作では小指側に寄り、伸ばす=横に開くと離れる)。
    //
    // 閾値は実機無しでの初期値のため、実際にカメラで試しながらThresholdsをチューニングする前提。
    public static class HandPoseClassifier
    {
        [Serializable]
        public struct Thresholds
        {
            [Tooltip("指(人差し指〜小指)を「伸展」とみなす、指先/付け根の手のひら中心からの距離比")]
            public float FingerExtendRatio;
            [Tooltip("親指を「伸展」とみなす、指先/付け根の小指付け根からの距離比")]
            public float ThumbExtendRatio;

            public static Thresholds Default => new Thresholds { FingerExtendRatio = 1.2f, ThumbExtendRatio = 1.15f };
        }

        // 単純な閾値判定(履歴なし)。しきい値付近でのチラつきは、呼び出し側(BattleGestureInputController)の
        // 保持時間(dwell)確認で吸収する前提。実機調整後もチラつきが気になる場合は、指ごとに
        // 「伸展/屈曲」の直前状態を持たせたヒステリシス判定への拡張を検討する。
        // lmは21点のワールドランドマーク(Vector3、メートル、手首基準の相対座標)。
        public static HandPose Classify(IReadOnlyList<Vector3> lm, Thresholds thresholds)
        {
            if (lm == null || lm.Count < 21) return HandPose.None;

            var palmCenter = PalmCenter(lm);

            var index = IsFingerExtended(lm[5], lm[8], palmCenter, thresholds.FingerExtendRatio);
            var middle = IsFingerExtended(lm[9], lm[12], palmCenter, thresholds.FingerExtendRatio);
            var ring = IsFingerExtended(lm[13], lm[16], palmCenter, thresholds.FingerExtendRatio);
            var pinky = IsFingerExtended(lm[17], lm[20], palmCenter, thresholds.FingerExtendRatio);
            var thumb = IsThumbExtended(lm[1], lm[4], lm[5], thresholds.ThumbExtendRatio);

            // 親指の伸展/屈曲も判定条件に含めることで4つのPoseを互いに排他にする
            // (例: 「人差し指のみ」と「親指+人差し指」を確実に区別する)。
            if (!thumb && index && !middle && !ring && !pinky) return HandPose.IndexOnly;
            if (thumb && index && middle && ring && pinky) return HandPose.OpenPalm;
            if (thumb && index && !middle && !ring && !pinky) return HandPose.ThumbIndex;
            if (thumb && index && middle && !ring && !pinky) return HandPose.ThumbIndexMiddle;

            return HandPose.None;
        }

        // 手首+4指の付け根(MCP関節)の平均。指を握っても崩れにくい、手のひらの中心に相当する基準点。
        // カーソル位置(手の中心)にもこれをそのまま使う。
        public static Vector3 PalmCenter(IReadOnlyList<Vector3> lm) =>
            (lm[0] + lm[5] + lm[9] + lm[13] + lm[17]) / 5f;

        static bool IsFingerExtended(Vector3 mcp, Vector3 tip, Vector3 palmCenter, float ratio)
        {
            var mcpDist = Vector3.Distance(mcp, palmCenter);
            var tipDist = Vector3.Distance(tip, palmCenter);
            return tipDist > mcpDist * ratio;
        }

        // 親指を握り込む(拳を握る)と指先は人差し指の付け根付近まで寄り、伸ばす(横に開く)と離れる。
        // 手のひら中心基準だと親指の動きは横方向的で判定が効きにくいため、人差し指の付け根を基準にする。
        static bool IsThumbExtended(Vector3 thumbCmc, Vector3 thumbTip, Vector3 indexMcp, float ratio)
        {
            var baseDist = Vector3.Distance(thumbCmc, indexMcp);
            var tipDist = Vector3.Distance(thumbTip, indexMcp);
            return tipDist > baseDist * ratio;
        }
    }
}
