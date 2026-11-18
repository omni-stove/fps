using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WeatherController : MonoBehaviour
    {
        public const string LogTag = WeatherLog.LogTag;

        public const string SunName = "WeatherSun";

        public const string RainName = "WeatherRain";

        public const float DefaultLightYawDegrees = 30f;

        public const float DefaultMaxRainPerSecond = 4000f;

        public const float DefaultRainFallSpeed = 20f;

        public const float DefaultRainAreaSize = 40f;

        public const float DefaultRainHeight = 15f;

        public const float DefaultMaxWindTiltDegrees = 25f;

        public const float DefaultRainVolume = 0.6f;

        [Header("ルール")]
        [SerializeField, Tooltip("オフラインで天候と時刻の Id を読む対戦ルール。オンラインではサーバーから届いたルールの Id で選び直す")]
        private NetMatchDefinition _rules;

        [SerializeField, Tooltip("ルールが天候を選ばないときの、このマップの天候の Id。空なら天候の一覧の既定")]
        private string _mapWeatherId = string.Empty;

        [SerializeField, Tooltip("ルールが時刻を選ばないときの、このマップの時刻の Id。空なら選んだ天候の組の時刻")]
        private string _mapTimeOfDayId = string.Empty;

        [SerializeField, Tooltip("天候と時刻の組の JSON。空なら入口 (GameContent) のもの、それも無ければ Resources/Tuning/weather")]
        private TextAsset _weatherTuning;

        [Header("光")]
        [SerializeField, Tooltip("太陽 (夜は月) として向きと色を変える平行光源。空なら RenderSettings.sun、それも無ければ作る")]
        private Light _sun;

        [SerializeField, Tooltip("光の水平の向き (度)")]
        private float _lightYawDegrees = DefaultLightYawDegrees;

        [SerializeField, Tooltip("光の色。左端が 0 時、右端が 24 時")]
        private Gradient _lightColor = DefaultLightColor();

        [SerializeField, Tooltip("環境光の色。左端が 0 時、右端が 24 時")]
        private Gradient _ambientColor = DefaultAmbientColor();

        [SerializeField, Tooltip("霧の色。左端が 0 時、右端が 24 時")]
        private Gradient _fogColor = DefaultFogColor();

        [Header("雨")]
        [SerializeField, Tooltip("雨の ParticleSystem を持つプレハブ。カメラに付いて動く。空なら簡単な雨を作る")]
        private GameObject _rainPrefab;

        [SerializeField, Min(0f), Tooltip("作る雨の、雨の強さ 1 での 1 秒あたりの粒の数。プレハブを配線したらプレハブの数に強さを掛ける")]
        private float _maxRainPerSecond = DefaultMaxRainPerSecond;

        [SerializeField, Min(0f), Tooltip("風の強さ 1 で雨を傾ける角度 (度)")]
        private float _maxWindTiltDegrees = DefaultMaxWindTiltDegrees;

        [SerializeField, Tooltip("雨の間ずっと鳴らす音。空なら鳴らさない")]
        private AudioClip _rainLoop;

        [SerializeField, Range(0f, 1f), Tooltip("雨の強さ 1 での雨の音量")]
        private float _rainVolume = DefaultRainVolume;

        private WeatherSpec _spec;
        private WeatherChoice? _current;
        private string _requestedWeatherId;
        private string _requestedTimeOfDayId;
        private SceneLook _original;
        private bool _captured;
        private Light _builtSun;
        private GameObject _rain;
        private ParticleSystem[] _rainSystems;
        private float[] _rainBaseRates;
        private AudioSource _rainSound;

        public static WeatherSpec Tuning => KitTuning.Load(c => c.WeatherTuning, WeatherJson.ResourcePath, WeatherJson.Parse, WeatherSpec.Default);

        public WeatherSpec Spec => _spec ?? (_spec = _weatherTuning != null
            ? KitTuning.Read(_weatherTuning.text, _weatherTuning.name, WeatherJson.Parse, Tuning)
            : Tuning);

        public WeatherChoice? Current => _current;

        public NetMatchDefinition Rules
        {
            get => _rules;
            set => _rules = value;
        }

        public string MapWeatherId
        {
            get => _mapWeatherId;
            set => _mapWeatherId = value ?? string.Empty;
        }

        public string MapTimeOfDayId
        {
            get => _mapTimeOfDayId;
            set => _mapTimeOfDayId = value ?? string.Empty;
        }

        public Light Sun
        {
            get => _sun;
            set => _sun = value;
        }

        public GameObject RainPrefab
        {
            get => _rainPrefab;
            set => _rainPrefab = value;
        }

        public AudioClip RainLoop
        {
            get => _rainLoop;
            set => _rainLoop = value;
        }

        public Light AppliedLight => _sun != null ? _sun : _builtSun != null ? _builtSun : RenderSettings.sun;

        public GameObject RainObject => _rain;

        public AudioSource RainSound => _rainSound;

        public void ApplySpec(WeatherSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (_current.HasValue)
            {
                _current = null;
                Select(_requestedWeatherId, _requestedTimeOfDayId);
            }
        }

        public WeatherChoice SelectFromRules()
        {
            if (_current.HasValue)
            {
                return _current.Value;
            }

            return Select(_rules != null ? _rules.WeatherId : null, _rules != null ? _rules.TimeOfDayId : null);
        }

        public WeatherChoice Select(string weatherId, string timeOfDayId)
        {
            var choice = WeatherChoice.Choose(Spec, weatherId, timeOfDayId, _mapWeatherId, _mapTimeOfDayId);
            _requestedWeatherId = weatherId;
            _requestedTimeOfDayId = timeOfDayId;
            if (_current.HasValue && _current.Value.Equals(choice))
            {
                return choice;
            }

            WarnIfUnknown(NetMatchFile.WeatherIdItem, weatherId, choice.WeatherFromRules, choice.Weather.Id);
            WarnIfUnknown(NetMatchFile.TimeOfDayIdItem, timeOfDayId, choice.TimeOfDayFromRules, choice.TimeOfDay.Id);
            _current = choice;
            Show(choice);
            Debug.Log(WeatherLog.Applied(choice.WeatherFromRules || choice.TimeOfDayFromRules ? WeatherLog.RulesSource : WeatherLog.MapSource, choice, weatherId, timeOfDayId), this);
            return choice;
        }

        public static float DayFraction(float hours)
        {
            return Mathf.Repeat(hours / WeatherPreset.HoursPerDay, 1f);
        }

        // 次のシーンや同じシーンでの次の試合に、この試合の空を残さない。
        public void RestoreScene()
        {
            if (_rain != null)
            {
                _rain.SetActive(false);
            }

            if (_captured)
            {
                _original.Restore();
                _captured = false;
            }

            _current = null;
        }

        private void Start()
        {
            SelectFromRules();
        }

        // 雨は広い範囲に降らせる代わりに、見ている所の周りにだけ降らせる。
        private void LateUpdate()
        {
            if (_rain == null || !_rain.activeSelf)
            {
                return;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                _rain.transform.position = camera.transform.position;
            }
        }

        private void OnDestroy()
        {
            RestoreScene();
        }

        private void WarnIfUnknown(string item, string id, bool found, string fallback)
        {
            if (!found && !WeatherChoice.IsUnset(id))
            {
                Debug.LogWarning(WeatherLog.UnknownId(item, id.Trim(), fallback), this);
            }
        }

        private void Show(WeatherChoice choice)
        {
            var light = LightToUse();
            if (!_captured)
            {
                _original = SceneLook.Capture(light == _builtSun ? null : light);
                _captured = true;
            }

            var day = DayFraction(choice.TimeOfDayHours);
            light.transform.rotation = Quaternion.Euler(choice.LightPitchDegrees, _lightYawDegrees, 0f);
            light.color = _lightColor.Evaluate(day);
            light.intensity = choice.SunIntensity;
            RenderSettings.sun = light;

            // 空の映り込みのままだと夜でも昼の明るさが残るので、時刻の色で一様に照らす。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientColor.Evaluate(day);

            RenderSettings.fog = choice.FogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = choice.FogDensity;
            RenderSettings.fogColor = _fogColor.Evaluate(day);

            ShowRain(choice);
        }

        private Light LightToUse()
        {
            if (_sun != null)
            {
                return _sun;
            }

            if (_builtSun != null)
            {
                return _builtSun;
            }

            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            {
                _sun = RenderSettings.sun;
                return _sun;
            }

            var sun = new GameObject(SunName);
            sun.transform.SetParent(transform, false);
            _builtSun = sun.AddComponent<Light>();
            _builtSun.type = LightType.Directional;
            _builtSun.shadows = LightShadows.Soft;
            return _builtSun;
        }

        private void ShowRain(WeatherChoice choice)
        {
            var intensity = choice.RainIntensity;
            if (intensity <= 0f)
            {
                if (_rain != null)
                {
                    _rain.SetActive(false);
                }

                return;
            }

            if (_rain == null)
            {
                BuildRain();
            }

            _rain.SetActive(true);
            _rain.transform.rotation = Quaternion.Euler(0f, _lightYawDegrees, choice.WindStrength * _maxWindTiltDegrees);
            for (var i = 0; i < _rainSystems.Length; i++)
            {
                var emission = _rainSystems[i].emission;
                emission.rateOverTimeMultiplier = _rainBaseRates[i] * intensity;
                if (!_rainSystems[i].isPlaying)
                {
                    _rainSystems[i].Play();
                }
            }

            if (_rainSound != null)
            {
                _rainSound.volume = _rainVolume * intensity;
                if (!_rainSound.isPlaying)
                {
                    _rainSound.Play();
                }
            }
        }

        private void BuildRain()
        {
            _rain = _rainPrefab != null ? Instantiate(_rainPrefab, transform, false) : BuildDefaultRain();
            _rain.name = RainName;
            _rainSystems = _rain.GetComponentsInChildren<ParticleSystem>(true);
            _rainBaseRates = new float[_rainSystems.Length];
            for (var i = 0; i < _rainSystems.Length; i++)
            {
                _rainBaseRates[i] = _rainSystems[i].emission.rateOverTimeMultiplier;
            }

            if (_rainLoop != null)
            {
                _rainSound = _rain.AddComponent<AudioSource>();
                _rainSound.clip = _rainLoop;
                _rainSound.loop = true;
                _rainSound.playOnAwake = false;
                _rainSound.spatialBlend = 0f;
            }
        }

        private GameObject BuildDefaultRain()
        {
            var rain = new GameObject(RainName);
            rain.transform.SetParent(transform, false);
            var particles = rain.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var lifetime = DefaultRainHeight / DefaultRainFallSpeed * 1.2f;
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.8f, 0.85f, 0.5f);
            main.maxParticles = Mathf.CeilToInt(_maxRainPerSecond * lifetime) + 1;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = _maxRainPerSecond;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, DefaultRainHeight, 0f);
            shape.scale = new Vector3(DefaultRainAreaSize, 0.1f, DefaultRainAreaSize);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-DefaultRainFallSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = DefaultMaterial();
            return rain;
        }

        // プリミティブを作るのは、その描画パイプラインの既定の材質を得るため
        private static Material DefaultMaterial()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = probe.GetComponent<Renderer>().sharedMaterial;
            DestroyImmediate(probe);
            return material;
        }

        private static Gradient DefaultLightColor()
        {
            return DayGradient(
                new Color(0.55f, 0.65f, 0.9f),
                new Color(1f, 0.6f, 0.35f),
                new Color(1f, 0.97f, 0.9f),
                new Color(1f, 0.55f, 0.3f));
        }

        private static Gradient DefaultAmbientColor()
        {
            return DayGradient(
                new Color(0.05f, 0.06f, 0.1f),
                new Color(0.35f, 0.3f, 0.3f),
                new Color(0.5f, 0.5f, 0.52f),
                new Color(0.3f, 0.25f, 0.3f));
        }

        private static Gradient DefaultFogColor()
        {
            return DayGradient(
                new Color(0.04f, 0.05f, 0.08f),
                new Color(0.6f, 0.5f, 0.45f),
                new Color(0.7f, 0.72f, 0.75f),
                new Color(0.5f, 0.4f, 0.4f));
        }

        private static Gradient DayGradient(Color midnight, Color dawn, Color noon, Color dusk)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(midnight, 0f),
                    new GradientColorKey(dawn, 0.25f),
                    new GradientColorKey(noon, 0.5f),
                    new GradientColorKey(dusk, 0.75f),
                    new GradientColorKey(midnight, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private struct SceneLook
        {
            private Light _sun;
            private AmbientMode _ambientMode;
            private Color _ambientLight;
            private bool _fog;
            private FogMode _fogMode;
            private float _fogDensity;
            private Color _fogColor;
            private Light _light;
            private Quaternion _lightRotation;
            private Color _lightColor;
            private float _lightIntensity;

            // 作った光は自分の子で一緒に消えるので、戻すのはシーンにあった光だけでよい。
            public static SceneLook Capture(Light changed)
            {
                var sun = changed;
                return new SceneLook
                {
                    _sun = RenderSettings.sun,
                    _ambientMode = RenderSettings.ambientMode,
                    _ambientLight = RenderSettings.ambientLight,
                    _fog = RenderSettings.fog,
                    _fogMode = RenderSettings.fogMode,
                    _fogDensity = RenderSettings.fogDensity,
                    _fogColor = RenderSettings.fogColor,
                    _light = sun,
                    _lightRotation = sun != null ? sun.transform.rotation : Quaternion.identity,
                    _lightColor = sun != null ? sun.color : Color.white,
                    _lightIntensity = sun != null ? sun.intensity : 1f,
                };
            }

            public void Restore()
            {
                RenderSettings.sun = _sun;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientLight;
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.fogColor = _fogColor;
                if (_light != null)
                {
                    _light.transform.rotation = _lightRotation;
                    _light.color = _lightColor;
                    _light.intensity = _lightIntensity;
                }
            }
        }
    }
}
