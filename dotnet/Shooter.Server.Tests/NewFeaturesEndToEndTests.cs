using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // docs/dedicated-server.md の書き方どおりに server-data (ルール・マップ・tuning/economy.json・tuning/mutators.json・playlists) を置き、
    // -playlist で起動するのと同じ順に読んで ServerModules の一式を回す。Payload → 経済ありの S&D → Payload と回る間に、
    // 投票キック (Payload) と放置のキック (S&D) が起き、試合の設定 (match.*) は最初の項目のもののまま、rules.* だけが項目ごとに替わることを確かめる
    public sealed class NewFeaturesEndToEndTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _playlistId = "new-features";
        private const string _payloadMap = "NetArena01";
        private const string _siteMap = "NetTest";
        private const string _payloadRules = "Payload";
        private const string _siteRules = "SearchAndDestroy";
        private const string _carbineId = "0d6f5c1e-2b7a-4c39-9e84-5a1f3b7c2d90";
        private const int _carbinePrice = 500;
        private const float _payloadIdleKickSeconds = 20f;
        private const float _payloadIdleWarningSeconds = 10f;
        private const float _siteIdleKickSeconds = 15f;
        private const float _siteIdleWarningSeconds = 5f;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly VoteKickSpec _voteKick = new VoteKickSpec(true, 0.6f, 30f, 120f);

        // 定数の配列を引数へ直に渡すと CA1861 になるので、フィールドに置く
        private static readonly string[] _launchArgs = { ServerLaunch.PlaylistFlag, _playlistId };
        private static readonly string[] _entryRules = { _payloadRules, _siteRules };
        private static readonly int[] _middleCheckpoint = { 1 };

        // 床 (TestMaps の箱) の上面は y=0.5。Alpha (1 と 3) は x=-5、Bravo (2 と 4) は x=5 に出る
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private readonly HashSet<int> _active = new HashSet<int>();
        private TempDirectory _root;
        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private StubControl _control;
        private List<IServerModule> _modules;
        private ServerPlaylist _playlist;
        private ServerMatch _match;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private int _inputTick;
        private int _appliedKicks;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
            _modules = new List<IServerModule>();
            _active.Clear();
            _inputTick = 0;
            _appliedKicks = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
            _root.Dispose();
        }

        [Test]
        public void APlaylistGoesFromPayloadToSearchAndDestroyWithAnEconomyWhileAVoteAndIdlingKickPlayers()
        {
            WriteServerData();
            Start();

            Assert.That(_playlist.Entries.Select(entry => entry.RulesName), Is.EqualTo(_entryRules));
            Assert.That(_playlist.Entries[1].Rules.PlayerMaxHealth, Is.EqualTo(50), "the low-health mutator is laid over the S&D rules");
            Assert.That(_match.CheckPlaylist(), Is.Null, "each entry fits its own map when there is no map vote");

            // 1 試合目: Payload (NetArena01)。経済はラウンドの無い目的では動かず、購入は買える時間でないと断られる
            JoinAll(1, 2, 3, 4);
            Step(0.2f);
            Assert.That(_match.Map.Name, Is.EqualTo(_payloadMap));
            Assert.That(_rules.Objective, Is.TypeOf<PayloadRules>());
            Assert.That(_rules.Economy, Is.Null);
            Assert.That(_rules.Session.GetTeam(4), Is.EqualTo(Team.Bravo));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Payload] net attackers=Alpha"));
            Assert.That(log, Does.Contain(VoteKickLog.Settings(_voteKick)));
            Assert.That(log, Does.Contain("[Match] idle kick seconds=20 warning=10"));

            Send(1, new BuyRequestMessage { ItemId = _carbineId });
            Assert.That(BuyAnswers(1).Single().Verdict, Is.EqualTo(PurchaseVerdict.NotBuyTime));

            // 4 人のうち 4 を外す投票。票を入れられるのは 1・2・3 で、0.6 の割合なら 2 票で決まる
            Send(1, new VoteKickRequestMessage { TargetPlayerId = 4, InFavor = true });
            Assert.That(_rules.CurrentVoteKick.IsOpen, Is.True);
            Assert.That(_rules.CurrentVoteKick.RequiredVotes, Is.EqualTo(2));
            Send(2, new VoteKickRequestMessage { TargetPlayerId = 4, InFavor = true });
            Step(_tickSeconds);

            Assert.That(_control.Kicks, Is.EqualTo(new[] { (new ConnectionId(4), DisconnectNotice.VoteKickedReason) }));
            Assert.That(VoteKickStates(4).Last().Outcome, Is.EqualTo(VoteKickOutcome.Passed), "the target hears the result before being cut");
            Assert.That(_match.TryGetPlayer(new ConnectionId(4), out _), Is.False);
            Assert.That(_match.IsBarred("device4"), Is.True, "the server refuses the account until the next match");
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.Kicked(4, true)));

            // Payload を終え、2 項目目の S&D (NetTest) を始める。投票で外した人は次の試合から入れる
            FinishTheMatch();

            Assert.That(_match.Map.Name, Is.EqualTo(_siteMap));
            Assert.That(_playlist.Current.Position, Is.EqualTo(1));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[1].Rules));
            Assert.That(_rules.Rules.Objective, Is.EqualTo(MatchObjective.SearchAndDestroy));
            Assert.That(_rules.Rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(_match.IsBarred("device4"), Is.False);
            Assert.That(_rules.Session.GetTeam(1), Is.EqualTo(Team.Alpha));
            Assert.That(_rules.Session.GetTeam(2), Is.EqualTo(Team.Bravo));
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(Team.Alpha));
            log = _log.ToString();
            Assert.That(log, Does.Contain(ServerPlaylistLog.Entry(_playlist, _playlist.Entries[1], 0)));
            Assert.That(log, Does.Contain(MatchLog.MapChanged(_payloadMap, _siteMap)));
            Assert.That(log, Does.Contain("[Match] rules changed objective from=Payload to=SearchAndDestroy"));
            Assert.That(log, Does.Contain(VoteKickLog.Unbarred(1)));
            Assert.That(log, Does.Contain("[Match] idle kick seconds=15 warning=5"), "the idle rules come from the S&D entry");

            // 財布の額は SearchAndDestroy.json (5000) ではなく、最初の項目の match.economy (800) のまま
            Assert.That(_rules.Economy, Is.Not.Null);
            Assert.That(_rules.Economy.IsBuyTime, Is.True);
            Assert.That(_rules.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_rules.Economy.MoneyOf(1), Is.EqualTo(800));
            Assert.That(log, Does.Contain("[Economy] started players=3 start=800 max=16000 items=1 resetOnSwap=yes"));

            Send(1, new BuyRequestMessage { ItemId = _carbineId });

            Assert.That(BuyAnswers(1).Last().Verdict, Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(_rules.Economy.MoneyOf(1), Is.EqualTo(800 - _carbinePrice));
            Assert.That(_rules.WeaponsFor(1)[0].Id, Is.EqualTo(_carbineId));
            Assert.That(_log.ToString(), Does.Contain(
                "[Economy] player=1 bought item=" + _carbineId + " kind=Weapon slot=0 price=500 money=300"));

            // 3 は入力を送らなくなる。試合が替わったときに時計を戻しているので、S&D の始まりから 10 秒で警告、15 秒で切る
            _active.Remove(3);
            Step(9f);
            Assert.That(IdleWarnings(3), Is.Empty, "not yet 5 seconds before the kick");

            Step(2f);
            var warning = IdleWarnings(3).Single();
            Assert.That(warning.Active, Is.True);
            Assert.That(warning.RemainingSeconds, Is.EqualTo(_siteIdleWarningSeconds).Within(0.1f));
            Assert.That(_control.Kicks, Has.Count.EqualTo(1));

            Step(5f);

            Assert.That(_control.Kicks.Skip(1), Is.EqualTo(new[] { (new ConnectionId(3), DisconnectNotice.IdleKickedReason) }));
            Assert.That(_match.TryGetPlayer(new ConnectionId(3), out _), Is.False);
            Assert.That(_match.IsBarred("device3"), Is.False, "an idle kick does not bar the rejoin");
            Assert.That(IdleWarnings(1), Is.Empty, "the players who keep moving are never warned");
            log = _log.ToString();
            Assert.That(log, Does.Contain("[Match] idle kick player=3 client=3 after=15s"));
            Assert.That(log, Does.Contain(EconomyLog.Left(3)));

            // 1 ラウンド目: 1 が Bravo の最後の 1 人 (2) を倒して勝つ。キルとラウンドの額は最初の項目の match.economy から払う
            Assert.That(_rules.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            StepUntil(() => _rules.SearchAndDestroy.GetRoundWins(Side.Alpha) == 1, 1f);

            Assert.That(_rules.Economy.MoneyOf(1), Is.EqualTo(300 + 300 + 3000));
            Assert.That(_rules.Economy.MoneyOf(2), Is.EqualTo(800 + 1400));
            log = _log.ToString();
            Assert.That(log, Does.Contain("[Economy] player=1 kill victim=2 +300 money=600"));
            Assert.That(log, Does.Contain("[Economy] player=1 side=0 round won +3000 money=3600 lossStreak=0"));
            Assert.That(log, Does.Contain("[Economy] player=2 side=1 round lost +1400 money=2200 lossStreak=1"));

            // 2 ラウンド目も勝つと winsToWin (2) に届いて試合が終わり、1 項目目の Payload に戻る
            StepUntil(() => _rules.SearchAndDestroy.Phase == RoundPhase.Live, 5f);
            Assert.That(_rules.WeaponsFor(1)[0].Id, Is.EqualTo(_carbineId), "a survivor keeps the bought weapon into the next round");
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            StepUntil(() => _match.State == GameModeState.Ended, 2f);
            Assert.That(_rules.SearchAndDestroy.GetRoundWins(Side.Alpha), Is.EqualTo(2));

            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.Map.Name, Is.EqualTo(_payloadMap));
            Assert.That(_playlist.Current.Position, Is.Zero);
            Assert.That(_playlist.Round, Is.EqualTo(1));
            Assert.That(_rules.Rules, Is.SameAs(_playlist.Entries[0].Rules));
            Assert.That(_rules.Rules.PlayerMaxHealth, Is.EqualTo(GameRules.Default.PlayerMaxHealth), "the mutator only lasts its entry");
            Assert.That(_rules.Objective, Is.TypeOf<PayloadRules>());
            Assert.That(_rules.Economy, Is.Null, "the wallets end with the S&D entry");
            Assert.That(_rules.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.RifleId));
            log = _log.ToString();
            Assert.That(log, Does.Contain("[Match] rules changed objective from=SearchAndDestroy to=Payload"));
            Assert.That(log, Does.Contain(ServerPlaylistLog.Entry(_playlist, _playlist.Entries[0], 1)));
            Assert.That(
                log.Split('\n').Count(line => line.TrimEnd('\r') == "[Match] idle kick seconds=20 warning=10"),
                Is.EqualTo(2),
                "the Payload entry's idle rules come back with it");
            Assert.That(_control.Kicks, Has.Count.EqualTo(2), "no one else was cut on the way");
            Assert.That(_control.Failures, Is.Empty);
        }

        private static GameRules Plain()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithRegenPerSecond(0f);
        }

        private static WeaponSpec Carbine() => new WeaponSpec(
            _carbineId, "weapon.carbine", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.09f, 2f, 22, 90f, DamageFalloff.None);

        private static SearchAndDestroySpec SiteSpec()
        {
            return new SearchAndDestroySpec(
                siteCount: 2,
                roundSeconds: 60f,
                plantSeconds: 1f,
                defuseSeconds: 2f,
                fuseSeconds: 5f,
                winsToWin: 2,
                preparationSeconds: 1f,
                swapEveryRounds: 0,
                bombPickupRadius: 1.5f,
                defuseRadius: 2f);
        }

        // 買える武器 (カービン) は最初の項目のルールの武器の一覧に置く。試合の設定と同じく、武器も最初の項目のものを使い続けるため
        private static NetMatchFile RulesFile(string name, GameRules rules, NetMatchSettings settings)
        {
            var rifle = TestRules.Rifle();
            var handgun = TestRules.Handgun();
            return new NetMatchFile(
                name,
                rules,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { rifle, handgun },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { rifle, handgun, Carbine() }),
                null);
        }

        private static NetMapPoints Spawns(ControlPointArea[] sites, NetMapPayload payload)
        {
            return new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                sites,
                null,
                payload);
        }

        private void WriteServerData()
        {
            var payloadRules = Plain()
                .WithObjective(MatchObjective.Payload)
                .WithIdleKickSeconds(_payloadIdleKickSeconds)
                .WithIdleWarningSeconds(_payloadIdleWarningSeconds);
            var payloadSettings = new NetMatchSettings(
                respawnSeconds: 0f,
                restartDelaySeconds: 1f,
                mapVoteSeconds: 0f,
                searchAndDestroy: SiteSpec(),
                voteKick: _voteKick,
                economy: new EconomySpec(startMoney: 800));
            TestRules.Write(_root.Path, _payloadRules, RulesFile(_payloadRules, payloadRules, payloadSettings));

            // 2 項目目のルールの match.* はわざと最初の項目と変える。プレイリストでは使われないことを確かめるため
            var siteRules = Plain()
                .WithObjective(MatchObjective.SearchAndDestroy)
                .WithRespawnMode(RespawnMode.None)
                .WithIdleKickSeconds(_siteIdleKickSeconds)
                .WithIdleWarningSeconds(_siteIdleWarningSeconds);
            var siteSettings = new NetMatchSettings(restartDelaySeconds: 30f, mapVoteSeconds: 0f, economy: new EconomySpec(startMoney: 5000));
            TestRules.Write(_root.Path, _siteRules, RulesFile(_siteRules, siteRules, siteSettings));

            // 台車の道筋は NetArena01 にだけ、爆破地点に使う拠点は NetTest にだけ置く (例の server-data と同じ分け方)
            var route = new NetMapPayload(
                Team.Alpha,
                new[] { new Vector3(0f, 0.5f, -4f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.5f, 4f) },
                _middleCheckpoint,
                2f,
                4f);
            TestMaps.Write(_root.Path, _payloadMap, points: Spawns(Array.Empty<ControlPointArea>(), route));
            var sites = new[]
            {
                new ControlPointArea(0, new Vector3(0f, 0.5f, -6f), 2f, 3f),
                new ControlPointArea(1, new Vector3(0f, 0.5f, 6f), 2f, 3f),
            };
            TestMaps.Write(_root.Path, _siteMap, points: Spawns(sites, null));

            var tuning = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, ServerRulesData.EconomyTuningFileName),
                "{ \"resetOnSwap\": true, \"items\": [ { \"id\": \"" + _carbineId
                + "\", \"kind\": \"Weapon\", \"price\": 500, \"slot\": 0, \"nameKey\": \"weapon.carbine\" } ] }");
            File.WriteAllText(
                Path.Combine(tuning, ServerPlaylist.MutatorsTuningFileName),
                "{ \"mutators\": [ { \"id\": \"low-health\", \"rules\": { \"playerMaxHealth\": 50 } } ] }");

            var playlists = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.PlaylistsDirectoryName);
            Directory.CreateDirectory(playlists);
            File.WriteAllText(
                Path.Combine(playlists, "rotation.json"),
                "{ \"playlists\": [ { \"id\": \"" + _playlistId + "\", \"order\": \"Sequential\", \"entries\": [ "
                + "{ \"scene\": \"" + _payloadMap + "\", \"rules\": \"" + _payloadRules + "\" }, "
                + "{ \"scene\": \"" + _siteMap + "\", \"rules\": \"" + _siteRules + "\", \"mutators\": [\"low-health\"] } ] } ] }");
        }

        // ServerProgram の -playlist と同じ順に読む: 引数 → プレイリスト (項目のルールとミューテーター) → 最初の項目のマップと他の項目のマップ → モジュール。
        // 試合の枠には MatchServer と同じくマップとプレイリストを渡し、モジュールを始めてからプレイリストを確かめる
        private void Start()
        {
            Assert.That(ServerLaunch.TryParse(_launchArgs, _ => null, out var launch, out var parseError), Is.True, parseError);
            Assert.That(launch.Playlist, Is.EqualTo(_playlistId));

            var roots = new[] { _root.Path };
            Assert.That(ServerPlaylist.TryLoad(launch.Playlist, roots, 0, out _playlist, out var playlistError), Is.True, playlistError);
            var data = _playlist.Current.Data;
            Assert.That(data.BuyList, Is.Not.Null, "tuning/economy.json is beside the rules");

            Assert.That(ServerMapData.TryLoad(_playlist.Current.Map, roots, out var map, out var mapError), Is.True, mapError);
            var pool = ServerProgram.PlaylistMapPool(_playlist, data.Settings.MapPool);
            Assert.That(ServerMapData.TryLoadPool(map, pool, roots, out var maps, out var poolError), Is.True, poolError);

            var runtime = new ServerRuntime(
                TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), roots, roots, () => _transport);
            Assert.That(ServerModules.Create(launch, runtime, data, _modules, out var problem), Is.True, problem);
            _rules = _modules.OfType<MatchRulesModule>().Single();

            var settings = data.Settings;
            _match = new ServerMatch(
                settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time, settings.MaxTeamSizeDifference,
                settings.MapVoteSeconds);
            _match.UseMaps(map, maps);
            _match.UsePlaylist(_playlist);
            var context = new ServerContext(
                launch.Options,
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                _control,
                data);
            _host = new ServerHost();
            foreach (var module in _modules)
            {
                _host.Register(module);
            }

            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void JoinAll(params int[] ids)
        {
            foreach (var id in ids)
            {
                var connection = new ConnectionId((ulong)id);
                _match.Join(connection, "device" + id);
                _host.OnConnected(connection);
                _active.Add(id);
            }
        }

        private void Send(int id, IMessage message)
        {
            _host.OnMessage(new ConnectionId((ulong)id), message);
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
                Tick();
            }
        }

        private void StepUntil(Func<bool> condition, float maxSeconds)
        {
            for (var elapsed = 0f; elapsed < maxSeconds; elapsed += _tickSeconds)
            {
                if (condition())
                {
                    return;
                }

                Tick();
            }

            Assert.That(condition(), Is.True, "not reached in time\n" + _log);
        }

        // 動いている人は 1 tick に 1 つ、向きを変えた入力を送る (放置に数えさせない)。
        // 切る要求は MatchServer と同じく tick の後で試合の枠から外し、モジュールに切断を知らせる
        private void Tick()
        {
            _inputTick++;
            var ids = new List<int>(_active);
            ids.Sort();
            foreach (var id in ids)
            {
                if (!_match.TryGetPlayer(new ConnectionId((ulong)id), out _))
                {
                    continue;
                }

                var input = new InputMessage();
                input.Add(new InputFrame(_inputTick, Vector2.Zero, (_inputTick % 2) * 0.5f, InputButtons.None));
                Send(id, input);
            }

            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
            while (_appliedKicks < _control.Kicks.Count)
            {
                var connection = _control.Kicks[_appliedKicks++].Connection;
                _active.Remove((int)connection.Value);
                if (_match.Leave(connection))
                {
                    _host.OnDisconnected(connection);
                }
            }
        }

        private List<BuyAnswerMessage> BuyAnswers(int id)
        {
            return _transport.Read<BuyAnswerMessage>(id, m => new BuyAnswerMessage { ItemId = m.ItemId, Verdict = m.Verdict });
        }

        private List<IdleWarningMessage> IdleWarnings(int id)
        {
            return _transport.Read<IdleWarningMessage>(id, m => new IdleWarningMessage { Active = m.Active, RemainingSeconds = m.RemainingSeconds });
        }

        private List<VoteKickStateMessage> VoteKickStates(int id)
        {
            return _transport.Read<VoteKickStateMessage>(id, m => new VoteKickStateMessage
            {
                TargetPlayerId = m.TargetPlayerId,
                CallerPlayerId = m.CallerPlayerId,
                YesVotes = m.YesVotes,
                RequiredVotes = m.RequiredVotes,
                RemainingSeconds = m.RemainingSeconds,
                Outcome = m.Outcome,
            });
        }
    }
}
