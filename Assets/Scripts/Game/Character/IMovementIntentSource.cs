using UnityEngine;

namespace Game
{
    public struct MovementIntent
    {
        public Vector3 DesiredDirection; // ワールド空間・XZ平面の正規化方向 (停止時はゼロで可)
        public Vector3? FaceOverride;    // 向きたい方向を移動方向と別に指定したい場合
        public bool Move;                // 前進力を実際に加えるかどうか
        public float SpeedMultiplier;    // 基本移動力に掛ける倍率 (0以下 = 未指定として1倍扱い)
    }

    // 各アビリティ(移動系ロジック)がこれを実装し、CharacterMovementが最も優先度の高い提案を採用する。
    // 場外回避(BoundaryAvoidance)が最優先(絶対優先)となるよう高い値を返す。
    public interface IMovementIntentSource
    {
        int MovementPriority { get; }
        bool TryGetMovementIntent(out MovementIntent intent);
    }
}
