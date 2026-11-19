using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesPlaylistTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _firstMap = "NetTest";
        private const string _secondMap = "NetArena01";
        private const string _thirdMap = "NetArena02";
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerHost _host;
        private ServerPlaylist _playlist;

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
            _host?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void ThreeEntriesAreGoneThroughInOrderAndThenTheFirstComesBack()
        {
            Start(mapVoteSeconds: 0f, _firstMap, _secondMap);
            Join(1);
            Join(2);
            var teamDeathmatch = _rules.Rules;
            Assert.That(teamDeathmatch, Is.SameAs(_playlist.Entries[0].Rules));
            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));

            // 1 試合目 (TDM) は 1 キルで終わり、2 項目目の Domination を NetArena01 で始める。ミューテーターで体力が 50 になる
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(_rules.Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(_rules.Rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(_movement.Settings.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(_playlist.Current.Position, Is.EqualTo(1));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.Domination));
            var log = _log.ToString();
            Assert.That(log, Does.Contain(ServerPlaylistLog.Entry(_playlist, _playlist.Entries[1], 0)));
            Assert.That(log, Does.Contain(MatchLog.MapChanged(_firstMap, _secondMap)));
            Assert.That(log, Does.Contain("[Match] rules changed objective from=None to=Domination"));

            // 2 試合目 (Domination) はキルでは終わらないので枠から終え、3 項目目の Kill Confirmed を NetTest で始める
            FinishTheMatch();

            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[2].Rules));
            Assert.That(_rules.Rules.Objective, Is.EqualTo(MatchObjective.KillConfirmed));
            Assert.That(_rules.Rules.PlayerMaxHealth, Is.EqualTo(GameRules.Default.PlayerMaxHealth));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.KillConfirmed));
            Assert.That(_log.ToString(), Does.Contain(ServerPlaylistLog.Entry(_playlist, _playlist.Entries[2], 0)));
            Assert.That(_log.ToString(), Does.Contain(MatchLog.MapChanged(_secondMap, _firstMap)));

            // 3 試合目の後は 2 周目の 1 項目目に戻る。マップは同じなので替えず、ルールは始めと同じ物に戻る
            FinishTheMatch();

            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(_rules.Rules, Is.SameAs(teamDeathmatch));
            Assert.That(_playlist.Current.Position, Is.Zero);
            Assert.That(_playlist.Round, Is.EqualTo(1));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(_log.ToString(), Does.Contain(ServerPlaylistLog.Entry(_playlist, _playlist.Entries[0], 1)));
            Assert.That(JsonOf(ServerEventLog.MapChangedType), Has.Count.EqualTo(2));
        }

        [Test]
        public void TheMapVoteNamesTheNextEntryAndItsRulesArePlayedOnTheChosenMap()
        {
            Start(mapVoteSeconds: 1f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);

            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));

            // 投票の候補は今までどおり今のマップ以外で、次の項目 (2 項目目の Domination) の名前を一緒に送る
            Assert.That(_rules.CurrentMapVote.CurrentMapId, Is.EqualTo(_firstMap));
            Assert.That(_rules.CurrentMapVote.Candidates, Is.EqualTo(new[] { _secondMap, _thirdMap }));
            Assert.That(States(1).Last().NextPlaylistEntryName, Is.EqualTo("Domination"));
            Step(1.1f);

            // 票が無くても投票は候補のどれかに決め、そのマップで次の項目のルールを遊ぶ
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.AnyOf(_secondMap, _thirdMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(States(1).Select(state => state.NextPlaylistEntryName), Is.All.EqualTo("Domination"));
        }

        [Test]
        public void AMapVotedForPlaysTheNextEntrysRulesThere()
        {
            Start(mapVoteSeconds: 1f, _firstMap, _secondMap, _thirdMap);
            Join(1);
            Join(2);
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_rules.ServerMapVote(1, 1), Is.EqualTo(MapVoteCast.Cast));
            Assert.That(_rules.ServerMapVote(2, 1), Is.EqualTo(MapVoteCast.Cast));
            Step(1.1f);

            Assert.That(_match.Map.Name, Is.EqualTo(_thirdMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(_log.ToString(), Does.Contain(ServerPlaylistLog.MapVotedOver(_playlist, _playlist.Entries[1], _thirdMap)));

            // 投票で外れても項目は進み、次は 3 項目目を指す
            Assert.That(_playlist.Upcoming.Position, Is.EqualTo(2));
        }

        [Test]
        public void WithoutAPlaylistTheVoteSendsNoEntryName()
        {
            Start(mapVoteSeconds: 1f, usePlaylist: false, _firstMap, _secondMap);
            Join(1);
            Join(2);
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));

            Assert.That(_match.Playlist, Is.Null);
            Assert.That(_rules.CurrentMapVote.CurrentMapId, Is.EqualTo(_firstMap));
            Assert.That(States(1).Last().NextPlaylistEntryName, Is.Empty);
        }

        [Test]
        public void AnEntryItsMapCannotPlayIsFoundBeforeTheServerListens()
        {
            Start(mapVoteSeconds: 0f, _firstMap, _secondMap, controlPoints: 0);

            Assert.That(_match.CheckPlaylist(), Is.EqualTo(
                "playlist rotation entry 2/3 cannot use rules Domination: cannot use the rules with objective Domination for the next match: "
                + "cannot change to map NetArena01 for the Domination match: it has no control points"));
        }

        [Test]
        public void WithoutAMapVoteAnEntryOnlyHasToFitItsOwnMap()
        {
            // Domination の項目は NetArena01 にあり、拠点の無い NetTest へは TDM と Kill Confirmed でしか行かない
            Start(mapVoteSeconds: 0f, true, new[] { _firstMap, _secondMap }, name => TestRules.Points(name == _secondMap ? 3 : 0), Playlist);

            Assert.That(_match.CheckPlaylist(), Is.Null);
        }

        [Test]
        public void WithAMapVoteAnEntryHasToFitEveryMap()
        {
            // 投票は項目のマップ以外も選べるので、Domination の項目は拠点の無い NetTest でも遊べなければならない
            Start(mapVoteSeconds: 1f, true, new[] { _firstMap, _secondMap }, name => TestRules.Points(name == _secondMap ? 3 : 0), Playlist);

            Assert.That(_match.CheckPlaylist(), Is.EqualTo(
                "playlist rotation entry 2/3 cannot use rules Domination: cannot use the rules with objective Domination for the next match: "
                + "cannot change to map NetTest for the Domination match: it has no control points"));
        }

        [Test]
        public void TheSameRulesAreCheckedOnTheMapOfEveryEntryThatUsesThem()
        {
            // 1 項目目と 3 項目目は同じ Domination のルールで、3 項目目のマップ (NetTest) には拠点が無い
            Start(mapVoteSeconds: 0f, true, new[] { _secondMap, _firstMap }, name => TestRules.Points(name == _secondMap ? 3 : 0), settings =>
            {
                var source = new Playlist("repeat", PlaylistOrder.Sequential, new[]
                {
                    new PlaylistEntry(_secondMap, "Domination"),
                    new PlaylistEntry(_secondMap, "TeamDeathmatch"),
                    new PlaylistEntry(_firstMap, "Domination"),
                });
                var domination = TestRules.Data(Plain().WithObjective(MatchObjective.Domination), settings, name: "Domination");
                return new ServerPlaylist(
                    source,
                    new[]
                    {
                        new ServerPlaylistEntry(0, source.Entries[0], "Domination", domination),
                        new ServerPlaylistEntry(1, source.Entries[1], "TeamDeathmatch", TestRules.Data(Plain(), settings)),
                        new ServerPlaylistEntry(2, source.Entries[2], "Domination", domination),
                    },
                    0);
            });

            Assert.That(_rules.Rules.Objective, Is.EqualTo(MatchObjective.Domination), "the first map has control points, so the module starts");
            Assert.That(_match.CheckPlaylist(), Is.EqualTo(
                "playlist repeat entry 3/3 cannot use rules Domination: cannot use the rules with objective Domination for the next match: "
                + "cannot change to map NetTest for the Domination match: it has no control points"));
        }

        // Issue の確認手順の並び。台車の道筋は NetArena01 にしか無く、Domination の拠点は NetTest にしか無い
        [Test]
        public void TeamDeathmatchDominationAndPayloadTakeTurnsOnTheirOwnMaps()
        {
            Start(
                mapVoteSeconds: 0f,
                true,
                new[] { _secondMap, _firstMap },
                name => name == _secondMap ? TestRules.PayloadPoints() : TestRules.Points(3),
                PayloadPlaylist);
            Assert.That(_match.CheckPlaylist(), Is.Null);
            Join(1);
            Join(2);
            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));

            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step(1.1f);

            Assert.That(_match.Map.Name, Is.EqualTo(_firstMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.Domination));

            FinishTheMatch();

            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[2].Rules));
            Assert.That(_rules.Objective, Is.TypeOf<PayloadRules>());
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.Payload));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Match] rules changed objective from=Domination to=Payload"));
            Assert.That(log, Does.Contain("[Payload] net attackers=Alpha"));
            Assert.That(log, Does.Not.Contain(ServerPlaylistLog.RulesRefused(_playlist, _playlist.Entries[2], string.Empty)));

            FinishTheMatch();

            Assert.That(_match.Map.Name, Is.EqualTo(_secondMap));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[0].Rules));
            Assert.That(_playlist.Round, Is.EqualTo(1));
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));
        }

        [Test]
        public void APlaylistTheMapsCanPlayPassesTheCheck()
        {
            Start(mapVoteSeconds: 0f, _firstMap, _secondMap);

            Assert.That(_match.CheckPlaylist(), Is.Null);
        }

        [Test]
        public void ThePlaylistMustStartOnItsFirstMapAndUseOnlyTheMapsOfTheMatch()
        {
            var settings = new NetMatchSettings(scoreLimit: 1);
            var match = new ServerMatch(1f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            var playlist = Playlist(settings);
            Assert.Throws<InvalidOperationException>(() => match.UsePlaylist(playlist), "the maps come first");

            match.UseMaps(TestMaps.Data(_secondMap), new[] { TestMaps.Data(_firstMap) });
            var error = Assert.Throws<ArgumentException>(() => match.UsePlaylist(playlist));
            Assert.That(error.Message, Does.StartWith("playlist rotation starts on map NetTest but the match is on NetArena01"));

            var other = new ServerMatch(1f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            other.UseMaps(TestMaps.Data(_firstMap));
            error = Assert.Throws<ArgumentException>(() => other.UsePlaylist(playlist));
            Assert.That(error.Message, Does.StartWith("playlist rotation entry 2/3: map NetArena01 is not one of the maps of the match"));

            var good = new ServerMatch(1f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            good.UseMaps(TestMaps.Data(_firstMap), new[] { TestMaps.Data(_secondMap) });
            good.UsePlaylist(playlist);
            Assert.That(good.Playlist, Is.SameAs(playlist));
            Assert.Throws<InvalidOperationException>(() => good.UsePlaylist(playlist));
        }

        private static GameRules Plain()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private static readonly string[] _lowHealthIds = { "low-health" };

        // TDM (NetTest) → Domination にミューテーターを重ねたもの (NetArena01) → Kill Confirmed (NetTest)
        private static ServerPlaylist Playlist(NetMatchSettings settings)
        {
            var source = new Playlist("rotation", PlaylistOrder.Sequential, new[]
            {
                new PlaylistEntry(_firstMap, "TeamDeathmatch"),
                new PlaylistEntry(_secondMap, "Domination", _lowHealthIds),
                new PlaylistEntry(_firstMap, "KillConfirmed"),
            });
            var lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }", "test mutators");
            var domination = GameRulesMutator.ApplyAll(Plain().WithObjective(MatchObjective.Domination), new[] { lowHealth });
            return new ServerPlaylist(
                source,
                new[]
                {
                    new ServerPlaylistEntry(0, source.Entries[0], "TeamDeathmatch", TestRules.Data(Plain(), settings)),
                    new ServerPlaylistEntry(1, source.Entries[1], "Domination", TestRules.Data(domination, settings, name: "Domination")),
                    new ServerPlaylistEntry(
                        2, source.Entries[2], "KillConfirmed", TestRules.Data(Plain().WithObjective(MatchObjective.KillConfirmed), settings, name: "KillConfirmed")),
                },
                0);
        }

        private void Start(float mapVoteSeconds, params string[] maps)
        {
            Start(mapVoteSeconds, true, maps);
        }

        private void Start(float mapVoteSeconds, string first, string second, int controlPoints)
        {
            Start(mapVoteSeconds, true, new[] { first, second }, controlPoints);
        }

        // TDM (NetArena01) → Domination (NetTest) → Payload (NetArena01)
        private static ServerPlaylist PayloadPlaylist(NetMatchSettings settings)
        {
            var source = new Playlist("rotation", PlaylistOrder.Sequential, new[]
            {
                new PlaylistEntry(_secondMap, "TeamDeathmatch"),
                new PlaylistEntry(_firstMap, "Domination"),
                new PlaylistEntry(_secondMap, "Payload"),
            });
            return new ServerPlaylist(
                source,
                new[]
                {
                    new ServerPlaylistEntry(0, source.Entries[0], "TeamDeathmatch", TestRules.Data(Plain(), settings)),
                    new ServerPlaylistEntry(
                        1, source.Entries[1], "Domination", TestRules.Data(Plain().WithObjective(MatchObjective.Domination), settings, name: "Domination")),
                    new ServerPlaylistEntry(
                        2, source.Entries[2], "Payload", TestRules.Data(Plain().WithObjective(MatchObjective.Payload), settings, name: "Payload")),
                },
                0);
        }

        private void Start(float mapVoteSeconds, bool usePlaylist, params string[] maps)
        {
            Start(mapVoteSeconds, usePlaylist, maps, 3);
        }

        private void Start(float mapVoteSeconds, bool usePlaylist, string[] maps, int controlPoints)
        {
            Start(mapVoteSeconds, usePlaylist, maps, _ => TestRules.Points(controlPoints), Playlist);
        }

        // MatchServer と同じく、試合の枠にマップとプレイリストを渡してからモジュールを始める
        private void Start(
            float mapVoteSeconds, bool usePlaylist, string[] maps, Func<string, NetMapPoints> pointsOf, Func<NetMatchSettings, ServerPlaylist> playlistOf)
        {
            var settings = new NetMatchSettings(
                scoreLimit: 1, respawnSeconds: 0f, restartDelaySeconds: 1f, mapPool: maps, mapVoteSeconds: mapVoteSeconds);
            _playlist = playlistOf(settings);
            var data = _playlist.Entries[0].Data;
            _match = new ServerMatch(
                settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time, settings.MaxTeamSizeDifference,
                settings.MapVoteSeconds);
            var loaded = maps.Select(name => TestMaps.Data(name, pointsOf(name))).ToArray();
            _match.UseMaps(loaded[0], loaded);
            if (usePlaylist)
            {
                _match.UsePlaylist(_playlist);
            }

            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                loaded[0],
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(new ServerSnapshots(data.Rules, new ISnapshotContributor[] { _movement, _rules }));
            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void FinishTheMatch()
        {
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            _rules.Session.Finish(Team.Alpha);
            Step(0.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Step(1.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private MatchSnapshot LatestMatch(int id)
        {
            Step(_tickSeconds);
            var latest = default(MatchSnapshot);
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                latest = snapshot.Match;
                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return latest;
        }

        private List<MapVoteStateMessage> States(int connection)
        {
            return _transport.Read<MapVoteStateMessage>(connection, message => new MapVoteStateMessage
            {
                RemainingSeconds = message.RemainingSeconds,
                NextPlaylistEntryName = message.NextPlaylistEntryName,
            });
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal))
                .ToList();
        }
    }
}
