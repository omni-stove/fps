using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchBetweenMatchesSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static readonly string[] _maps = { "NetArena01", "NetArena02" };
        private static readonly string[] _sameMapTwice = { "NetArena01", "NetArena01" };
        private static readonly string[] _mapWithASlash = { "Maps/NetArena01" };

        private static NetMatchFile File(NetMatchSettings settings)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "TeamDeathmatch",
                GameRules.Default,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match)
        {
            return "{\"match\": " + match + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void DefaultsKeepTheMapTeamsAndPublicLobby()
        {
            var settings = NetMatchSettings.Default;

            Assert.That(settings.MapPool, Is.Empty);
            Assert.That(settings.MapVoteSeconds, Is.EqualTo(0f));
            Assert.That(settings.MaxTeamSizeDifference, Is.EqualTo(0));
            Assert.That(settings.PrivateMatch, Is.False);
            Assert.That(settings.JoinCode, Is.Null);
            Assert.That(settings.ChatIntervalSeconds, Is.EqualTo(1f));
            Assert.That(settings.MaxChatLength, Is.EqualTo(120));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(
                mapPool: _maps, mapVoteSeconds: 15f, maxTeamSizeDifference: 2, privateMatch: true, joinCode: "abc-123",
                chatIntervalSeconds: 0.5f, maxChatLength: 80);

            var text = NetMatchFile.Write(File(original));
            var read = NetMatchFile.Parse(text, "between.json").Settings;

            Assert.That(text, Does.Contain("\"mapPool\": [\"NetArena01\", \"NetArena02\"]"));
            Assert.That(text, Does.Contain("\"joinCode\": \"abc-123\""));
            Assert.That(read.MapPool, Is.EqualTo(_maps));
            Assert.That(read.MapVoteSeconds, Is.EqualTo(15f));
            Assert.That(read.MaxTeamSizeDifference, Is.EqualTo(2));
            Assert.That(read.PrivateMatch, Is.True);
            Assert.That(read.JoinCode, Is.EqualTo("abc-123"));
            Assert.That(read.ChatIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(read.MaxChatLength, Is.EqualTo(80));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "between.json")), Is.EqualTo(text));
        }

        [Test]
        public void DefaultsAreWrittenAsAnEmptyPoolAndEmptyJoinCode()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Contain("\"mapPool\": []"));
            Assert.That(text, Does.Contain("\"mapVoteSeconds\": 0"));
            Assert.That(text, Does.Contain("\"maxTeamSizeDifference\": 0"));
            Assert.That(text, Does.Contain("\"privateMatch\": false"));
            Assert.That(text, Does.Contain("\"joinCode\": \"\""));
            Assert.That(text, Does.Contain("\"chatIntervalSeconds\": 1"));
            Assert.That(text, Does.Contain("\"maxChatLength\": 120"));
            Assert.That(NetMatchFile.Parse(text, "default.json").Settings.JoinCode, Is.Null);
        }

        [Test]
        public void LeftOutValuesAreTheDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"scoreLimit\": 5}"), "old.json").Settings;

            Assert.That(read.ScoreLimit, Is.EqualTo(5));
            Assert.That(read.MapPool, Is.Empty);
            Assert.That(read.MapVoteSeconds, Is.EqualTo(NetMatchSettings.DefaultMapVoteSeconds));
            Assert.That(read.MaxTeamSizeDifference, Is.EqualTo(NetMatchSettings.DefaultMaxTeamSizeDifference));
            Assert.That(read.PrivateMatch, Is.False);
            Assert.That(read.JoinCode, Is.Null);
            Assert.That(read.ChatIntervalSeconds, Is.EqualTo(NetMatchSettings.DefaultChatIntervalSeconds));
            Assert.That(read.MaxChatLength, Is.EqualTo(NetMatchSettings.DefaultMaxChatLength));
        }

        [TestCase("{\"mapPool\": [\"a b\"]}", "match.mapPool")]
        [TestCase("{\"mapPool\": [\"NetArena01\", \"NetArena01\"]}", "match.mapPool")]
        [TestCase("{\"mapPool\": [1]}", "match.mapPool[0]")]
        [TestCase("{\"mapPool\": \"NetArena01\"}", "match.mapPool")]
        [TestCase("{\"mapVoteSeconds\": -1}", "match.mapVoteSeconds")]
        [TestCase("{\"maxTeamSizeDifference\": -1}", "match.maxTeamSizeDifference")]
        [TestCase("{\"privateMatch\": 1}", "match.privateMatch")]
        [TestCase("{\"joinCode\": \"abc 123\"}", "match.joinCode")]
        [TestCase("{\"joinCode\": \"abcdefghijklmnopq\"}", "match.joinCode")]
        [TestCase("{\"chatIntervalSeconds\": -0.5}", "match.chatIntervalSeconds")]
        [TestCase("{\"maxChatLength\": 0}", "match.maxChatLength")]
        [TestCase("{\"maxChatLength\": 171}", "match.maxChatLength")]
        public void BadValuesNameTheItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void MapPoolIsCopiedAndChecked()
        {
            var maps = new List<string>(_maps);
            var settings = new NetMatchSettings(mapPool: maps);
            maps.Add("NetArena03");

            Assert.That(settings.MapPool, Is.EqualTo(_maps));
            Assert.Throws<ArgumentException>(() => _ = new NetMatchSettings(mapPool: _sameMapTwice));
            Assert.Throws<ArgumentException>(() => _ = new NetMatchSettings(mapPool: _mapWithASlash));
        }

        [Test]
        public void MapPoolFitsAMapVote()
        {
            var full = new List<string>();
            for (var i = 0; i < NetMatchSettings.MaxMapPoolCount; i++)
            {
                full.Add("Map" + i);
            }

            Assert.That(new NetMatchSettings(mapPool: full).MapPool, Has.Count.EqualTo(16));
            full.Add("MapOver");
            Assert.Throws<ArgumentException>(() => _ = new NetMatchSettings(mapPool: full));
        }

        [Test]
        public void EmptyJoinCodeIsNone()
        {
            Assert.That(new NetMatchSettings(joinCode: string.Empty).JoinCode, Is.Null);
            Assert.That(new NetMatchSettings(privateMatch: true, joinCode: "Room_42").JoinCode, Is.EqualTo("Room_42"));
        }

        [Test]
        public void WithPrivacyChangesOnlyThePrivacy()
        {
            var settings = new NetMatchSettings(
                scoreLimit: 30,
                timeLimitSeconds: 600f,
                respawnSeconds: 5f,
                restartDelaySeconds: 15f,
                maxHealth: 150,
                minEnemySpawnDistance: 12f,
                reconnectGraceSeconds: 20f,
                maxShotOriginOffset: 2.5f,
                dominationScoreLimit: 200,
                captureSeconds: 6f,
                pointsPerSecond: 2f,
                maxCapturers: 3,
                maxPlayers: 12,
                mapPool: _maps,
                mapVoteSeconds: 20f,
                maxTeamSizeDifference: 2,
                chatIntervalSeconds: 0.5f,
                maxChatLength: 80);

            var changed = settings.WithPrivacy(true, "ABC234");

            Assert.That(changed.PrivateMatch, Is.True);
            Assert.That(changed.JoinCode, Is.EqualTo("ABC234"));
            Assert.That(settings.PrivateMatch, Is.False, "the original is left as it was");
            Assert.That(settings.JoinCode, Is.Null);
            Assert.That(changed.ScoreLimit, Is.EqualTo(30));
            Assert.That(changed.TimeLimitSeconds, Is.EqualTo(600f));
            Assert.That(changed.RespawnSeconds, Is.EqualTo(5f));
            Assert.That(changed.RestartDelaySeconds, Is.EqualTo(15f));
            Assert.That(changed.MaxHealth, Is.EqualTo(150));
            Assert.That(changed.MinEnemySpawnDistance, Is.EqualTo(12f));
            Assert.That(changed.ReconnectGraceSeconds, Is.EqualTo(20f));
            Assert.That(changed.MaxShotOriginOffset, Is.EqualTo(2.5f));
            Assert.That(changed.DominationScoreLimit, Is.EqualTo(200));
            Assert.That(changed.CaptureSeconds, Is.EqualTo(6f));
            Assert.That(changed.PointsPerSecond, Is.EqualTo(2f));
            Assert.That(changed.MaxCapturers, Is.EqualTo(3));
            Assert.That(changed.MaxPlayers, Is.EqualTo(12));
            Assert.That(changed.MapPool, Is.EqualTo(_maps));
            Assert.That(changed.MapVoteSeconds, Is.EqualTo(20f));
            Assert.That(changed.MaxTeamSizeDifference, Is.EqualTo(2));
            Assert.That(changed.ChatIntervalSeconds, Is.EqualTo(0.5f));
            Assert.That(changed.MaxChatLength, Is.EqualTo(80));
            Assert.That(changed.WithPrivacy(false, null).PrivateMatch, Is.False);
            Assert.Throws<ArgumentException>(() => settings.WithPrivacy(true, "has/slash"));
        }
    }
}
