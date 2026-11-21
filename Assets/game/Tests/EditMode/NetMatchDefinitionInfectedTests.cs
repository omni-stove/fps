using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // サーバーへ書き出す感染の調整値を、オフラインの InfectedRunner.CreateSpec と同じ順 (欄のアセット、調整値の JSON) で選ぶことを確かめる
    public sealed class NetMatchDefinitionInfectedTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private readonly List<Object> _objects = new List<Object>();
        private NetMatchDefinition _match;
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _match = Make<NetMatchDefinition>();
            _content = Make<GameContent>();
            GameContent.Use(_content);
            KitTuning.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            KitTuning.Reload();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void TheWiredInfectedAssetIsCarried()
        {
            var definition = Make<InfectedDefinition>();
            definition.CopyFrom(new InfectedSpec(2, 12f, 240f, 180, 1.2f, 2f, 3, 4));
            _match.Infected = definition;

            var spec = _match.ToSettings().Infected;

            Assert.That(spec.InitialInfected, Is.EqualTo(2));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(12f));
            Assert.That(spec.RoundSeconds, Is.EqualTo(240f));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(180));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(1.2f));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(2f));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(3));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(4));
            Assert.That(spec.HasInfectedWeapon, Is.False);
        }

        [Test]
        public void WithoutAnAssetTheTuningJsonOfTheGameContentIsRead()
        {
            var json = new TextAsset("{\"initialInfected\": 3, \"roundSeconds\": 180}");
            _objects.Add(json);
            _content.InfectedTuning = json;

            var spec = _match.ToSettings().Infected;

            Assert.That(spec.InitialInfected, Is.EqualTo(3));
            Assert.That(spec.RoundSeconds, Is.EqualTo(180f));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(InfectedSpec.DefaultStartDelaySeconds));
        }

        [Test]
        public void AnInvalidInfectedAssetStopsTheSettings()
        {
            var definition = Make<InfectedDefinition>();
            definition.name = "BrokenInfected";
            definition.InitialInfected = 0;
            _match.Infected = definition;

            var e = Assert.Throws<InvalidOperationException>(() => _match.ToSettings());

            Assert.That(e.Message, Does.Contain("BrokenInfected").And.Contain("initialInfected"));
        }

        [Test]
        public void TheCarriedValuesSurviveTheServerFile()
        {
            var definition = Make<InfectedDefinition>();
            definition.CopyFrom(new InfectedSpec(initialInfected: 2, roundSeconds: 200f));
            _match.Infected = definition;
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);
            var file = new NetMatchFile(
                "Infected",
                GameRules.Default.WithObjective(MatchObjective.Infected),
                _match.ToSettings(),
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);

            var read = NetMatchFile.Parse(NetMatchFile.Write(file), "Infected.json").Settings.Infected;

            Assert.That(read.InitialInfected, Is.EqualTo(2));
            Assert.That(read.RoundSeconds, Is.EqualTo(200f));
        }

        private T Make<T>()
            where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }
    }
}
