using UnityEngine;

namespace Game
{
    // 場外近くの危険地帯に入ったら、離れる方向への補正をCharacterMovementに提供する。
    // 以前は移動を完全に上書きしていたが、それだと「敵から逃げたい方向」と「境界から離れたい方向」が
    // 正反対のときに毎フレーム勝者が入れ替わり、境界付近で往復して動けなくなる問題があった。
    // 現在は緊急度(0〜1)を返し、CharacterMovement側で行動方向とベクトル合成することで、
    // 壁沿いに滑るように移動しながら安全な内側へ戻れるようにしている。
    public class BoundaryAvoidance : MonoBehaviour
    {
        MapBounds mapBounds;

        void Start() => mapBounds = FindAnyObjectByType<MapBounds>();

        public bool TryGetAvoidance(out Vector3 awayDir, out float urgency)
        {
            awayDir = Vector3.zero;
            urgency = 0f;
            if (mapBounds == null) return false;

            Vector3 pos = transform.position;
            float margin = mapBounds.DangerMargin;
            if (margin <= 0f) return false;

            float distToMinX = pos.x - mapBounds.MinCorner.x;
            float distToMaxX = mapBounds.MaxCorner.x - pos.x;
            float distToMinZ = pos.z - mapBounds.MinCorner.y;
            float distToMaxZ = mapBounds.MaxCorner.y - pos.z;

            float minDist = Mathf.Min(Mathf.Min(distToMinX, distToMaxX), Mathf.Min(distToMinZ, distToMaxZ));
            if (minDist > margin) return false;

            Vector3 pushDir = Vector3.zero;
            if (distToMinX <= margin) pushDir += Vector3.right * (margin - distToMinX);
            if (distToMaxX <= margin) pushDir += Vector3.left * (margin - distToMaxX);
            if (distToMinZ <= margin) pushDir += Vector3.forward * (margin - distToMinZ);
            if (distToMaxZ <= margin) pushDir += Vector3.back * (margin - distToMaxZ);

            if (pushDir.sqrMagnitude < 0.0001f) return false;

            awayDir = pushDir.normalized;
            urgency = Mathf.Clamp01((margin - minDist) / margin);
            return true;
        }
    }
}
