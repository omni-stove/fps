using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class NetMatchDefinitionRoundsTests
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
        public void NewDefinitionPlaysTheDefaultRounds()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.RoundWinsToWin, Is.EqualTo(3));
            Assert.That(settings.RoundPreparationSeconds, Is.EqualTo(5f));
            Assert.That(settings.RoundTimeLimitSeconds, Is.EqualTo(120f));
        }
    }
}
