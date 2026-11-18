using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ボイスの名簿 (ProductUserId → プレイヤー Id) が出入りのたびに全員へ届き、名乗りの重なる Id を誰にも結び付けないことを確かめる
    public sealed class MatchRulesVoiceTests
    {
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            Start();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void NoRosterIsSentWhenNobodyHasAnId()
        {
            Join(1);
            Join(2);
            Leave(2);

            Assert.That(Rosters(1), Is.Empty);
            Assert.That(Rosters(2), Is.Empty);
        }

        [Test]
        public void JoiningWithAnIdSendsTheWholeRosterToEveryone()
        {
            Join(1, voiceUserId: _alice);
            Assert.That(Rosters(1), Has.Count.EqualTo(1));
            Assert.That(Last(1), Is.EqualTo("1=" + _alice));

            Join(2);
            Assert.That(Rosters(1), Has.Count.EqualTo(1), "nothing changed for those already in");
            Assert.That(Rosters(2), Has.Count.EqualTo(1));
            Assert.That(Last(2), Is.EqualTo("1=" + _alice), "a newcomer gets the roster it missed");

            Join(3, voiceUserId: _bob);
            Assert.That(Last(1), Is.EqualTo("1=" + _alice + ",3=" + _bob));
            Assert.That(Last(2), Is.EqualTo("1=" + _alice + ",3=" + _bob));
            Assert.That(Last(3), Is.EqualTo("1=" + _alice + ",3=" + _bob));
            Assert.That(_log.ToString(), Does.Contain("[Voice] roster players=2"));
        }

        [Test]
        public void LeavingDropsTheEntryForTheRest()
        {
            Join(1, voiceUserId: _alice);
            Join(2, voiceUserId: _bob);

            Leave(1);

            Assert.That(Last(2), Is.EqualTo("2=" + _bob));
        }

        [Test]
        public void TheRosterIsSentReliably()
        {
            Join(1, voiceUserId: _alice);

            var table = MatchMessages.CreateClientTable();
            var channels = new List<TransportChannel>();
            foreach (var sent in _transport.Sent)
            {
                if (table.Read(new ArraySegment<byte>(sent.Payload)) is VoiceRosterMessage)
                {
                    channels.Add(sent.Channel);
                }
            }

            Assert.That(channels, Is.Not.Empty);
            Assert.That(channels, Is.All.EqualTo(TransportChannel.Reliable));
        }

        [Test]
        public void TwoUnverifiedClaimsOfOneIdTieItToNobody()
        {
            Join(1, voiceUserId: _alice);
            Join(2, voiceUserId: _alice);

            Assert.That(Last(1), Is.Empty);
            Assert.That(Last(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Voice] product user id " + _alice + " is claimed by more than one player"));

            Leave(2);

            Assert.That(Last(1), Is.EqualTo("1=" + _alice), "the one left with the id gets it back");
        }

        [Test]
        public void AVerifiedIdWinsOverAClaimOfTheSameId()
        {
            Join(1, productUserId: _alice);
            Join(2, voiceUserId: _alice);

            Assert.That(Last(1), Is.EqualTo("1=" + _alice));
            Assert.That(Last(2), Is.EqualTo("1=" + _alice));
        }

        [Test]
        public void OnlyAnIdTheTransportVerifiedIsMarkedVerified()
        {
            Join(1, productUserId: _alice);
            Join(2, voiceUserId: _bob);

            var verified = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _transport.Read<VoiceRosterMessage>(1, roster =>
            {
                verified.Clear();
                for (var i = 0; i < roster.Count; i++)
                {
                    verified[roster.ProductUserId(i)] = roster.Verified(i);
                }

                return roster;
            });

            Assert.That(verified[_alice], Is.True);
            Assert.That(verified[_bob], Is.False);
        }

        [Test]
        public void BotsAreNeverInTheRoster()
        {
            Join(1, voiceUserId: _alice);
            _match.JoinBot(BotRoster.FirstBotPlayerId);

            Assert.That(Rosters(1), Has.Count.EqualTo(1));
            Assert.That(Last(1), Is.EqualTo("1=" + _alice));
        }

        [Test]
        public void BuildVoiceRosterResolvesClaims()
        {
            var players = new[]
            {
                new ServerPlayer(new ConnectionId(1), 1, Team.Alpha, "k1", voiceUserId: _alice),
                new ServerPlayer(new ConnectionId(2), 2, Team.Bravo, "k2", productUserId: _bob),
                new ServerPlayer(new ConnectionId(3), 3, Team.Alpha, "k3", voiceUserId: _bob),
                new ServerPlayer(new ConnectionId(4), 4, Team.Bravo, "k4"),
            };
            var roster = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new List<string>();

            MatchRulesModule.BuildVoiceRoster(
                players, roster, new Dictionary<string, MatchRulesModule.VoiceClaim>(StringComparer.OrdinalIgnoreCase), ambiguous);

            Assert.That(roster, Has.Count.EqualTo(2));
            Assert.That(roster[_alice], Is.EqualTo(1));
            Assert.That(roster[_bob], Is.EqualTo(2));
            Assert.That(ambiguous, Is.Empty);
        }

        [Test]
        public void AVerifiedIdIsAlsoTheVoiceId()
        {
            var player = new ServerPlayer(new ConnectionId(1), 1, Team.Alpha, "k1", productUserId: _alice, voiceUserId: _bob);

            Assert.That(player.VoiceUserId, Is.EqualTo(_alice));
            Assert.That(new ServerPlayer(new ConnectionId(2), 2, Team.Alpha, "k2").VoiceUserId, Is.Null);
        }

        [Test]
        public void JoinRefusesAMalformedVoiceId()
        {
            Assert.Throws<ArgumentException>(() => _match.Join(new ConnectionId(9), "k9", voiceUserId: "nope"));
        }

        private void Start()
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(GameRules.Default), PingSpec.Default, "memory", null);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _host = new ServerHost();
            _host.Register(new ServerMovement(ServerModules.MotorSettingsFrom(data)));
            _host.Register(new MatchRulesModule());
            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id, string productUserId = null, string voiceUserId = null)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id, productUserId, voiceUserId: voiceUserId);
            _host.OnConnected(connection);
        }

        private void Leave(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Leave(connection);
            _host.OnDisconnected(connection);
        }

        // 表は読むたびに同じメッセージを上書きするので、読んだその場で「プレイヤー Id=ProductUserId」の並びに写す
        private List<string> Rosters(int id)
        {
            var rosters = new List<string>();
            _transport.Read<VoiceRosterMessage>(id, roster =>
            {
                var entries = new List<string>();
                for (var i = 0; i < roster.Count; i++)
                {
                    entries.Add(roster.PlayerId(i).ToString(CultureInfo.InvariantCulture) + "=" + roster.ProductUserId(i));
                }

                rosters.Add(string.Join(",", entries));
                return roster;
            });
            return rosters;
        }

        private string Last(int id)
        {
            var rosters = Rosters(id);
            Assert.That(rosters, Is.Not.Empty, "player " + id.ToString(CultureInfo.InvariantCulture) + " got no roster");
            return rosters[rosters.Count - 1];
        }
    }
}
