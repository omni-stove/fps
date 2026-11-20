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
    // 名前の名簿が出入りのたびに全員へ全員分届き、途中から入った人にも先にいた人と Bot の名前が届くことを確かめる
    public sealed class MatchRulesPlayerNamesTests
    {
        private static readonly DateTime _time = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        private static readonly string[] _badWord = { "badword" };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private MatchRulesModule _module;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
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
        public void EachJoinSendsTheWholeRosterToEveryone()
        {
            Start();

            Join(1, "Alice");
            Assert.That(Last(1), Is.EqualTo("1=Alice"));

            Join(2, "Bob");
            Assert.That(Last(1), Is.EqualTo("1=Alice,2=Bob"));
            Assert.That(Last(2), Is.EqualTo("1=Alice,2=Bob"), "a newcomer gets the names of those already in");
            Assert.That(Rosters(2), Has.Count.EqualTo(1));
        }

        [Test]
        public void ALeaveSendsTheRestToThoseLeft()
        {
            Start();
            Join(1, "Alice");
            Join(2, "Bob");
            Join(3, "Carol");

            Leave(2);

            Assert.That(Last(1), Is.EqualTo("1=Alice,3=Carol"));
            Assert.That(Last(3), Is.EqualTo("1=Alice,3=Carol"));
            Assert.That(_module.CurrentPlayerNames.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheRosterIsSentReliably()
        {
            Start();
            Join(1, "Alice");
            Join(2, "Bob");

            var table = MatchMessages.CreateClientTable();
            var channels = new List<TransportChannel>();
            foreach (var sent in _transport.Sent)
            {
                if (table.Read(new ArraySegment<byte>(sent.Payload)) is PlayerNamesMessage)
                {
                    channels.Add(sent.Channel);
                }
            }

            Assert.That(channels, Has.Count.EqualTo(3), "one to Alice, then one each to Alice and Bob");
            Assert.That(channels, Is.All.EqualTo(TransportChannel.Reliable));
        }

        [Test]
        public void BotsAreInTheRosterUnderTheirBotName()
        {
            Start();
            var bot = BotRoster.FirstBotPlayerId;
            _match.JoinBot(bot);

            Join(1, "Alice");

            Assert.That(Last(1), Is.EqualTo(Text(bot) + "=" + BotRoster.KillFeedName(bot) + ",1=Alice"));
        }

        [Test]
        public void ABotLeavingIsSentToThePlayers()
        {
            Start();
            Join(1, "Alice");
            var bot = BotRoster.FirstBotPlayerId;
            _match.JoinBot(bot);
            Assert.That(Last(1), Is.EqualTo("1=Alice," + Text(bot) + "=" + BotRoster.KillFeedName(bot)));

            Assert.That(_match.LeaveBot(bot), Is.True);

            Assert.That(Last(1), Is.EqualTo("1=Alice"));
        }

        [Test]
        public void APlayerWithoutANameIsShownByTheIdName()
        {
            Start();

            Join(1, null);

            Assert.That(Last(1), Is.EqualTo("1=" + BotRoster.KillFeedName(1)));
        }

        [Test]
        public void TheSameNameTwiceGetsANumber()
        {
            Start();
            Join(1, "Alice");

            Join(2, "alice");

            Assert.That(Last(2), Is.EqualTo("1=Alice,2=alice (2)"));
        }

        [Test]
        public void ANameWithAChatFilterWordIsShownByTheIdName()
        {
            Start(new ChatWordFilterSpec(_badWord));

            Join(1, "badword");

            Assert.That(Last(1), Is.EqualTo("1=" + BotRoster.KillFeedName(1)));
        }

        [Test]
        public void TheLogTellsWhoIsInTheRoster()
        {
            Start();
            Join(1, "Alice");
            Join(2, "Bob");
            Leave(1);

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Names] player=1 name=\"Alice\" roster players=1"));
            Assert.That(log, Does.Contain("[Names] player=2 name=\"Bob\" roster players=2"));
            Assert.That(log, Does.Contain("[Names] player=1 left roster players=1"));
        }

        [Test]
        public void StoppingForgetsTheRoster()
        {
            Start();
            Join(1, "Alice");

            _host.Stop();

            Assert.That(_module.CurrentPlayerNames, Is.Null);
        }

        private void Start(ChatWordFilterSpec words = null)
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var data = new ServerRulesData(
                "TeamDeathmatch", TestRules.File(GameRules.Default), PingSpec.Default, "memory", null, chatWords: words);
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
            _module = new MatchRulesModule();
            _host = new ServerHost();
            _host.Register(new ServerMovement(ServerModules.MotorSettingsFrom(data)));
            _host.Register(_module);
            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id, string displayName)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + Text(id), displayName: displayName);
            _host.OnConnected(connection);
        }

        private void Leave(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Leave(connection);
            _host.OnDisconnected(connection);
        }

        // 表は読むたびに同じメッセージを上書きするので、読んだその場で「プレイヤー Id=名前」の並びに写す
        private List<string> Rosters(int id)
        {
            var rosters = new List<string>();
            _transport.Read<PlayerNamesMessage>(id, roster =>
            {
                var entries = new List<string>();
                for (var i = 0; i < roster.Count; i++)
                {
                    entries.Add(Text(roster.PlayerId(i)) + "=" + roster.Name(i));
                }

                rosters.Add(string.Join(",", entries));
                return roster;
            });
            return rosters;
        }

        private string Last(int id)
        {
            var rosters = Rosters(id);
            Assert.That(rosters, Is.Not.Empty, "player " + Text(id) + " got no roster");
            return rosters[rosters.Count - 1];
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
