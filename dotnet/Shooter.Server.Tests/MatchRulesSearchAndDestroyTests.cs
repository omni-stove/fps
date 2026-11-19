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
    public sealed class MatchRulesSearchAndDestroyTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        // TestRules.Points の拠点 0 (A) の中心は (0, 0, -15)、半径 4、高さ 3
        private static readonly Vector3 _siteA = new Vector3(0f, 0.5f, -15f);
        private static readonly Vector3 _away = new Vector3(20f, 0.5f, 20f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void TheAttackersWinTheMatchByDetonatingTheBombInEveryRound()
        {
            Start(Spec(winsToWin: 2));
            Join(1);
            Join(2);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(_module.Objective, Is.TypeOf<SearchAndDestroyRules>());
            Assert.That(_log.ToString(), Does.Contain(
                "[SearchAndDestroy] net sites=2 winsToWin=2 round=30s plant=1s defuse=2s fuse=5s preparation=1s swapEvery=0"));

            Step();
            var game = _module.SearchAndDestroy;
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(game.Attacker, Is.EqualTo(Side.Alpha));
            Assert.That(game.CarrierId, Is.EqualTo(1), "the first attacker carries the bomb");
            Assert.That(_module.CanFire(1), Is.False);
            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Ignored));

            PlayRoundToDetonation(round: 1);

            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried), "the next round hands the bomb back to an attacker");
            Assert.That(Match().AlphaPoints, Is.EqualTo(1));
            Assert.That(Match().BravoPoints, Is.Zero);

            PlayRoundToDetonation(round: 2);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Ended));
            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.SearchAndDestroy));
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(2));
            Assert.That(snapshot.HasRound, Is.True);
            Assert.That(snapshot.Round, Is.EqualTo(RoundPhase.Ended));
            Assert.That(snapshot.Bomb, Is.EqualTo(BombState.Detonated));
            Assert.That(JsonOf("match_ended").Single(), Does.Contain("\"winner\":\"Alpha\"").And.Contain("\"scoreAlpha\":\"2\""));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[SearchAndDestroy] round=1 attacker=0 defender=1"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] live round=1"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] planted player=1 site=A"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] detonated site=A"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] round_end round=1 winner=0 reason=Detonated wins=0:1,1:0"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] round_end round=2 winner=0 reason=Detonated wins=0:2,1:0"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] ended winner=0"));
            Assert.That(log, Does.Contain("[Match] round phase=Ended"));

            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended), "the ended match does not start another round");
            Assert.That(game.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void PlantingShowsTheSiteAndTheFuseInTheSnapshot()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Advance(1.1f);
            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(Match().RemainingSeconds, Is.EqualTo(30), "the round clock runs before the plant");
            Assert.That(Match().HasActivePoint, Is.False);

            Place(1, _siteA);
            Place(2, _away);
            Advance(0.5f);

            var planting = Match();
            Assert.That(_module.SearchAndDestroy.PlantingSite, Is.EqualTo(0));
            Assert.That(planting.HasBomb, Is.True);
            Assert.That(planting.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(planting.HasActivePoint, Is.True);
            Assert.That(planting.ActivePointIndex, Is.Zero);
            Assert.That(planting.ObjectiveRemainingSeconds, Is.Zero);
            Assert.That(planting.PlantFraction, Is.EqualTo(_module.SearchAndDestroy.PlantFraction));
            Assert.That(planting.PlantFraction, Is.GreaterThan(0.4f).And.LessThan(0.6f), "half of the 1 second plant has passed");
            Assert.That(planting.HasDefuser, Is.False);
            var carried = Items().Single();
            Assert.That(carried.ItemId, Is.EqualTo(SearchAndDestroyRules.BombItemId));
            Assert.That(carried.Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(carried.Kind, Is.EqualTo(ObjectiveItemKind.Bomb));
            Assert.That(carried.Team, Is.EqualTo(Team.Alpha), "the bomb belongs to the attackers");
            Assert.That(carried.State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(carried.HasCarrier, Is.True);
            Assert.That(carried.CarrierId, Is.EqualTo(1));
            Assert.That(carried.Position, Is.EqualTo(_siteA));

            Advance(0.6f);

            var planted = Match();
            Assert.That(planted.Bomb, Is.EqualTo(BombState.Planted));
            Assert.That(planted.HasActivePoint, Is.True);
            Assert.That(planted.ActivePointIndex, Is.Zero);
            Assert.That(planted.ObjectiveRemainingSeconds, Is.EqualTo(5));
            Assert.That(planted.RemainingSeconds, Is.EqualTo(5), "the clock shows the fuse after the plant");
            Assert.That(planted.PlantFraction, Is.Zero, "nobody is planting once the bomb is planted");
            var item = Items().Single();
            Assert.That(item.State, Is.EqualTo(ObjectiveItemState.Planted));
            Assert.That(item.HasCarrier, Is.False);
            Assert.That(item.Position, Is.EqualTo(_siteA));

            Place(1, _away);
            Advance(1f);
            Assert.That(Items().Single().Position, Is.EqualTo(_siteA), "the planted bomb stays where it was planted");
            Assert.That(Match().ObjectiveRemainingSeconds, Is.EqualTo(4));
        }

        [Test]
        public void TheDefendersWinTheRoundByDefusingTheBomb()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Advance(1.1f);
            Place(1, _siteA);
            Place(2, _away);
            Advance(1.1f);
            Assert.That(_module.SearchAndDestroy.Bomb, Is.EqualTo(BombState.Planted));

            Place(2, _siteA + new Vector3(1f, 0f, 0f));
            Advance(1f);
            Assert.That(_module.SearchAndDestroy.DefuserId, Is.EqualTo(2));
            Assert.That(_module.SearchAndDestroy.Bomb, Is.EqualTo(BombState.Planted), "the defuse takes 2 seconds");
            var defusing = Match();
            Assert.That(defusing.HasDefuser, Is.True);
            Assert.That(defusing.DefuserId, Is.EqualTo(2));
            Assert.That(defusing.DefuseFraction, Is.EqualTo(_module.SearchAndDestroy.DefuseFraction));
            Assert.That(defusing.DefuseFraction, Is.GreaterThan(0.4f).And.LessThan(0.6f), "half of the 2 second defuse has passed");
            Assert.That(defusing.HasLastRound, Is.False, "no round has ended yet");

            Advance(1.1f);

            var game = _module.SearchAndDestroy;
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(_match.BravoScore, Is.EqualTo(1));
            var after = Match();
            Assert.That(after.BravoPoints, Is.EqualTo(1));
            Assert.That(after.HasLastRound, Is.True);
            Assert.That(after.LastRoundEnd, Is.EqualTo(RoundEndReason.Defused));
            Assert.That(after.HasLastRoundWinner, Is.True);
            Assert.That(after.LastRoundWinner, Is.EqualTo(Team.Bravo));
            Assert.That(after.HasDefuser, Is.False, "the defuser is not sent once the bomb is defused");
            Assert.That(after.DefuseFraction, Is.Zero);
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[SearchAndDestroy] defused player=2"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] round_end round=1 winner=1 reason=Defused wins=0:0,1:1"));
        }

        [Test]
        public void ADefenderOutOfReachDoesNotDefuse()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Advance(1.1f);
            Place(1, _siteA);
            Place(2, _away);
            Advance(1.1f);

            Place(2, _siteA + new Vector3(3f, 0f, 0f));
            Advance(1f);
            Assert.That(_module.SearchAndDestroy.DefuserId, Is.Null, "3 m is outside the 2 m defuse radius");

            Place(2, _siteA + new Vector3(0f, 2.5f, 0f));
            Advance(1f);
            Assert.That(_module.SearchAndDestroy.DefuserId, Is.Null, "a floor above the bomb is out of reach");
        }

        [Test]
        public void KillingTheCarrierDropsTheBombWhereTheCarrierFellForAnotherAttacker()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Advance(1.1f);
            var fell = new Vector3(5f, 0.5f, 5f);
            Place(1, fell);
            Place(3, _away);

            Assert.That(_module.ApplyDamage(2, 1, 100), Is.EqualTo(DamageOutcome.Killed));

            var game = _module.SearchAndDestroy;
            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live), "another attacker is still standing");
            var dropped = Items().Single();
            Assert.That(dropped.State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(dropped.HasCarrier, Is.False);
            Assert.That(dropped.Position, Is.EqualTo(fell));
            Assert.That(Match().Bomb, Is.EqualTo(BombState.Dropped));

            Place(2, fell);
            Step();
            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped), "defenders do not pick the bomb up");

            Place(3, fell + new Vector3(1f, 0f, 0f));
            Step();

            Assert.That(game.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(game.CarrierId, Is.EqualTo(3));
            Assert.That(Items().Single().CarrierId, Is.EqualTo(3));
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[SearchAndDestroy] drop player=1"));
            Assert.That(log, Does.Contain("[SearchAndDestroy] pickup player=3"));
        }

        [Test]
        public void EliminatingTheDefendersWinsTheRoundAndBringsEveryoneBack()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Advance(1.1f);

            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step();

            var game = _module.SearchAndDestroy;
            Assert.That(game.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.DefendersEliminated));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_module.Session.IsAlive(2), Is.True, "the next round brings the fallen back");
            Assert.That(Row(2).Deaths, Is.EqualTo(1), "the next round keeps the stats");
            Assert.That(_log.ToString(), Does.Contain("[SearchAndDestroy] round_end round=1 winner=0 reason=DefendersEliminated wins=0:1,1:0"));
        }

        [Test]
        public void AFallenPlayerStaysDownUntilTheNextRound()
        {
            Start(Spec(), respawnSeconds: 0f);
            Join(1);
            Join(2);
            Join(3);
            Advance(1.1f);

            Assert.That(_module.ApplyDamage(2, 3, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(2f);

            Assert.That(_module.Session.IsAlive(3), Is.False);
            Assert.That(_module.CanFire(3), Is.False);
            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(_match.BravoScore, Is.Zero, "kills do not score in rounds");
        }

        [Test]
        public void TheDefendersWinTheRoundWhenTimeRunsOutBeforeThePlant()
        {
            Start(Spec(roundSeconds: 2f));
            Join(1);
            Join(2);
            Advance(1.1f);
            Assert.That(Match().RemainingSeconds, Is.EqualTo(2));

            Advance(2.1f);

            var game = _module.SearchAndDestroy;
            Assert.That(game.GetRoundWins(Side.Bravo), Is.EqualTo(1));
            Assert.That(game.LastRoundEnd, Is.EqualTo(RoundEndReason.TimeUp));
            Assert.That(_log.ToString(), Does.Contain("[SearchAndDestroy] round_end round=1 winner=1 reason=TimeUp wins=0:0,1:1"));
        }

        [Test]
        public void TheSidesSwapAfterTheConfiguredRounds()
        {
            Start(Spec(swapEveryRounds: 1));
            Join(1);
            Join(2);
            Advance(1.1f);

            _module.ApplyDamage(1, 2, 100);
            Step();

            var game = _module.SearchAndDestroy;
            Assert.That(game.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(game.CarrierId, Is.EqualTo(2));
            Assert.That(Items().Single().Team, Is.EqualTo(Team.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[SearchAndDestroy] swap attacker=1 defender=0"));
            Assert.That(_log.ToString(), Does.Contain("[SearchAndDestroy] round=2 attacker=1 defender=0"));
        }

        [Test]
        public void TheSnapshotCountsDownThePreparation()
        {
            Start(Spec(preparationSeconds: 4f));
            Join(1);
            Join(2);
            Step();

            Advance(1f);

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.SearchAndDestroy));
            Assert.That(snapshot.HasRound, Is.True);
            Assert.That(snapshot.Round, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(3));
            Assert.That(snapshot.HasBomb, Is.True);
            Assert.That(snapshot.Bomb, Is.EqualTo(BombState.Carried));
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(4));
            Assert.That(snapshot.HasLastRound, Is.False);
        }

        [Test]
        public void NothingIsSentAboutTheBombBeforeTheFirstRound()
        {
            Start(Spec(winsToWin: 3));

            var snapshot = Match();
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.SearchAndDestroy));
            Assert.That(snapshot.HasBomb, Is.False);
            Assert.That(snapshot.HasActivePoint, Is.False);
            Assert.That(snapshot.RoundNumber, Is.Zero);
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(3), "the rounds to win are the configured ones");
            Assert.That(snapshot.HasLastRound, Is.False);
            Assert.That(snapshot.HasDefuser, Is.False);
            Assert.That(Items(), Is.Empty);
        }

        [Test]
        public void TheNextMatchStartsFromNoRoundWins()
        {
            Start(Spec(winsToWin: 1, preparationSeconds: 0f), restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_match.AlphaScore, Is.EqualTo(1));

            Advance(2.1f);

            var game = _module.SearchAndDestroy;
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.AlphaScore, Is.Zero);
            Assert.That(game.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(game.RoundNumber, Is.EqualTo(1));
            Assert.That(game.Attacker, Is.EqualTo(Side.Alpha));
            var snapshot = Match();
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.HasLastRound, Is.False, "the next match forgets the last round of the previous one");
            Assert.That(snapshot.HasLastRoundWinner, Is.False);
        }

        [Test]
        public void TheNextRoundCarriesItsNumberAndHowThePreviousOneEnded()
        {
            Start(Spec(preparationSeconds: 4f));
            Join(1);
            Join(2);
            Advance(4.1f);
            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Live));

            _module.ApplyDamage(1, 2, 100);
            Step();

            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Preparing));
            var snapshot = Match();
            Assert.That(snapshot.RoundNumber, Is.EqualTo(2));
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(4));
            Assert.That(snapshot.HasLastRound, Is.True);
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.DefendersEliminated));
            Assert.That(snapshot.HasLastRoundWinner, Is.True);
            Assert.That(snapshot.LastRoundWinner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void TheCarrierLeavingDropsTheBombDuringTheRound()
        {
            Start(Spec());
            Join(1);
            Join(2);
            Join(3);
            Advance(1.1f);
            var left = new Vector3(-3f, 0.5f, 2f);
            Place(1, left);

            _match.Leave(new ConnectionId(1));
            Step();

            var game = _module.SearchAndDestroy;
            Assert.That(game.HasPlayer(1), Is.False);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Dropped));
            Assert.That(Items().Single().Position, Is.EqualTo(left));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live));
        }

        [Test]
        public void AMapWithoutControlPointsCannotHostTheMatch()
        {
            var e = Assert.Throws<InvalidOperationException>(() => Start(Spec(), controlPoints: 0));

            Assert.That(e.Message, Is.EqualTo("cannot start the SearchAndDestroy match on map NetTest: it has no control points to use as bomb sites"));
        }

        [Test]
        public void CheckMapRefusesMapsWithoutSitesOrWithMoreSitesThanTheSnapshotCarries()
        {
            var rules = new SearchAndDestroyRules(Settings(Spec()), new NullHost(_log));

            var none = Assert.Throws<InvalidOperationException>(
                () => rules.CheckMap(TestMaps.Data("Empty", TestRules.Points()), TestRules.Points()));
            Assert.That(none.Message, Is.EqualTo("cannot change to map Empty for the SearchAndDestroy match: it has no control points to use as bomb sites"));

            var many = TestRules.Points(controlPoints: SnapshotMessage.MaxControlPoints + 1);
            var tooMany = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(TestMaps.Data("Wide", many), many));
            Assert.That(tooMany.Message, Is.EqualTo(
                "cannot change to map Wide for the SearchAndDestroy match: it has 17 control points but at most 16 bomb sites fit in a snapshot"));

            var three = TestRules.Points(controlPoints: 3);
            Assert.DoesNotThrow(() => rules.CheckMap(TestMaps.Data("Three", three), three));
            Assert.Throws<InvalidOperationException>(() => rules.Start(), "Start needs a map");
        }

        [Test]
        public void TheRulesTakeTheSiteCountFromTheMapAndTheRestFromTheSettings()
        {
            var rules = new SearchAndDestroyRules(Settings(Spec(winsToWin: 3)), new NullHost(_log));
            Assert.That(rules.Game, Is.Null);

            var three = TestRules.Points(controlPoints: 3);
            rules.UseMap(TestMaps.Data("Three", three), three);

            var spec = rules.Game.Spec;
            Assert.That(spec.SiteCount, Is.EqualTo(3));
            Assert.That(spec.WinsToWin, Is.EqualTo(3));
            Assert.That(spec.PlantSeconds, Is.EqualTo(1f));
            Assert.That(spec.FuseSeconds, Is.EqualTo(5f));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.SearchAndDestroy));
            Assert.That(rules.PlaysInRounds, Is.True);
            Assert.That(rules.KeepsMatchScore, Is.True);
            Assert.That(rules.HoldsFire, Is.False, "the first round has not started preparing");
            Assert.That(rules.CurrentRoundPhase(), Is.EqualTo(RoundPhase.Waiting));
            Assert.That(rules.DecideWinner(Team.Bravo), Is.Null, "no round wins is a draw whoever has more kills");
            var session = rules.CreateSessionSpec(GameRules.Default, new NetMatchSettings(respawnSeconds: 2f));
            Assert.That(session.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(session.TimeLimitSeconds, Is.EqualTo(float.MaxValue));
            Assert.That(session.RespawnSeconds, Is.EqualTo(2f));
            Assert.That(session.RespawnsByRound, Is.True);
        }

        [Test]
        public void TheFactoryOffersSearchAndDestroy()
        {
            var rules = ServerObjectiveFactory.Create(MatchObjective.SearchAndDestroy, new NetMatchSettings(), new NullHost(_log));

            Assert.That(rules, Is.TypeOf<SearchAndDestroyRules>());
            Assert.That(rules.PlaysInRounds, Is.True);
        }

        [Test]
        public void OtherObjectivesSendNoObjectiveItems()
        {
            Start(Spec(), objective: MatchObjective.Elimination);
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.SearchAndDestroy, Is.Null);
            Assert.That(Items(), Is.Empty);
        }

        // ラウンドの合間から始めて、攻撃側 1 が地点 A に爆弾を設置し、起爆させる
        private void PlayRoundToDetonation(int round)
        {
            var game = _module.SearchAndDestroy;
            Advance(1.1f);
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Live), "round " + round + " is live");
            Assert.That(game.CarrierId, Is.EqualTo(1));
            Place(1, _siteA);
            Place(2, _away);

            Advance(1.1f);
            Assert.That(game.Bomb, Is.EqualTo(BombState.Planted), "round " + round + " has the bomb planted");

            Advance(5.2f);
        }

        private static SearchAndDestroySpec Spec(
            int winsToWin = 4, float roundSeconds = 30f, float preparationSeconds = 1f, int swapEveryRounds = 0)
        {
            return new SearchAndDestroySpec(
                siteCount: 2,
                roundSeconds: roundSeconds,
                plantSeconds: 1f,
                defuseSeconds: 2f,
                fuseSeconds: 5f,
                winsToWin: winsToWin,
                preparationSeconds: preparationSeconds,
                swapEveryRounds: swapEveryRounds,
                bombPickupRadius: 1.5f,
                defuseRadius: 2f);
        }

        private static NetMatchSettings Settings(SearchAndDestroySpec spec, float respawnSeconds = 3f, float restartDelaySeconds = 10f)
        {
            return new NetMatchSettings(
                respawnSeconds: respawnSeconds,
                restartDelaySeconds: restartDelaySeconds,
                searchAndDestroy: spec);
        }

        private static GameRules Rules(MatchObjective objective)
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(objective);
        }

        private static IMatchObjectiveRules CreateObjective(MatchObjective objective, NetMatchSettings settings, IMatchObjectiveHost host)
        {
            return ServerObjectiveFactory.Create(objective, settings, host);
        }

        private void Start(
            SearchAndDestroySpec spec,
            float respawnSeconds = 3f,
            float restartDelaySeconds = 10f,
            int controlPoints = 2,
            MatchObjective objective = MatchObjective.SearchAndDestroy)
        {
            var settings = Settings(spec, respawnSeconds, restartDelaySeconds);
            var events = new ServerEventWriter(true, _json, _log);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, events, () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points(controlPoints)),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Rules(objective), settings));
            var module = new MatchRulesModule(CreateObjective);
            module.Start(_context);
            _module = module;
        }

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Place(int id, Vector3 position)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            body.SetPose(position, 0f, PlayerStance.Standing);
        }

        private MatchSnapshot Match()
        {
            var snapshot = default(MatchSnapshot);
            _module.FillMatch(ref snapshot);
            return snapshot;
        }

        private List<ObjectiveItemSnapshot> Items()
        {
            var items = new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
            _module.FillObjectiveItems(items);
            var copy = new List<ObjectiveItemSnapshot>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                copy.Add(items[i]);
            }

            return copy;
        }

        private PlayerSnapshot Row(int id)
        {
            var row = default(PlayerSnapshot);
            Assert.That(_module.FillPlayer(id, ref row), Is.True);
            return row;
        }

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
            }
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }

        // Session を作る前の目的だけを確かめるので、Session と体は持たない
        private sealed class NullHost : IMatchObjectiveHost
        {
            public NullHost(TextWriter log)
            {
                Log = log;
            }

            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }
        }
    }
}
