using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2WeatherPartTests
    {
        private readonly List<string> _logs = new List<string>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private WeatherController _scene;
        private NetV2WeatherPart _part;
        private int _searches;
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
            _logs.Clear();
            _searches = 0;
            _scene = null;
            _part = new NetV2WeatherPart(Find, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    var controller = go.GetComponent<WeatherController>();
                    if (controller != null)
                    {
                        controller.RestoreScene();
                    }
                }
            }

            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
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
        public void TheRulesRainyNightIsShownOnTheClient()
        {
            _scene = Controller("Map");

            _part.Apply(Snapshot(WeatherSpec.StormNightId, WeatherSpec.StormNightId));

            Assert.That(_part.AppliedTo, Is.SameAs(_scene));
            Assert.That(_scene.Current.HasValue, Is.True);
            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_scene.Current.Value.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_scene.Current.Value.IsNight, Is.True);
            Assert.That(_scene.RainObject.activeSelf, Is.True);
            Assert.That(_part.WeatherId, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_part.TimeOfDayId, Is.EqualTo(WeatherSpec.StormNightId));
        }

        [Test]
        public void TheSameRulesEveryTickDoNotSearchOrSelectAgain()
        {
            _scene = Controller("Map");
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));
            _scene.Select(WeatherSpec.ClearNoonId, null);

            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            Assert.That(_searches, Is.EqualTo(1));
            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId), "an unchanged rule is not applied again");
        }

        [Test]
        public void ChangedRulesAreSelectedAgain()
        {
            _scene = Controller("Map");
            _part.Apply(Snapshot(WeatherSpec.ClearNoonId, null));

            _part.Apply(Snapshot(WeatherSpec.StormNightId, WeatherSpec.OvercastDuskId));

            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_scene.Current.Value.TimeOfDay.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(_searches, Is.EqualTo(1));
        }

        [Test]
        public void EmptyIdsLeaveItToTheMapDefault()
        {
            _scene = Controller("Map");
            _scene.MapWeatherId = WeatherSpec.OvercastDuskId;

            _part.Apply(Snapshot(string.Empty, null));

            Assert.That(_part.WeatherId, Is.Null);
            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
        }

        [Test]
        public void WithoutAControllerItLogsOnceAndSearchesAgainAfterTheInterval()
        {
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            Assert.That(_searches, Is.EqualTo(1), "the scene is not searched every tick");
            Assert.That(_logs, Is.EqualTo(new[] { WeatherLog.NoController(WeatherSpec.StormNightId, null) }));

            _scene = Controller("Late");
            _part.Tick(NetV2WeatherPart.SearchIntervalSeconds * 0.5f);
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));
            Assert.That(_searches, Is.EqualTo(1));

            _part.Tick(NetV2WeatherPart.SearchIntervalSeconds);
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            Assert.That(_searches, Is.EqualTo(2));
            Assert.That(_part.AppliedTo, Is.SameAs(_scene));
            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(_logs, Has.Count.EqualTo(1));
        }

        [Test]
        public void ANewMapsControllerGetsTheRulesToo()
        {
            var first = Controller("First");
            _scene = first;
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            first.RestoreScene();
            Object.DestroyImmediate(first.gameObject);
            _scene = Controller("Second");
            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            Assert.That(_part.AppliedTo, Is.SameAs(_scene));
            Assert.That(_scene.Current.Value.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
        }

        [Test]
        public void MessagesAreLeftToTheOtherParts()
        {
            Assert.That(_part.Handle(new MapVoteRequestMessage()), Is.False);
        }

        [Test]
        public void AfterDisposeNothingIsSelected()
        {
            _scene = Controller("Map");
            _part.Dispose();

            _part.Apply(Snapshot(WeatherSpec.StormNightId, null));

            Assert.That(_searches, Is.Zero);
            Assert.That(_scene.Current.HasValue, Is.False);
        }

        private WeatherController Find()
        {
            _searches++;
            return _scene;
        }

        private WeatherController Controller(string name)
        {
            var sun = new GameObject("NetV2WeatherPartTests.Sun." + name);
            _objects.Add(sun);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;

            var go = new GameObject("NetV2WeatherPartTests." + name);
            _objects.Add(go);
            var controller = go.AddComponent<WeatherController>();
            controller.Sun = light;
            controller.ApplySpec(WeatherSpec.Default);
            return controller;
        }

        private static SnapshotMessage Snapshot(string weatherId, string timeOfDayId)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.Rules = new MatchRulesSync { WeatherId = weatherId, TimeOfDayId = timeOfDayId };
            return snapshot;
        }
    }
}
