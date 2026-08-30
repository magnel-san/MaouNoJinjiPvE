using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    // ボスの被弾/雑魚の撃破で落ちるコイン。プロシージャル生成(円柱プリミティブ+発光マテリアル)で
    // 外部アセットに依存しない、このプロジェクトの他エフェクトと同じ方針。
    // 出現時は地面まで落下してから待機し、プレイヤーキャラが近づくとその場でスコアを加算し、
    // カメラへ吸い込まれるように飛んでいって消える。
    public class CoinPickup : MonoBehaviour
    {
        const float PickupRadius = 1.8f;
        const float SpinSpeedDegPerSec = 220f;
        const float BobAmplitude = 0.12f;
        const float BobFrequency = 2f;
        const float FlyToCameraSeconds = 0.4f;
        const float FallSpeed = 9f;
        // 地面のコライダー形状に依存すると(見つからない/高い所に当たる等で)コインが上空に
        // 留まってしまうことがあったため、固定の高さまで落ちるようにする。
        const float GroundY = 1f;
        // 出現してからこの秒数は拾えないようにする(即座に消えると「落ちた」感が無いため)。
        const float PickupDelaySeconds = 1f;
        // カメラのビューポート座標(右下寄り)。コインをここへ吸い込ませることで、
        // 画面手前へまっすぐ飛んでくるのではなく「スコア表示へ回収されている」ように見せる。
        static readonly Vector3 FlyTargetViewport = new Vector3(0.92f, 0.08f, 1.5f);

        static readonly Color CoinColor = new Color(1f, 0.85f, 0.15f);
        static readonly List<CoinPickup> _active = new List<CoinPickup>();

        float groundY;
        bool grounded;
        bool collected;
        float collectedElapsed;
        float pickupDelayRemaining;
        Vector3 flyStartPos;

        public static void Spawn(Vector3 position)
        {
            var go = new GameObject("CoinPickup");
            go.transform.position = position + Vector3.up * 0.6f;
            go.AddComponent<CoinPickup>().Initialize();
        }

        // 現在フィールドに存在する全てのコインを即座に回収する(最終決戦の撃破演出用)。
        public static void CollectAll()
        {
            foreach (var coin in _active.ToArray())
            {
                if (coin != null && !coin.collected) coin.BeginCollect();
            }
        }

        void OnEnable() => _active.Add(this);
        void OnDisable() => _active.Remove(this);

        void Initialize()
        {
            groundY = GroundY;
            pickupDelayRemaining = PickupDelaySeconds;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "CoinVisual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            var mat = new Material(VfxShaderUtil.GetUnlitShader()) { color = CoinColor };
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", CoinColor * 1.5f);
            }
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void Update()
        {
            if (collected)
            {
                UpdateFlyToCamera();
                return;
            }

            if (pickupDelayRemaining > 0f) pickupDelayRemaining -= Time.deltaTime;

            if (!grounded)
            {
                var pos = transform.position;
                pos.y = Mathf.MoveTowards(pos.y, groundY, FallSpeed * Time.deltaTime);
                transform.position = pos;
                if (Mathf.Approximately(pos.y, groundY)) grounded = true;
            }
            else
            {
                transform.Rotate(Vector3.up, SpinSpeedDegPerSec * Time.deltaTime, Space.World);
                var pos = transform.position;
                pos.y = groundY + Mathf.Sin(Time.time * BobFrequency) * BobAmplitude;
                transform.position = pos;
            }

            if (pickupDelayRemaining <= 0f && TryFindNearbyPlayer()) BeginCollect();
        }

        bool TryFindNearbyPlayer()
        {
            foreach (var c in CharacterRegistry.All)
            {
                if (c == null || c.Team != Team.Player || !c.IsAlive) continue;

                // 高さは無視し、水平距離だけで判定する(コインの高さとキャラの基準点の高さが
                // 一致するとは限らないため、3D距離だと遠く感じて拾えないことがあった)。
                var diff = c.transform.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= PickupRadius * PickupRadius) return true;
            }
            return false;
        }

        void BeginCollect()
        {
            collected = true;
            collectedElapsed = 0f;
            flyStartPos = transform.position;
            ScoreManager.AddCoinScore(1);
        }

        void UpdateFlyToCamera()
        {
            var cam = Camera.main;
            if (cam == null) { Destroy(gameObject); return; }

            collectedElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(collectedElapsed / FlyToCameraSeconds);
            var eased = t * t; // 加速しながら吸い込まれる

            // カメラの正面(位置)へまっすぐ向かうと画面手前に迫ってくるだけで「回収された」ように
            // 見えないため、毎フレームのカメラのビューポート右下の1点(FlyTargetViewport)を
            // ワールド座標に変換した位置を目標にする(カメラが動いても追従する)。
            var target = cam.ViewportToWorldPoint(FlyTargetViewport);
            transform.position = Vector3.Lerp(flyStartPos, target, eased);
            transform.localScale = Vector3.one * (1f - eased * 0.7f);

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
