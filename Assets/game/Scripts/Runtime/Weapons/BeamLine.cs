using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BeamLine : MonoBehaviour
    {
        public const string LogTag = "[Beam]";

        public const float CodeLineWidth = 0.04f;

        // 次の照射がフレームの揺れで少し遅れても線が途切れないよう、照射の間隔より長く残す。
        public const float HoldTicks = 1.5f;

        public static readonly Color CodeLineColor = new Color(1f, 0.2f, 0.15f);

        private static Material _codeLineMaterial;
        private static GameObject _unownedHost;

        private GameObject _visual;
        private LineRenderer _line;
        private AudioSource _sound;
        private string _weaponId;
        private bool _resolved;
        private float _hideAt;

        public bool IsVisible => _visual != null && _visual.activeSelf;

        public LineRenderer Line => _line;

        public GameObject Visual => _visual;

        public static BeamLine Show(GameObject shooter, string weaponId, float tickSeconds, Vector3 from, Vector3 to, float now)
        {
            // 撃った者の分からない照射 (仕掛けや試験) も線を出せるよう、持ち主の代わりの物に付ける。
            var host = shooter != null ? shooter : UnownedHost();
            if (!host.TryGetComponent<BeamLine>(out var line))
            {
                line = host.AddComponent<BeamLine>();
            }

            line.Draw(weaponId, tickSeconds, from, to, now);
            return line;
        }

        public void Draw(string weaponId, float tickSeconds, Vector3 from, Vector3 to, float now)
        {
            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (!_resolved || _visual == null || !string.Equals(weaponId, _weaponId, StringComparison.Ordinal))
            {
                Rebuild(weaponId);
            }

            // 線の端は世界の点で置くので transform は動かないが、3D 音は transform の位置で鳴るため、銃口に合わせて動かす。
            _visual.transform.position = from;

            if (_line != null)
            {
                _line.SetPosition(0, from);
                _line.SetPosition(1, to);
            }

            if (!_visual.activeSelf)
            {
                _visual.SetActive(true);
                if (_sound != null)
                {
                    _sound.Play();
                }
            }

            _hideAt = now + (tickSeconds * HoldTicks);
        }

        public void HideIfExpired(float now)
        {
            if (IsVisible && now >= _hideAt)
            {
                Hide();
            }
        }

        public void Hide()
        {
            if (_visual == null || !_visual.activeSelf)
            {
                return;
            }

            if (_sound != null)
            {
                _sound.Stop();
            }

            _visual.SetActive(false);
        }

        public static BeamSettings FindSettings(GameObject shooter, string weaponId)
        {
            var definition = ShooterWeapons.Find(shooter, weaponId);
            return definition != null ? definition.Beam : null;
        }

        private void Update()
        {
            HideIfExpired(Time.time);
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (_visual != null)
            {
                DestroyVisual(_visual);
            }
        }

        private void Rebuild(string weaponId)
        {
            if (_visual != null)
            {
                DestroyVisual(_visual);
            }

            _resolved = true;
            _weaponId = weaponId;
            var settings = FindSettings(gameObject == _unownedHost ? null : gameObject, weaponId);
            var prefab = settings != null ? settings.Visual : null;

            // 撃った者の子に置かないのは、体の見た目を切り替える処理 (一人称で体を隠すなど) に線まで巻き込まないため。
            _visual = prefab != null
                ? VisualPrefab.Instantiate(prefab, null, $"{name} Beam", stripColliders: true)
                : CreateCodeLine($"{name} Beam");
            _visual.SetActive(false);
            if (gameObject.scene.IsValid() && _visual.scene != gameObject.scene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_visual, gameObject.scene);
            }

            _line = _visual.GetComponentInChildren<LineRenderer>(true);
            if (_line == null)
            {
                Debug.LogWarning($"{LogTag} beam visual '{prefab.name}' of weapon '{weaponId}' has no LineRenderer; it is shown without following the hit", this);
            }
            else
            {
                // 端を照射のたびに世界の点で置き直すので、プレハブの設定に関わらず世界の座標で描く。
                _line.useWorldSpace = true;
                _line.positionCount = 2;
            }

            var clip = settings != null ? settings.Sound : null;
            _sound = null;
            if (clip != null)
            {
                _sound = _visual.AddComponent<AudioSource>();
                _sound.clip = clip;
                _sound.loop = true;
                _sound.playOnAwake = false;
                _sound.spatialBlend = 1f;
            }
        }

        private static GameObject CreateCodeLine(string objectName)
        {
            var visual = new GameObject(objectName);
            var line = visual.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = CodeLineWidth;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.startColor = CodeLineColor;
            line.endColor = CodeLineColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sharedMaterial = CodeLineMaterial();
            return visual;
        }

        // 頂点色で塗れて照明に左右されない組み込みのシェーダーを使う。どのパイプラインでも描け、ビルドにも常に入る。
        private static Material CodeLineMaterial()
        {
            if (_codeLineMaterial != null)
            {
                return _codeLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"{LogTag} the built-in shader 'Sprites/Default' is missing; the code-built beam line uses the default material");
                return null;
            }

            _codeLineMaterial = new Material(shader) { name = "BeamLine", hideFlags = HideFlags.DontSave };
            return _codeLineMaterial;
        }

        private static GameObject UnownedHost()
        {
            if (_unownedHost == null)
            {
                _unownedHost = new GameObject("Beam (no shooter)") { hideFlags = HideFlags.DontSave };
            }

            return _unownedHost;
        }

        // 試験は Play せずに線を作るので、Play 中でなければ Destroy が使えない。
        private static void DestroyVisual(Object visual)
        {
            if (Application.isPlaying)
            {
                Destroy(visual);
            }
            else
            {
                DestroyImmediate(visual);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _unownedHost = null;
        }
    }
}
