using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SprayFlame : MonoBehaviour
    {
        public const string LogTag = "[Spray]";

        public const float CodeFlameSpeed = 12f;

        public const float CodeFlameRate = 120f;

        // 次の噴射がフレームの揺れで少し遅れても火炎が途切れないよう、噴射の間隔より長く出し続ける。
        public const float HoldTicks = 1.5f;

        public static readonly Color CodeFlameNear = new Color(1f, 0.85f, 0.3f);

        public static readonly Color CodeFlameFar = new Color(0.9f, 0.25f, 0.05f);

        private static Material _codeFlameMaterial;
        private static GameObject _unownedHost;

        private GameObject _visual;
        private ParticleSystem[] _systems = Array.Empty<ParticleSystem>();
        private bool _codeBuilt;
        private AudioSource _sound;
        private string _weaponId;
        private bool _resolved;
        private bool _emitting;
        private float _stopAt;

        public bool IsEmitting => _visual != null && _emitting;

        public GameObject Visual => _visual;

        public ParticleSystem CodeParticles => _codeBuilt && _systems.Length > 0 ? _systems[0] : null;

        public static SprayFlame Show(
            GameObject shooter, string weaponId, float tickSeconds, Vector3 from, Vector3 direction, float length, float coneDegrees, float now)
        {
            // 撃った者の分からない噴射 (仕掛けや試験) も火炎を出せるよう、持ち主の代わりの物に付ける。
            var host = shooter != null ? shooter : UnownedHost();
            if (!host.TryGetComponent<SprayFlame>(out var flame))
            {
                flame = host.AddComponent<SprayFlame>();
            }

            flame.Draw(weaponId, tickSeconds, from, direction, length, coneDegrees, now);
            return flame;
        }

        public void Draw(string weaponId, float tickSeconds, Vector3 from, Vector3 direction, float length, float coneDegrees, float now)
        {
            if (!(tickSeconds > 0f) || float.IsInfinity(tickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "tickSeconds must be positive and finite");
            }

            if (direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            if (!(length >= 0f) || float.IsInfinity(length))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "length must be a non-negative finite number");
            }

            if (!_resolved || _visual == null || !string.Equals(weaponId, _weaponId, StringComparison.Ordinal))
            {
                Rebuild(weaponId);
            }

            // 撃った者の子に置かないので、噴射のたびに銃口へ合わせる。
            _visual.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            if (_codeBuilt)
            {
                FitCodeFlame(_systems[0], length, coneDegrees);
            }

            if (!_emitting)
            {
                _emitting = true;
                foreach (var system in _systems)
                {
                    system.Play(false);
                }

                if (_sound != null)
                {
                    _sound.Play();
                }
            }

            _stopAt = now + (tickSeconds * HoldTicks);
        }

        public void StopIfExpired(float now)
        {
            if (IsEmitting && now >= _stopAt)
            {
                Stop();
            }
        }

        // 出た粒を消さずに止めるのは、離した瞬間に火炎が消えて見えないようにするため。
        public void Stop()
        {
            if (!IsEmitting)
            {
                return;
            }

            _emitting = false;
            foreach (var system in _systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            if (_sound != null)
            {
                _sound.Stop();
            }
        }

        public static SpraySettings FindSettings(GameObject shooter, string weaponId)
        {
            var definition = ShooterWeapons.Find(shooter, weaponId);
            return definition != null ? definition.Spray : null;
        }

        private void Update()
        {
            StopIfExpired(Time.time);
        }

        private void OnDisable()
        {
            Stop();
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
            _emitting = false;
            var settings = FindSettings(gameObject == _unownedHost ? null : gameObject, weaponId);
            var prefab = settings != null ? settings.Visual : null;

            // 撃った者の子に置かないのは、体の見た目を切り替える処理 (一人称で体を隠すなど) に火炎まで巻き込まないため。
            _codeBuilt = prefab == null;
            _visual = _codeBuilt ? CreateCodeFlame($"{name} Flame") : VisualPrefab.Instantiate(prefab, null, $"{name} Flame", stripColliders: true);
            if (gameObject.scene.IsValid() && _visual.scene != gameObject.scene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_visual, gameObject.scene);
            }

            _systems = _visual.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in _systems)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_systems.Length == 0)
            {
                Debug.LogWarning($"{LogTag} spray visual '{prefab.name}' of weapon '{weaponId}' has no ParticleSystem; it is shown without emitting", this);
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

        // 粒の寿命で火炎の長さを決めるのは、壁で止まった噴射の火炎が壁を突き抜けて見えないようにするため。
        private static void FitCodeFlame(ParticleSystem system, float length, float coneDegrees)
        {
            var main = system.main;
            main.startLifetime = Mathf.Max(0.01f, length / CodeFlameSpeed);
            var shape = system.shape;
            shape.angle = Mathf.Clamp(coneDegrees, 0f, 90f);
        }

        private static GameObject CreateCodeFlame(string objectName)
        {
            var visual = new GameObject(objectName);
            var system = visual.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = CodeFlameSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(CodeFlameNear, CodeFlameFar);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;

            var emission = system.emission;
            emission.rateOverTime = CodeFlameRate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.05f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(CodeFlameNear, 0f), new GradientColorKey(CodeFlameFar, 0.6f), new GradientColorKey(Color.black, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = visual.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = CodeFlameMaterial();
            return visual;
        }

        // 頂点色で塗れて照明に左右されない組み込みのシェーダーを使う。どのパイプラインでも描け、ビルドにも常に入る。
        private static Material CodeFlameMaterial()
        {
            if (_codeFlameMaterial != null)
            {
                return _codeFlameMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"{LogTag} the built-in shader 'Sprites/Default' is missing; the code-built flame uses the default material");
                return null;
            }

            _codeFlameMaterial = new Material(shader) { name = "SprayFlame", hideFlags = HideFlags.DontSave };
            return _codeFlameMaterial;
        }

        private static GameObject UnownedHost()
        {
            if (_unownedHost == null)
            {
                _unownedHost = new GameObject("Spray (no shooter)") { hideFlags = HideFlags.DontSave };
            }

            return _unownedHost;
        }

        // 試験は Play せずに火炎を作るので、Play 中でなければ Destroy が使えない。
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
