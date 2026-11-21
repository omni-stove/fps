using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class WeatherControllerTests
    {
        private const float _angleTolerance = 0.01f;

        private readonly List<Object> _objects = new List<Object>();
        private WeatherController _controller;
        private Light _sun;
        private bool _fog;
        private float _fogDensity;
        private FogMode _fogMode;
        private Color _fogColor;
        private AmbientMode _ambientMode;
        private Color _ambientLight;
        private Light _renderSun;

        [SetUp]
        public void SetUp()
        {
            _fog = RenderSettings.fog;
            _fogDensity = RenderSettings.fogDensity;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            _renderSun = RenderSettings.sun;

            var sun = Track(new GameObject("WeatherControllerTests.Sun"));
            _sun = sun.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.intensity = 0.7f;
            _sun.color = Color.red;

            var go = Track(new GameObject("WeatherControllerTests.Weather"));
            _controller = go.AddComponent<WeatherController>();
            _controller.Sun = _sun;
            _controller.ApplySpec(WeatherSpec.Default);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
            {
                _controller.RestoreScene();
            }

            foreach (var o in _objects)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            _objects.Clear();
            RenderSettings.fog = _fog;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.sun = _renderSun;
        }

        [Test]
        public void OfflineTheRulesRainyNightIsShown()
        {
            _controller.Rules = Rules(WeatherSpec.StormNightId, WeatherSpec.StormNightId);

            var choice = _controller.SelectFromRules();

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_controller.Current, Is.EqualTo(choice));
            Assert.That(Quaternion.Angle(_sun.transform.rotation, Quaternion.Euler(75f, WeatherController.DefaultLightYawDegrees, 0f)), Is.LessThan(_angleTolerance),
                "23:00 is lit by the moon 75 degrees up");
            Assert.That(_sun.intensity, Is.EqualTo(0.05f));
            Assert.That(RenderSettings.sun, Is.SameAs(_sun));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.05f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(_controller.RainObject, Is.Not.Null);
            Assert.That(_controller.RainObject.activeSelf, Is.True);
            Assert.That(_controller.RainObject.name, Is.EqualTo(WeatherController.RainName));
            var rain = _controller.RainObject.GetComponent<ParticleSystem>();
            Assert.That(rain, Is.Not.Null, "with no prefab wired a rain is built");
            Assert.That(rain.emission.rateOverTimeMultiplier, Is.EqualTo(WeatherController.DefaultMaxRainPerSecond));
            Assert.That(
                Quaternion.Angle(_controller.RainObject.transform.rotation, Quaternion.Euler(0f, WeatherController.DefaultLightYawDegrees, WeatherController.DefaultMaxWindTiltDegrees)),
                Is.LessThan(_angleTolerance),
                "full wind tilts the rain fully");
        }

        [Test]
        public void WithoutRulesTheSpecDefaultClearNoonIsShown()
        {
            var choice = _controller.SelectFromRules();

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId));
            Assert.That(Quaternion.Angle(_sun.transform.rotation, Quaternion.Euler(90f, WeatherController.DefaultLightYawDegrees, 0f)), Is.LessThan(_angleTolerance));
            Assert.That(_sun.intensity, Is.EqualTo(1f));
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(_controller.RainObject, Is.Null, "no rain is built for a dry sky");
        }

        [Test]
        public void EmptyRuleIdsUseTheMapDefaults()
        {
            _controller.Rules = Rules(string.Empty, string.Empty);
            _controller.MapWeatherId = WeatherSpec.OvercastDuskId;
            _controller.MapTimeOfDayId = WeatherSpec.StormNightId;

            var choice = _controller.SelectFromRules();

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.02f));
            Assert.That(_sun.intensity, Is.EqualTo(0.05f));
        }

        [Test]
        public void AChoiceFromTheServerIsKeptOverTheOfflineRules()
        {
            _controller.Rules = Rules(WeatherSpec.ClearNoonId, WeatherSpec.ClearNoonId);
            _controller.Select(WeatherSpec.StormNightId, null);

            var choice = _controller.SelectFromRules();

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_controller.RainObject.activeSelf, Is.True);
        }

        [Test]
        public void ClearingTheRainHidesIt()
        {
            _controller.Select(WeatherSpec.StormNightId, null);
            _controller.Select(WeatherSpec.ClearNoonId, null);

            Assert.That(_controller.RainObject.activeSelf, Is.False);
            Assert.That(RenderSettings.fog, Is.False);
        }

        [Test]
        public void ARainPrefabIsUsedAndScaledByTheIntensity()
        {
            var prefab = Track(new GameObject("WeatherControllerTests.RainPrefab"));
            var particles = prefab.AddComponent<ParticleSystem>();
            var emission = particles.emission;
            emission.rateOverTime = 200f;
            _controller.RainPrefab = prefab;
            var rain = new WeatherPreset("drizzle", null, 15f, 0f, 0.25f, 0f, 0.5f);
            _controller.ApplySpec(new WeatherSpec(new[] { rain }));

            _controller.Select("drizzle", null);

            var instance = _controller.RainObject;
            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.transform.parent, Is.SameAs(_controller.transform));
            Assert.That(instance.GetComponent<ParticleSystem>().emission.rateOverTimeMultiplier, Is.EqualTo(50f));
        }

        [Test]
        public void TheRainLoopPlaysAtTheIntensityVolume()
        {
            var clip = AudioClip.Create("WeatherControllerTests.Rain", 441, 1, 44100, false);
            _objects.Add(clip);
            _controller.RainLoop = clip;

            _controller.Select(WeatherSpec.StormNightId, null);

            Assert.That(_controller.RainSound, Is.Not.Null);
            Assert.That(_controller.RainSound.clip, Is.SameAs(clip));
            Assert.That(_controller.RainSound.loop, Is.True);
            Assert.That(_controller.RainSound.volume, Is.EqualTo(WeatherController.DefaultRainVolume));
        }

        [Test]
        public void WithoutAWiredLightOneIsBuilt()
        {
            _controller.Sun = null;
            RenderSettings.sun = null;
            // RenderSettings.sun falls back to the brightest active directional light, so the open scene's lights are turned off here.
            var sceneLights = new List<GameObject>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type == LightType.Directional)
                {
                    sceneLights.Add(light.gameObject);
                    light.gameObject.SetActive(false);
                }
            }

            try
            {
                _controller.Select(WeatherSpec.OvercastDuskId, null);
            }
            finally
            {
                foreach (var sceneLight in sceneLights)
                {
                    sceneLight.SetActive(true);
                }
            }

            var built = _controller.AppliedLight;
            Assert.That(built, Is.Not.Null);
            Assert.That(built.name, Is.EqualTo(WeatherController.SunName));
            Assert.That(built.type, Is.EqualTo(LightType.Directional));
            Assert.That(built.transform.parent, Is.SameAs(_controller.transform));
            Assert.That(built.intensity, Is.EqualTo(0.4f));
        }

        [Test]
        public void AnUnknownRuleIdWarnsAndFallsBack()
        {
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(WeatherLog.UnknownId(NetMatchFile.WeatherIdItem, "sandstorm", WeatherSpec.ClearNoonId))));

            var choice = _controller.Select("sandstorm", null);

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId));
        }

        [Test]
        public void SelectingTheSameSkyAgainKeepsTheRainThatIsFalling()
        {
            var first = _controller.Select(WeatherSpec.StormNightId, WeatherSpec.StormNightId);
            var rain = _controller.RainObject;

            var again = _controller.Select(WeatherSpec.StormNightId, WeatherSpec.StormNightId);

            Assert.That(again, Is.EqualTo(first));
            Assert.That(_controller.RainObject, Is.SameAs(rain));
            Assert.That(_controller.RainObject.activeSelf, Is.True);
        }

        [Test]
        public void RestoringPutsTheSceneLookBack()
        {
            RenderSettings.fog = false;
            _controller.Select(WeatherSpec.StormNightId, null);

            _controller.RestoreScene();

            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(_sun.intensity, Is.EqualTo(0.7f));
            Assert.That(_sun.color, Is.EqualTo(Color.red));
            Assert.That(_controller.Current.HasValue, Is.False);
            Assert.That(_controller.RainObject.activeSelf, Is.False);
        }

        [TestCase(0f, 0f)]
        [TestCase(6f, 0.25f)]
        [TestCase(12f, 0.5f)]
        [TestCase(18f, 0.75f)]
        [TestCase(23f, 23f / 24f)]
        public void TheHourIsReadAsAFractionOfTheDay(float hours, float fraction)
        {
            Assert.That(WeatherController.DayFraction(hours), Is.EqualTo(fraction).Within(1e-5f));
        }

        private NetMatchDefinition Rules(string weatherId, string timeOfDayId)
        {
            var rules = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _objects.Add(rules);
            rules.WeatherId = weatherId;
            rules.TimeOfDayId = timeOfDayId;
            return rules;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
