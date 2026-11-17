using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class NetMatchDefinitionMaxPlayersTests
    {
        private NetMatchDefinition _match;

        [SetUp]
        public void SetUp()
        {
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_match);
        }

        [Test]
        public void NewDefinitionTakesEightPlayers()
        {
            Assert.That(_match.MaxPlayers, Is.EqualTo(8));
            Assert.That(_match.ToSettings().MaxPlayers, Is.EqualTo(8));
        }

        [Test]
        public void SettingsCarryTheMaxPlayers()
        {
            _match.MaxPlayers = 2;

            var settings = _match.ToSettings();

            Assert.That(settings.MaxPlayers, Is.EqualTo(2));
            Assert.That(settings.ScoreLimit, Is.EqualTo(NetMatchSettings.DefaultScoreLimit));
        }

        [TestCase(0)]
        [TestCase(65)]
        public void MaxPlayersOutsideOneTo64IsRefused(int maxPlayers)
        {
            _match.MaxPlayers = maxPlayers;

            Assert.Throws<System.ArgumentOutOfRangeException>(() => _match.ToSettings());
        }
    }
}
