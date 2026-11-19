using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientBattleRoyaleStatusTests
    {
        // 分隊 s の n 人目 (0 始まり) は 10 * s + n。自分は分隊 0 の 0。
        private const int _self = 0;
        private const int _squadmate = 1;
        private const int _oneA = 10;
        private const int _oneB = 11;
        private const int _twoA = 20;
        private const int _twoB = 21;
        private const int _tickRate = 10;

        private static readonly Side _squadZero = new Side(0);
        private static readonly Side _squadOne = new Side(1);
        private static readonly Side _squadTwo = new Side(2);
        private static readonly Side[] _expectedSquads = { _squadZero, _squadOne, _squadTwo };

        private ClientBattleRoyaleStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientBattleRoyaleStatus(_self, _tickRate);
        }

        [Test]
        public void TheZoneIsTheSameAsTheOfflineSchedule()
        {
            var schedule = Schedule();
            var waiting = schedule.Evaluate(10f);
            var shrinking = schedule.Evaluate(40f);
            var closed = schedule.Evaluate(100f);
            Assert.That(waiting.Stage, Is.EqualTo(ZoneStage.Waiting));
            Assert.That(shrinking.Stage, Is.EqualTo(ZoneStage.Shrinking));
            Assert.That(closed.Stage, Is.EqualTo(ZoneStage.Closed));

            _status.Apply(Snapshot(100, zone: waiting));
            AssertSameZone(_status.BattleRoyale.Zone, waiting);

            _status.Apply(Snapshot(400, zone: shrinking));
            AssertSameZone(_status.BattleRoyale.Zone, shrinking);

            _status.Apply(Snapshot(1000, zone: closed));
            AssertSameZone(_status.BattleRoyale.Zone, closed);
        }

        [Test]
        public void ARestartedWaitInTheSamePhaseIsReadAsShrinking()
        {
            var zone = new ClientZone();
            var match = Snapshot(1, zone: Schedule().Evaluate(29f)).Match;
            zone.Apply(match);
            Assert.That(zone.State.Value.Stage, Is.EqualTo(ZoneStage.Waiting));

            // 待ちが終わり、縮み始めた瞬間は円がまだ動かないが、残りの秒が縮む長さに戻る
            match.ZonePhaseRemainingSeconds = 20;
            Assert.That(zone.Apply(match), Is.True);

            Assert.That(zone.State.Value.Stage, Is.EqualTo(ZoneStage.Shrinking));
            Assert.That(zone.State.Value.SecondsUntilShrink, Is.Zero);
            Assert.That(zone.State.Value.SecondsUntilStageEnds, Is.EqualTo(20f));
        }

        [Test]
        public void BeforeTheZoneStartsThereIsNoZone()
        {
            _status.Apply(Snapshot(1));

            Assert.That(_status.BattleRoyale.Zone, Is.Null);
        }

        [Test]
        public void TheSquadsAreTheSameAsTheOfflineSession()
        {
            var offline = Offline();
            offline.Start();
            offline.ReportLethal(_squadmate, _oneA);
            offline.ReportLethal(_twoA, _self);
            offline.ReportLethal(_twoB, _self);
            offline.Tick(5f);
            var expected = new BattleRoyaleStatus(offline, _self, null, null, false);

            var snapshot = Snapshot(100, squadsRemaining: 2);
            Down(snapshot, _squadmate, (int)Math.Ceiling(offline.GetBleedoutRemaining(_squadmate).Value));
            Kill(snapshot, _twoA);
            Kill(snapshot, _twoB);
            var changed = _status.Apply(snapshot);

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status.Detail, Is.InstanceOf<BattleRoyaleStatus>(), "the HUD formatter registered for BattleRoyaleStatus reads it");
            Assert.That(status.OwnSide, Is.EqualTo((Side?)_squadZero));
            Assert.That(status.RemainingSeconds, Is.Null, "no time limit, like the offline default");
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
            var game = _status.BattleRoyale.Game;
            var view = (IBattleRoyaleView)expected.Game;
            Assert.That(game.State, Is.EqualTo(view.State));
            Assert.That(game.HasTimeLimit, Is.EqualTo(view.HasTimeLimit));
            Assert.That(game.Sides, Is.EqualTo(view.Sides));
            Assert.That(game.Sides, Is.EqualTo(_expectedSquads));
            Assert.That(game.EntrantCount, Is.EqualTo(view.EntrantCount));
            Assert.That(game.SquadsRemaining, Is.EqualTo(view.SquadsRemaining));
            foreach (var squad in _expectedSquads)
            {
                Assert.That(game.GetPlacement(squad), Is.EqualTo(view.GetPlacement(squad)), "placement of " + squad);
                Assert.That(game.CountMembers(squad), Is.EqualTo(view.CountMembers(squad)), "members of " + squad);
                Assert.That(game.CountRemaining(squad), Is.EqualTo(view.CountRemaining(squad)), "remaining of " + squad);
                Assert.That(game.CountStanding(squad), Is.EqualTo(view.CountStanding(squad)), "standing of " + squad);
                Assert.That(game.CountDowned(squad), Is.EqualTo(view.CountDowned(squad)), "downed of " + squad);
            }

            Assert.That(game.GetPlacement(_squadTwo), Is.EqualTo((int?)3));
            Assert.That(game.HasPlayer(_squadmate), Is.True);
            Assert.That(game.GetSide(_squadmate), Is.EqualTo(view.GetSide(_squadmate)));
            Assert.That(game.IsDowned(_squadmate), Is.EqualTo(view.IsDowned(_squadmate)));
            Assert.That(game.IsStanding(_self), Is.EqualTo(view.IsStanding(_self)));
            Assert.That(game.IsStanding(_squadmate), Is.EqualTo(view.IsStanding(_squadmate)));
            Assert.That(game.GetBleedoutRemaining(_squadmate), Is.EqualTo(view.GetBleedoutRemaining(_squadmate)));
            Assert.That(game.GetReviveProgress(_squadmate), Is.EqualTo(view.GetReviveProgress(_squadmate)));
            Assert.That(game.GetBleedoutRemaining(_self), Is.Null);
            Assert.That(_status.BattleRoyale.LocalPlayerId, Is.EqualTo((int?)_self));
            Assert.That(_status.BattleRoyale.ReviveTargetId, Is.Null, "who revives whom is not sent");
        }

        [Test]
        public void TheEndRanksTheSquadsLikeTheOfflineSession()
        {
            var offline = Offline();
            offline.Start();
            offline.ReportLethal(_twoA, _self);
            offline.ReportLethal(_twoB, _self);
            offline.Tick(0f);
            offline.ReportLethal(_oneA, _self);
            offline.ReportLethal(_oneB, _self);
            offline.Tick(0f);
            var view = (IBattleRoyaleView)offline;
            Assert.That(view.State, Is.EqualTo(GameModeState.Ended));

            var two = Snapshot(100, squadsRemaining: 2);
            Kill(two, _twoA);
            Kill(two, _twoB);
            _status.Apply(two);
            var one = Snapshot(101, squadsRemaining: 1);
            Kill(one, _twoA);
            Kill(one, _twoB);
            Kill(one, _oneA);
            Kill(one, _oneB);
            _status.Apply(one);
            var end = Snapshot(102, squadsRemaining: 1, state: GameModeState.Ended);
            Kill(end, _twoA);
            Kill(end, _twoB);
            Kill(end, _oneA);
            Kill(end, _oneB);
            _status.Apply(end);

            var game = _status.BattleRoyale.Game;
            Assert.That(game.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(game.Winner, Is.EqualTo(view.Winner));
            Assert.That(game.Winner, Is.EqualTo((Side?)_squadZero));
            foreach (var squad in _expectedSquads)
            {
                Assert.That(game.GetPlacement(squad), Is.EqualTo(view.GetPlacement(squad)), "placement of " + squad);
            }

            Assert.That(_status.Status.IsEnded, Is.True);
            Assert.That(_status.Status.Winner, Is.EqualTo((Side?)_squadZero));
        }

        [Test]
        public void ATimeUpWithEqualSquadsHasNoWinner()
        {
            _status.Apply(Snapshot(100, squadsRemaining: 3, remaining: 5));
            var end = Snapshot(101, squadsRemaining: 3, state: GameModeState.Ended);
            Kill(end, _twoB);

            _status.Apply(end);

            var game = _status.BattleRoyale.Game;
            Assert.That(game.Winner, Is.Null, "squads zero and one both have two standing");
            Assert.That(game.GetPlacement(_squadZero), Is.EqualTo((int?)1));
            Assert.That(game.GetPlacement(_squadOne), Is.EqualTo((int?)1));
            Assert.That(game.GetPlacement(_squadTwo), Is.EqualTo((int?)3));
        }

        [Test]
        public void ASquadIsNotPlacedBeforeTheServerCountsItOut()
        {
            var snapshot = Snapshot(100, squadsRemaining: 3);
            Kill(snapshot, _twoA);
            Kill(snapshot, _twoB);

            _status.Apply(snapshot);

            Assert.That(_status.BattleRoyale.Game.GetPlacement(_squadTwo), Is.Null);
        }

        [Test]
        public void TheOwnersRedeployIsShownLikeOffline()
        {
            var waiting = Snapshot(100);
            Kill(waiting, _self);
            waiting.HasOwner = true;
            waiting.Owner.RedeploySeconds = 7;
            waiting.Owner.RedeploysLeft = 1;

            _status.Apply(waiting);

            Assert.That(_status.Status.RespawnSeconds, Is.EqualTo(7f));
            Assert.That(_status.BattleRoyale.LocalRedeploySeconds, Is.EqualTo(7f));
            Assert.That(_status.BattleRoyale.LocalRedeploysLeft, Is.EqualTo((int?)1));

            var standing = Snapshot(101);
            standing.HasOwner = true;
            standing.Owner.RedeploysLeft = 2;
            _status.Apply(standing);

            Assert.That(_status.Status.RespawnSeconds, Is.Null);
            Assert.That(_status.BattleRoyale.LocalRedeploySeconds, Is.Null);
            Assert.That(_status.BattleRoyale.LocalRedeploysLeft, Is.EqualTo((int?)2));

            var spent = Snapshot(102);
            spent.HasOwner = true;
            spent.Owner.RedeploysLeft = 0;
            _status.Apply(spent);

            Assert.That(_status.BattleRoyale.LocalRedeploysLeft, Is.Null, "no redeploys left and none being waited for");
        }

        [Test]
        public void AnOwnerWithoutWeaponsIsUnarmed()
        {
            var snapshot = Snapshot(100);
            snapshot.HasOwner = true;

            _status.Apply(snapshot);

            Assert.That(_status.BattleRoyale.IsLocalPlayerUnarmed, Is.True);
        }

        [Test]
        public void SupplyCratesAreNumberedInTheOrderTheyFirstAppear()
        {
            var snapshot = Snapshot(100);
            AddCrate(snapshot, 5, new Vector3(1f, 0f, 0f), ObjectiveItemState.Home, opened: false);
            AddCrate(snapshot, 3, new Vector3(50f, 30f, 0f), ObjectiveItemState.Dropped, opened: false);
            AddCrate(snapshot, 4, new Vector3(9f, 0f, 9f), ObjectiveItemState.Home, opened: true);

            _status.Apply(snapshot, "E");

            var detail = _status.BattleRoyale;
            Assert.That(detail.SupplyDrops, Has.Count.EqualTo(2), "an opened crate is not listed");
            Assert.That(detail.SupplyDrops[0].Number, Is.EqualTo(1));
            Assert.That(detail.SupplyDrops[0].HasLanded, Is.True);
            Assert.That(detail.SupplyDrops[0].Position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(detail.SupplyDrops[1].Number, Is.EqualTo(2));
            Assert.That(detail.SupplyDrops[1].HasLanded, Is.False);
            Assert.That(detail.LandedSupplyDrops, Is.EqualTo(1));
            Assert.That(detail.FallingSupplyDrops, Is.EqualTo(1));
            Assert.That(detail.IsLocalPlayerAtSupplyDrop, Is.True, "the landed crate is within its radius");
            Assert.That(detail.InteractKeyLabel, Is.EqualTo("E"));
        }

        [Test]
        public void TheClockStopsWhenTheMatchEnds()
        {
            _status.Apply(Snapshot(100));
            _status.Apply(Snapshot(125, state: GameModeState.Ended));
            var changed = _status.Apply(Snapshot(200, state: GameModeState.Ended));

            Assert.That(changed, Is.False);
            Assert.That(_status.BattleRoyale.Game.ElapsedSeconds, Is.EqualTo(2d), "stopped at the first Ended snapshot");
        }

        [Test]
        public void TheClockCountsUpWithoutATimeLimit()
        {
            _status.Apply(Snapshot(100));
            _status.Apply(Snapshot(125));

            Assert.That(_status.BattleRoyale.Game.HasTimeLimit, Is.False);
            Assert.That(_status.BattleRoyale.Game.ElapsedSeconds, Is.EqualTo(2d), "25 ticks at 10 per second, in whole seconds");
        }

        [Test]
        public void ATimeLimitIsShownAsTheRemainingSeconds()
        {
            _status.Apply(Snapshot(100, remaining: 90));

            Assert.That(_status.Status.RemainingSeconds, Is.EqualTo(90f));
            Assert.That(_status.BattleRoyale.Game.HasTimeLimit, Is.True);
            Assert.That(_status.BattleRoyale.Game.RemainingSeconds, Is.EqualTo(90f));
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(100));
            var shown = _status.Status;

            Assert.That(_status.Apply(Snapshot(101)), Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(100));
            var snapshot = Snapshot(101);
            snapshot.Match.Objective = MatchObjective.Payload;

            Assert.That(_status.Apply(snapshot), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Apply(snapshot), Is.False, "already cleared");
        }

        [Test]
        public void ANewMatchAfterTheEndForgetsThePlacements()
        {
            var two = Snapshot(100, squadsRemaining: 2);
            Kill(two, _twoA);
            Kill(two, _twoB);
            _status.Apply(two);
            _status.Apply(Snapshot(101, squadsRemaining: 2, state: GameModeState.Ended));

            _status.Apply(Snapshot(200, squadsRemaining: 3));

            Assert.That(_status.BattleRoyale.Game.GetPlacement(_squadTwo), Is.Null);
            Assert.That(_status.BattleRoyale.Game.Winner, Is.Null);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientBattleRoyaleStatus(_self, 0));
        }

        private static BattleRoyaleSession Offline()
        {
            var br = new BattleRoyaleSession(3, 2, float.PositiveInfinity, ActionRules.Default.WithDowned(Downed.Revive));
            br.Mode.AddPlayer(_self, _squadZero);
            br.Mode.AddPlayer(_squadmate, _squadZero);
            br.Mode.AddPlayer(_oneA, _squadOne);
            br.Mode.AddPlayer(_oneB, _squadOne);
            br.Mode.AddPlayer(_twoA, _squadTwo);
            br.Mode.AddPlayer(_twoB, _squadTwo);
            return br;
        }

        private static ZoneSchedule Schedule()
        {
            return new ZoneSchedule(
                new Vector2(0f, 0f),
                100f,
                new[] { new ZonePhase(30f, 20f, 50f, 5f), new ZonePhase(10f, 10f, 20f, 10f) },
                7);
        }

        private static void AssertSameZone(ZoneState? actual, ZoneState expected)
        {
            Assert.That(actual.HasValue, Is.True);
            var zone = actual.Value;
            Assert.That(zone.PhaseIndex, Is.EqualTo(expected.PhaseIndex));
            Assert.That(zone.Stage, Is.EqualTo(expected.Stage));
            Assert.That(zone.Center, Is.EqualTo(expected.Center));
            Assert.That(zone.Radius, Is.EqualTo(expected.Radius));
            Assert.That(zone.TargetCenter, Is.EqualTo(expected.TargetCenter));
            Assert.That(zone.TargetRadius, Is.EqualTo(expected.TargetRadius));
            Assert.That(zone.SecondsUntilShrink, Is.EqualTo(expected.SecondsUntilShrink));
            Assert.That(zone.SecondsUntilStageEnds, Is.EqualTo(expected.SecondsUntilStageEnds));
        }

        private static SnapshotMessage Snapshot(
            int tick,
            int squadsRemaining = 3,
            int remaining = 0,
            GameModeState state = GameModeState.Playing,
            ZoneState? zone = null)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.Objective = MatchObjective.BattleRoyale;
            match.RemainingSeconds = remaining;
            match.SquadsRemaining = squadsRemaining;
            if (zone.HasValue)
            {
                var value = zone.Value;
                match.ZonePhase = value.PhaseIndex + 1;
                match.ZoneCenter = value.Center;
                match.ZoneRadius = value.Radius;
                match.NextZoneCenter = value.TargetCenter;
                match.NextZoneRadius = value.TargetRadius;
                match.ZonePhaseRemainingSeconds = (int)Math.Ceiling(value.SecondsUntilStageEnds);
            }

            AddPlayer(snapshot, _self, 0);
            AddPlayer(snapshot, _squadmate, 0);
            AddPlayer(snapshot, _oneA, 1);
            AddPlayer(snapshot, _oneB, 1);
            AddPlayer(snapshot, _twoA, 2);
            AddPlayer(snapshot, _twoB, 2);
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int id, byte squad)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = id;
            row.Team = Team.Alpha;
            row.SquadId = squad;
            row.Health = 100;
            row.WeaponId = "";
        }

        private static void Down(SnapshotMessage snapshot, int id, int bleedoutSeconds)
        {
            ref var row = ref snapshot.Players[snapshot.Players.IndexOf(id)];
            row.Downed = new DownedSync(true, bleedoutSeconds, 0);
        }

        private static void Kill(SnapshotMessage snapshot, int id)
        {
            ref var row = ref snapshot.Players[snapshot.Players.IndexOf(id)];
            row.Health = 0;
        }

        private static void AddCrate(SnapshotMessage snapshot, int id, Vector3 position, ObjectiveItemState state, bool opened)
        {
            ref var crate = ref snapshot.ObjectiveItems.Add();
            crate.ItemId = id;
            crate.Present = ObjectiveItemFields.All;
            crate.Kind = ObjectiveItemKind.SupplyCrate;
            crate.Position = position;
            crate.State = state;
            crate.Opened = opened;
            crate.Radius = 3f;
        }
    }
}
