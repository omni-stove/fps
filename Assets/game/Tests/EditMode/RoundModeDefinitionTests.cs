using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// ラウンド系のモードの調整値のアセット (<see cref="SearchAndDestroyDefinition"/>・<see cref="KillConfirmedDefinition"/>・
    /// <see cref="TeamDeathmatchDefinition"/>) が既定値で作られ、値を Spec に渡し、不正な値をアセット名付きで断ることと、
    /// <see cref="TeamDeathmatchRunner"/> がアセットの値で試合を進めることを確かめる。
    /// </summary>
    public sealed class RoundModeDefinitionTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var runner in Object.FindObjectsByType<ModeRunnerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                runner.Stop();
            }

            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ANewSearchAndDestroyDefinitionHasTheSpecDefaults()
        {
            var spec = Create<SearchAndDestroyDefinition>("Snd").ToSpec(SearchAndDestroySpec.DefaultSiteCount);
            var defaults = new SearchAndDestroySpec();

            Assert.That(spec.SiteCount, Is.EqualTo(defaults.SiteCount));
            Assert.That(spec.RoundSeconds, Is.EqualTo(defaults.RoundSeconds));
            Assert.That(spec.PlantSeconds, Is.EqualTo(defaults.PlantSeconds));
            Assert.That(spec.DefuseSeconds, Is.EqualTo(defaults.DefuseSeconds));
            Assert.That(spec.FuseSeconds, Is.EqualTo(defaults.FuseSeconds));
            Assert.That(spec.WinsToWin, Is.EqualTo(defaults.WinsToWin));
            Assert.That(spec.PreparationSeconds, Is.EqualTo(defaults.PreparationSeconds));
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(defaults.SwapEveryRounds));
        }

        [Test]
        public void SearchAndDestroyDefinitionCopiesASpecAndGivesItBack()
        {
            var source = new SearchAndDestroySpec(3, 80f, 2f, 6f, 25f, 5, 1f, 3);
            var definition = Create<SearchAndDestroyDefinition>("Snd");

            definition.CopyFrom(source);
            var spec = definition.ToSpec(3);

            Assert.That(spec.RoundSeconds, Is.EqualTo(80f));
            Assert.That(spec.PlantSeconds, Is.EqualTo(2f));
            Assert.That(spec.DefuseSeconds, Is.EqualTo(6f));
            Assert.That(spec.FuseSeconds, Is.EqualTo(25f));
            Assert.That(spec.WinsToWin, Is.EqualTo(5));
            Assert.That(spec.PreparationSeconds, Is.EqualTo(1f));
            Assert.That(spec.SwapEveryRounds, Is.EqualTo(3));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void SearchAndDestroyDefinitionRejectsInvalidValuesWithItsName()
        {
            var definition = Create<SearchAndDestroyDefinition>("BrokenSnd");
            definition.PlantSeconds = float.NaN;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec(2));
            StringAssert.Contains("BrokenSnd", error.Message);
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());

            definition.PlantSeconds = SearchAndDestroySpec.DefaultPlantSeconds;
            Assert.Throws<InvalidOperationException>(() => definition.ToSpec(0), "a scene without sites is still refused");
        }

        [Test]
        public void ANewKillConfirmedDefinitionHasTheRunnerDefaults()
        {
            var spec = Create<KillConfirmedDefinition>("Kc").ToSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(KillConfirmedRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(KillConfirmedRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(KillConfirmedRunner.DefaultRespawnSeconds));
            Assert.That(spec.TagLifetimeSeconds, Is.EqualTo(KillConfirmedRunner.DefaultTagLifetimeSeconds));
        }

        [Test]
        public void KillConfirmedDefinitionCopiesASpecAndRejectsInvalidValues()
        {
            var definition = Create<KillConfirmedDefinition>("BrokenKc");
            definition.CopyFrom(new KillConfirmedSpec(7, 45f, 0f, 9f));

            var spec = definition.ToSpec();
            Assert.That(spec.ScoreLimit, Is.EqualTo(7));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(45f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(0f));
            Assert.That(spec.TagLifetimeSeconds, Is.EqualTo(9f));

            definition.ScoreLimit = 0;
            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            StringAssert.Contains("BrokenKc", error.Message);
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ANewTeamDeathmatchDefinitionHasTheRunnerDefaults()
        {
            var spec = Create<TeamDeathmatchDefinition>("Tdm").ToSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(TeamDeathmatchRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(TeamDeathmatchRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(TeamDeathmatchRunner.DefaultRespawnSeconds));
            Assert.That(spec.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void TeamDeathmatchDefinitionKeepsTheGivenRulesAndRejectsInvalidValues()
        {
            var definition = Create<TeamDeathmatchDefinition>("BrokenTdm");
            definition.CopyFrom(new TeamDeathmatchSpec(12, 200f, 1.5f));
            var rules = GameRules.Default.WithFriendlyFire(FriendlyFire.On);

            var spec = definition.ToSpec(rules);
            Assert.That(spec.ScoreLimit, Is.EqualTo(12));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(200f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.Rules, Is.SameAs(rules));

            definition.TimeLimitSeconds = float.PositiveInfinity;
            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());
            StringAssert.Contains("BrokenTdm", error.Message);
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void TheTeamDeathmatchRunnerUsesTheInspectorValuesWithoutADefinition()
        {
            var runner = Runner();
            runner.ScoreLimit = 15;
            runner.TimeLimitSeconds = 100f;
            runner.RespawnSeconds = 2f;

            var spec = runner.CreateSpec();

            Assert.That(spec.ScoreLimit, Is.EqualTo(15));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(100f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void TheTeamDeathmatchRunnerPlaysTheMatchWithTheDefinitionTimeLimit()
        {
            var runner = Runner();
            var definition = Create<TeamDeathmatchDefinition>("ShortTdm");
            definition.TimeLimitSeconds = 5f;
            definition.ScoreLimit = 3;
            definition.RespawnSeconds = 0.5f;
            runner.Definition = definition;
            Member("Alpha-1", 0);
            Member("Bravo-1", 1);

            Assert.That(runner.CreateSpec().ScoreLimit, Is.EqualTo(3));
            Assert.That(runner.TryBegin(), Is.True);
            Assert.That(runner.Session.TimeLimitSeconds, Is.EqualTo(5f), "not the Inspector's " + runner.TimeLimitSeconds);

            runner.Tick(4f);
            Assert.That(runner.Session.State, Is.EqualTo(GameModeState.Playing));

            runner.Tick(1f);
            Assert.That(runner.Session.State, Is.EqualTo(GameModeState.Ended));
        }

        private TeamDeathmatchRunner Runner()
        {
            var go = new GameObject("Runner");
            _objects.Add(go);
            return go.AddComponent<TeamDeathmatchRunner>();
        }

        private void Member(string name, int side)
        {
            var go = new GameObject(name, typeof(CapsuleCollider));
            _objects.Add(go);
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = new Side(side);
        }

        private T Create<T>(string name)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _objects.Add(asset);
            return asset;
        }
    }
}
