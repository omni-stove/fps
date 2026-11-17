using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GunGameLadderDefinitionTests
    {
        private GunGameLadderDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<GunGameLadderDefinition>();
            _definition.name = "TestLadder";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ANewDefinitionHasTheDefaultTimeLimitAndRespawn()
        {
            Assert.That(_definition.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(_definition.RespawnSeconds, Is.EqualTo(2f));
            Assert.That(_definition.TimeLimitSeconds, Is.EqualTo(GunGameRunner.DefaultTimeLimitSeconds));
            Assert.That(_definition.RespawnSeconds, Is.EqualTo(GunGameRunner.DefaultRespawnSeconds));
        }

        [Test]
        public void TheSessionUsesTheDefinitionsTimeLimit()
        {
            _definition.TimeLimitSeconds = 30f;
            _definition.RespawnSeconds = 0f;

            var session = _definition.CreateSession(NewGunGame());

            Assert.That(session.TimeLimitSeconds, Is.EqualTo(30f));
            Assert.That(session.Layout, Is.InstanceOf<FreeForAll>());
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AnInvalidTimeLimitNamesTheAsset(float seconds)
        {
            _definition.TimeLimitSeconds = seconds;

            var e = Assert.Throws<InvalidOperationException>(() => _definition.CreateSession(NewGunGame()));

            Assert.That(e.Message, Does.Contain("TestLadder"));
            Assert.That(e.Message, Does.Contain("timeLimitSeconds"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-0.5f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void AnInvalidRespawnNamesTheAsset(float seconds)
        {
            _definition.RespawnSeconds = seconds;

            var e = Assert.Throws<InvalidOperationException>(() => _definition.CreateSession(NewGunGame()));

            Assert.That(e.Message, Does.Contain("TestLadder"));
            Assert.That(e.Message, Does.Contain("respawnSeconds"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ASessionNeedsAGunGame()
        {
            Assert.Throws<ArgumentNullException>(() => _definition.CreateSession(null));
        }

        private static GunGame NewGunGame()
        {
            return new GunGame(new GunGameLadder(new[] { new GunGameTier("weapon-a", null) }, null, true));
        }
    }
}
