using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class NetMatchDefinitionBetweenMatchesTests
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
        public void NewDefinitionKeepsTheMapTeamsAndPublicLobby()
        {
            var settings = _match.ToSettings();

            Assert.That(settings.MapPool, Is.Empty);
            Assert.That(settings.MapVoteSeconds, Is.EqualTo(0f));
            Assert.That(settings.MaxTeamSizeDifference, Is.EqualTo(0));
            Assert.That(settings.PrivateMatch, Is.False);
            Assert.That(settings.JoinCode, Is.Null);
            Assert.That(settings.ChatIntervalSeconds, Is.EqualTo(1f));
            Assert.That(settings.MaxChatLength, Is.EqualTo(120));
        }

        [Test]
        public void SettingsCarryTheMapPoolBalanceAndJoinCode()
        {
            _match.SetMapPool(new[] { "NetArena01", "NetArena02" });
            _match.MaxTeamSizeDifference = 1;
            _match.PrivateMatch = true;
            _match.JoinCode = "abc-123";

            var settings = _match.ToSettings();

            Assert.That(settings.MapPool, Is.EqualTo(new[] { "NetArena01", "NetArena02" }));
            Assert.That(settings.MaxTeamSizeDifference, Is.EqualTo(1));
            Assert.That(settings.PrivateMatch, Is.True);
            Assert.That(settings.JoinCode, Is.EqualTo("abc-123"));
        }

        [Test]
        public void SettingsCarryTheDisplayNameLimit()
        {
            Assert.That(_match.ToSettings().MaxDisplayNameLength, Is.EqualTo(16));

            _match.MaxDisplayNameLength = 10;

            Assert.That(_match.ToSettings().MaxDisplayNameLength, Is.EqualTo(10));
        }

        [Test]
        public void ADisplayNameLimitOverTheBytesIsRefused()
        {
            _match.MaxDisplayNameLength = NetMatchSettings.MaxMaxDisplayNameLength + 1;

            Assert.Throws<ArgumentOutOfRangeException>(() => _match.ToSettings());
        }

        [Test]
        public void JoinCodeWithASpaceIsRefused()
        {
            _match.JoinCode = "abc 123";

            Assert.Throws<ArgumentException>(() => _match.ToSettings());
        }

        [Test]
        public void MapPoolListingAMapTwiceIsRefused()
        {
            _match.SetMapPool(new[] { "NetArena01", "NetArena01" });

            Assert.Throws<ArgumentException>(() => _match.ToSettings());
        }
    }
}
