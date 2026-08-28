using UnityEngine;

namespace Game
{
    // グー(防御)コマンド中、キャラの周りに盾を3つ公転させる見た目だけの演出。
    // OrbitingSword.csと同じ「公転オブジェクト」の構造を流用するが、こちらは敵に接触しても
    // ダメージは与えない(見た目のみ)。実際のダメージ軽減はCharacterHealth.ApplyDamageが
    // BattleCommandState.GuardActiveを見て一括で行うため、このコンポーネントは表示のオン/オフだけを担当する。
    [RequireComponent(typeof(CharacterIdentity))]
    public class CharacterGuard : MonoBehaviour
    {
        [SerializeField] private int _shieldCount = 3;
        [SerializeField] private float _orbitRadius = 1.1f;
        [SerializeField] private float _orbitHeight = 1f;
        [SerializeField] private float _orbitSpeed = 220f;
        [SerializeField] private float _shieldSize = 0.4f;
        static readonly Color ShieldColor = new Color(0.4f, 0.75f, 1f, 0.85f);

        CharacterIdentity identity;
        Transform[] shields;
        float currentAngleDeg;

        void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            currentAngleDeg = Random.Range(0f, 360f);
            BuildShields();
        }

        void BuildShields()
        {
            var count = Mathf.Max(1, _shieldCount);
            shields = new Transform[count];

            for (var i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "GuardShield";
                Destroy(go.GetComponent<Collider>());

                go.transform.localScale = Vector3.one * _shieldSize;

                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(VfxShaderUtil.GetTransparentShader()) { color = ShieldColor };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                go.SetActive(false);
                shields[i] = go.transform;
            }
        }

        void Update()
        {
            var shouldShow = identity.Team == Team.Player && identity.IsAlive && BattleCommandState.GuardActive;
            for (var i = 0; i < shields.Length; i++)
            {
                if (shields[i].gameObject.activeSelf != shouldShow) shields[i].gameObject.SetActive(shouldShow);
            }
            if (!shouldShow) return;

            currentAngleDeg += _orbitSpeed * Time.deltaTime;
            var spacing = 360f / shields.Length;
            for (var i = 0; i < shields.Length; i++)
            {
                var rad = (currentAngleDeg + spacing * i) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _orbitRadius;
                shields[i].position = transform.position + Vector3.up * _orbitHeight + offset;
            }
        }

        void OnDestroy()
        {
            if (shields == null) return;
            foreach (var s in shields)
            {
                if (s != null) Destroy(s.gameObject);
            }
        }
    }
}
