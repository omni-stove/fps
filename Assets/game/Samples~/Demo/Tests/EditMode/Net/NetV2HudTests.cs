using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2HudTests
    {
        private const int _localId = 3;
        private const int _otherId = 7;
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";

        private readonly List<Object> _created = new List<Object>();
        private NetV2Hud _hud;

        [SetUp]
        public void SetUp()
        {
            ClearSignals();
            _hud = NetV2Hud.Create(_localId, 100, _ => null);
            _created.Add(_hud.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            ClearSignals();
        }

        // GameSignals.ResetAll はこのアセンブリから見えないので、使う項目を 1 つずつ消す。
        private static void ClearSignals()
        {
            GameSignals.HudModel.Clear();
            GameSignals.Participants.Clear();
            GameSignals.PauseTimeMode.Clear();
            GameSignals.HudDensity.Clear();
            GameSignals.PaletteViewer.Clear();
            GameSignals.Scoreboard.Clear();
            GameSignals.GrenadeCount.Clear();
            GameSignals.LocalBlindSeconds.Clear();
            GameSignals.Inventory.Clear();
            GameSignals.ModeStatus.Clear();
            GameSignals.Stash.Clear();
        }

        [Test]
        public void CreatePublishesTheModelTheParticipantsAndThatPauseKeepsRunning()
        {
            Assert.That(_hud.gameObject.name, Is.EqualTo(NetV2Hud.ObjectName));
            Assert.That(GameSignals.HudModel.Value, Is.SameAs(_hud.Model));
            Assert.That(GameSignals.Participants.Value, Is.SameAs(_hud.Participants));
            Assert.That(GameSignals.PauseTimeMode.Value, Is.EqualTo((PauseTimeMode?)PauseTimeMode.KeepRunning));
            Assert.Throws<ArgumentNullException>(() => NetV2Hud.Create(_localId, 100, null));
        }

        [Test]
        public void ASnapshotIsWrittenToTheModelAndTheRulesAndTeamArePublished()
        {
            var snapshot = Snapshot(MatchObjective.None);
            snapshot.Match.AlphaScore = 4;
            snapshot.Match.BravoScore = 6;
            snapshot.Match.RemainingSeconds = 75;
            var rules = snapshot.Match.Rules;
            rules.HudDensity = HudDensity.Minimal;
            snapshot.Match.Rules = rules;

            _hud.Apply(snapshot);

            var model = _hud.Model;
            Assert.That(model.CurrentHealth, Is.EqualTo(80));
            Assert.That(model.MaxHealth, Is.EqualTo(100));
            Assert.That(model.FirstScoreTeam, Is.EqualTo((Team?)Team.Bravo), "the own team's score comes first");
            Assert.That(model.FirstScore, Is.EqualTo(6));
            Assert.That(model.SecondScore, Is.EqualTo(4));
            Assert.That(model.RemainingSeconds, Is.EqualTo(75));
            Assert.That(model.HasResult, Is.False);
            Assert.That(GameSignals.HudDensity.Value, Is.EqualTo((HudDensity?)HudDensity.Minimal));
            Assert.That(GameSignals.PaletteViewer.Value, Is.EqualTo((Team?)Team.Bravo));

            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Alpha;
            _hud.Apply(snapshot);

            Assert.That(model.HasResult, Is.True);
            Assert.That(model.Winner, Is.EqualTo((Team?)Team.Alpha));
        }

        [Test]
        public void TheScoreboardIsPublishedAsANewListOnlyWhenItChanges()
        {
            var snapshot = Snapshot(MatchObjective.None);

            _hud.Apply(snapshot);
            var rows = GameSignals.Scoreboard.Value;
            _hud.Apply(snapshot);

            Assert.That(GameSignals.Scoreboard.Value, Is.SameAs(rows), "the same rows are not published again every tick");
            Assert.That(rows, Is.EqualTo(new[]
            {
                new ScoreboardEntry(_localId, BotRoster.ScoreboardName(_localId), Team.Bravo, 2, 1, 0, ScoreboardConnection.Connected, true),
                new ScoreboardEntry(_otherId, BotRoster.ScoreboardName(_otherId), Team.Alpha, 0, 0, 0, ScoreboardConnection.Connected, false),
            }));

            snapshot.Players[1].Kills = 1;
            _hud.Apply(snapshot);

            var changed = GameSignals.Scoreboard.Value;
            Assert.That(changed, Is.Not.SameAs(rows), "a new list, so the one already handed out does not change");
            Assert.That(rows[1].Kills, Is.Zero);
            Assert.That(changed[1].Kills, Is.EqualTo(1));
        }

        [Test]
        public void TheScoreboardShowsTheAssistsOfEachRowAndRepublishesWhenTheyChange()
        {
            var snapshot = Snapshot(MatchObjective.None);
            snapshot.Players[0].Assists = 3;

            _hud.Apply(snapshot);
            var rows = GameSignals.Scoreboard.Value;

            Assert.That(rows[0].Assists, Is.EqualTo(3));
            Assert.That(rows[1].Assists, Is.Zero);

            snapshot.Players[1].Assists = 1;
            _hud.Apply(snapshot);

            var changed = GameSignals.Scoreboard.Value;
            Assert.That(changed, Is.Not.SameAs(rows), "an assist alone changes the rows");
            Assert.That(changed[0].Assists, Is.EqualTo(3));
            Assert.That(changed[1].Assists, Is.EqualTo(1));
            Assert.That(rows[1].Assists, Is.Zero);
        }

        [Test]
        public void FreeForAllDoesNotShowThePointsPerKillAndLimitAsTeamScores()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll);
            snapshot.Match.AlphaPoints = 1;
            snapshot.Match.BravoPoints = 15;

            _hud.Apply(snapshot);

            Assert.That(_hud.Model.FirstScore, Is.Zero);
            Assert.That(_hud.Model.SecondScore, Is.Zero);
        }

        [Test]
        public void FreeForAllPublishesTheStandingOnlyWhenTheShownValuesChange()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll);
            snapshot.Match.AlphaPoints = 1;
            snapshot.Match.RemainingSeconds = 120;

            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.OwnSide, Is.EqualTo((Side?)new Side(_localId)));
            Assert.That(status.Detail, Is.EqualTo(new FreeForAllStanding(1, 2, 2, 2)), "2 kills against 0 lead by 2");

            snapshot.Tick++;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(status), "an unchanged standing is not published again");

            snapshot.Players[1].Kills = 3;
            _hud.Apply(snapshot);
            var changed = GameSignals.ModeStatus.Value;
            Assert.That(changed, Is.Not.SameAs(status));
            Assert.That(changed.Detail, Is.EqualTo(new FreeForAllStanding(2, 2, 2, -1)));

            snapshot.Match.Objective = MatchObjective.None;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "a match without a mode takes the status back");
        }

        [Test]
        public void KillConfirmedPublishesTheConfirmedCountsAsTheModeStatus()
        {
            var snapshot = Snapshot(MatchObjective.KillConfirmed);
            snapshot.Tick = 10;
            snapshot.Match.AlphaScore = 3;
            snapshot.Match.BravoScore = 5;
            snapshot.Match.AlphaPoints = 3;
            snapshot.Match.BravoPoints = 5;

            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<KillConfirmed>());
            Assert.That(((KillConfirmed)status.Detail).GetConfirmed(Side.Bravo), Is.EqualTo(5));
            Assert.That(_hud.Model.FirstScore, Is.EqualTo(5), "the own team's confirmed count comes first");
        }

        [Test]
        public void HardpointPublishesTheMirroredHillAsTheModeStatusUntilTheModeEnds()
        {
            var snapshot = Snapshot(MatchObjective.Hardpoint);
            snapshot.Match.AlphaPoints = 12;
            snapshot.Match.BravoPoints = 30;
            snapshot.Match.RemainingSeconds = 240;
            snapshot.Match.HasActivePoint = true;
            snapshot.Match.ActivePointIndex = 1;
            snapshot.Match.ObjectiveRemainingSeconds = 17;
            AddPoint(snapshot, 0, null, 0, false);
            AddPoint(snapshot, 1, Team.Bravo, -100, false);
            AddPoint(snapshot, 2, null, 0, false);

            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<Hardpoint>(), "the same detail type as offline, so the Hardpoint formatter reads it");
            var hardpoint = (Hardpoint)status.Detail;
            Assert.That(hardpoint.PointCount, Is.EqualTo(3));
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(17f));
            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Held));
            Assert.That(hardpoint.Holder, Is.EqualTo(Side.Bravo));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(status.RemainingSeconds, Is.EqualTo(240f));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(12d));
            Assert.That(status.SideScores[1].Score, Is.EqualTo(30d));
            Assert.That(status.Detail, Is.Not.InstanceOf<DominationStatus>(), "the hill's points are not domination points");
            Assert.That(_hud.Model.FirstScore, Is.EqualTo(30), "the HUD's team scores are the hardpoint points");
            Assert.That(_hud.Model.SecondScore, Is.EqualTo(12));

            snapshot.Tick++;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(status), "an unchanged hill is not published again");

            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;
            _hud.Apply(snapshot);
            var ended = GameSignals.ModeStatus.Value;
            Assert.That(ended, Is.Not.SameAs(status));
            Assert.That(ended.IsEnded, Is.True);
            Assert.That(ended.Winner, Is.EqualTo((Side?)Side.Bravo), "the server's winner is shown");

            snapshot.Match.Objective = MatchObjective.None;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "a match without a mode takes the status back");
        }

        [Test]
        public void ClearSignalsTakesBackTheHardpointStatus()
        {
            var snapshot = Snapshot(MatchObjective.Hardpoint);
            snapshot.Match.HasActivePoint = true;
            snapshot.Match.ActivePointIndex = 0;
            snapshot.Match.ObjectiveRemainingSeconds = 20;
            AddPoint(snapshot, 0, Team.Alpha, 100, false);
            AddPoint(snapshot, 1, null, 0, false);
            AddPoint(snapshot, 2, null, 0, false);
            _hud.Apply(snapshot);
            var shown = GameSignals.ModeStatus.Value;
            Assert.That(shown, Is.Not.Null);

            _hud.ClearSignals();
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);

            _hud.Apply(snapshot);
            var again = GameSignals.ModeStatus.Value;
            Assert.That(again, Is.Not.Null, "the next snapshot after clearing publishes the hill again");
            Assert.That(again, Is.Not.SameAs(shown));
        }

        [Test]
        public void GunGameShowsTheTiersOnlyWithALocalLadder()
        {
            var snapshot = Snapshot(MatchObjective.GunGame);
            snapshot.Players[0].GunGameTier = 1;

            _hud.Apply(snapshot);

            var ladder = GameContent.Pick(c => c.GunGameLadder);
            if (ladder == null || ladder.Tiers.Count == 0)
            {
                Assert.That(GameSignals.ModeStatus.Value, Is.Null);
            }
            else
            {
                Assert.That(GameSignals.ModeStatus.Value.Detail, Is.InstanceOf<GunGameStatus>());
            }
        }

        [Test]
        public void PayloadPublishesTheCartWithTheScenesCheckpointsUntilTheModeEnds()
        {
            AddCart(2);
            var snapshot = PayloadSnapshot(Team.Alpha, 0.4f, 1, 2);
            snapshot.Match.RemainingSeconds = 240;

            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<PayloadStatus>(), "the same detail type as offline, so the Payload formatter reads it");
            var payload = (PayloadStatus)status.Detail;
            Assert.That(payload.Attackers, Is.EqualTo(Side.Alpha));
            Assert.That(payload.Progress, Is.EqualTo(0.4f));
            Assert.That(payload.CheckpointsReached, Is.EqualTo(1));
            Assert.That(payload.CheckpointCount, Is.EqualTo(2), "the count comes from the scene's cart");
            Assert.That(payload.Pushers, Is.EqualTo(2));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(status.RemainingSeconds, Is.EqualTo(240f));
            Assert.That(status.Detail, Is.Not.InstanceOf<DominationStatus>());

            snapshot.Tick++;
            _hud.Apply(snapshot);
            var held = GameSignals.ModeStatus.Value;
            Assert.That(held, Is.Not.SameAs(status), "pushers that do not move the cart change the status");
            Assert.That(((PayloadStatus)held.Detail).IsContested, Is.True);

            snapshot.Tick++;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(held), "an unchanged cart is not published again");

            snapshot.Tick++;
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;
            _hud.Apply(snapshot);
            var ended = GameSignals.ModeStatus.Value;
            Assert.That(ended.IsEnded, Is.True);
            Assert.That(ended.Winner, Is.EqualTo((Side?)Side.Bravo), "the server's winner is shown");

            snapshot.Match.Objective = MatchObjective.None;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "a match without a mode takes the status back");
        }

        [Test]
        public void PayloadWithoutACartInTheSceneShowsNoStatus()
        {
            LogAssert.Expect(LogType.Warning, ClientPayloadLog.CartMissing());

            _hud.Apply(PayloadSnapshot(Team.Alpha, 0.4f, 1, 1));

            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "the checkpoint count is not known without the cart");

            var snapshot = PayloadSnapshot(Team.Alpha, 0.5f, 1, 1);
            snapshot.Tick = 1;
            _hud.Apply(snapshot);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ClearSignalsTakesBackThePayloadStatus()
        {
            AddCart(1);
            var snapshot = PayloadSnapshot(Team.Bravo, 0.2f, 0, 1);
            _hud.Apply(snapshot);
            var shown = GameSignals.ModeStatus.Value;
            Assert.That(shown, Is.Not.Null);

            _hud.ClearSignals();
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);

            _hud.Apply(snapshot);
            var again = GameSignals.ModeStatus.Value;
            Assert.That(again, Is.Not.Null, "the next snapshot after clearing publishes the cart again");
            Assert.That(again, Is.Not.SameAs(shown));
            Assert.That(((PayloadStatus)again.Detail).Attackers, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void TheModeKeyChangesWithTheShownValuesOnly()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll);
            var key = NetV2Hud.PerSnapshotKey(snapshot);

            snapshot.Tick++;
            snapshot.Players[0].Health = 10;
            Assert.That(NetV2Hud.PerSnapshotKey(snapshot), Is.EqualTo(key), "health and the tick are not shown in the mode status");

            snapshot.Players[0].GunGameTier = 1;
            Assert.That(NetV2Hud.PerSnapshotKey(snapshot), Is.Not.EqualTo(key));
        }

        [Test]
        public void TheModeKeyChangesWithTheOwnRespawnCountdown()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll);
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = null;
            var alive = NetV2Hud.PerSnapshotKey(snapshot);

            snapshot.Owner.RespawnSeconds = 0;
            var zero = NetV2Hud.PerSnapshotKey(snapshot);
            snapshot.Owner.RespawnSeconds = 3;
            var three = NetV2Hud.PerSnapshotKey(snapshot);

            Assert.That(zero, Is.Not.EqualTo(alive), "a countdown at 0 is not the same as no countdown");
            Assert.That(three, Is.Not.EqualTo(zero));
        }

        [Test]
        public void FreeForAllPublishesTheOwnRespawnCountdown()
        {
            var snapshot = Snapshot(MatchObjective.FreeForAll);
            snapshot.Match.AlphaPoints = 1;
            snapshot.HasOwner = true;
            snapshot.Owner.RespawnSeconds = 5;

            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value.RespawnSeconds, Is.EqualTo(5f));

            snapshot.Tick++;
            snapshot.Owner.RespawnSeconds = 4;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value.RespawnSeconds, Is.EqualTo(4f), "the countdown is published again when it ticks");

            snapshot.Tick++;
            snapshot.Owner.RespawnSeconds = null;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value.RespawnSeconds, Is.Null, "the countdown goes away after respawning");
        }

        [Test]
        public void DominationPublishesThePointsInOrderUntilOneIsMissing()
        {
            var snapshot = Snapshot(MatchObjective.Domination);
            snapshot.Match.AlphaPoints = 30;
            snapshot.Match.BravoPoints = 12;
            snapshot.Match.RemainingSeconds = 90;
            AddPoint(snapshot, 0, Team.Alpha, 100, false);
            AddPoint(snapshot, 1, null, -40, true);
            AddPoint(snapshot, 3, Team.Bravo, -100, false);

            _hud.Apply(snapshot);

            var modeStatus = GameSignals.ModeStatus.Value;
            Assert.That(modeStatus, Is.Not.Null);
            Assert.That(modeStatus.OwnSide, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(modeStatus.RemainingSeconds, Is.EqualTo(90f));
            Assert.That(modeStatus.SideScores, Is.EqualTo(new[] { new SideScore(Side.Alpha, 30d), new SideScore(Side.Bravo, 12d) }));
            Assert.That(modeStatus.IsEnded, Is.False);
            var status = modeStatus.Detail as DominationStatus;
            Assert.That(status, Is.Not.Null, "the points ride on ModeStatus.Detail like the offline DominationRunner");
            Assert.That(status.AlphaScore, Is.EqualTo(30f));
            Assert.That(status.BravoScore, Is.EqualTo(12f));
            Assert.That(status.RemainingSeconds, Is.EqualTo(90f));
            Assert.That(status.Points, Is.EqualTo(new[] { new ControlPointStatus(Team.Alpha, 100, false), new ControlPointStatus(null, -40, true) }),
                "point 2 is missing, so point 3 is not shown under the name C");
            Assert.That(_hud.Model.FirstScore, Is.EqualTo(12), "the HUD's team scores are the domination points");

            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(modeStatus), "an unchanged state is not published again");

            snapshot.Match.Objective = MatchObjective.None;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
        }

        [Test]
        public void GrenadesArePublishedWithTheTypeOnlyWhenThereAreSeveral()
        {
            var pouch = new GrenadePouchSync { Enabled = true };
            pouch.Add("frag", 2);

            _hud.ShowGrenades(pouch, "frag");
            Assert.That(GameSignals.GrenadeCount.Value, Is.EqualTo((GrenadeCountStatus?)new GrenadeCountStatus(true, 2, null)));

            pouch.Add("smoke", 1);
            _hud.ShowGrenades(pouch, "smoke");
            Assert.That(GameSignals.GrenadeCount.Value, Is.EqualTo((GrenadeCountStatus?)new GrenadeCountStatus(true, 1, "smoke")));

            var heard = 0;
            Action<GrenadeCountStatus?> listen = _ => heard++;
            GameSignals.GrenadeCount.Changed += listen;
            try
            {
                _hud.ShowGrenades(pouch, "smoke");
                _hud.ShowGrenades(null, "smoke");
            }
            finally
            {
                GameSignals.GrenadeCount.Changed -= listen;
            }

            Assert.That(heard, Is.Zero, "nothing changed, and no pouch keeps what is shown");
        }

        [Test]
        public void TheParticipantsNameEveryIdAndKeepTheTeamsOfThoseInTheSnapshot()
        {
            _hud.Apply(Snapshot(MatchObjective.None));
            var participants = GameSignals.Participants.Value;

            Assert.That(participants.TryGet(_localId, out var local), Is.True);
            Assert.That(local, Is.EqualTo(new Participant(_localId, BotRoster.KillFeedName(_localId), Team.Bravo, true)));
            Assert.That(participants.TryGet(42, out var gone), Is.True, "someone who left still has a name");
            Assert.That(gone, Is.EqualTo(new Participant(42, BotRoster.KillFeedName(42), null, false)));
        }

        [Test]
        public void TheParticipantsTakeTheNamesFromTheServerRoster()
        {
            var names = new ClientPlayerNames();
            var roster = new PlayerNamesMessage();
            roster.Add(_localId, "Alice");
            roster.Add(42, "Bob");
            names.Apply(roster);
            var hud = NetV2Hud.Create(_localId, 100, _ => null, names: names);
            _created.Add(hud.gameObject);

            hud.Apply(Snapshot(MatchObjective.None));
            var participants = hud.Participants;

            Assert.That(participants.TryGet(_localId, out var local), Is.True);
            Assert.That(local, Is.EqualTo(new Participant(_localId, "Alice", Team.Bravo, true)));
            Assert.That(participants.TryGet(42, out var gone), Is.True);
            Assert.That(gone, Is.EqualTo(new Participant(42, "Bob", null, false)), "someone not in the snapshot keeps the roster name");
            Assert.That(participants.TryGet(_otherId, out var other), Is.True);
            Assert.That(other.Name, Is.EqualTo(BotRoster.KillFeedName(_otherId)), "someone missing from the roster keeps the id name");
        }

        [Test]
        public void ANameThatArrivesLaterIsShownFromTheNextSnapshot()
        {
            var names = new ClientPlayerNames();
            var hud = NetV2Hud.Create(_localId, 100, _ => null, names: names);
            _created.Add(hud.gameObject);
            hud.Apply(Snapshot(MatchObjective.None));
            var roster = new PlayerNamesMessage();
            roster.Add(_otherId, "Carol");

            names.Apply(roster);
            hud.Apply(Snapshot(MatchObjective.None));

            Assert.That(hud.Participants.TryGet(_otherId, out var other), Is.True);
            Assert.That(other.Name, Is.EqualTo("Carol"));
        }

        [Test]
        public void ClearSignalsTakesBackOnlyWhatItPublished()
        {
            var snapshot = Snapshot(MatchObjective.Domination);
            AddPoint(snapshot, 0, null, 0, false);
            _hud.Apply(snapshot);
            var pouch = new GrenadePouchSync { Enabled = true };
            pouch.Add("frag", 1);
            _hud.ShowGrenades(pouch, "frag");
            _hud.BindStatusEffects(new ClientStatusEffects());
            Assert.That(GameSignals.LocalBlindSeconds.Value, Is.Not.Null);
            var other = new HudModel();
            GameSignals.HudModel.Publish(other);

            _hud.ClearSignals();
            _hud.ClearSignals();

            Assert.That(GameSignals.HudModel.Value, Is.SameAs(other), "a model published by someone else stays");
            Assert.That(GameSignals.Participants.Value, Is.Null);
            Assert.That(GameSignals.PauseTimeMode.Value, Is.Null);
            Assert.That(GameSignals.HudDensity.Value, Is.Null);
            Assert.That(GameSignals.PaletteViewer.Value, Is.Null);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
            Assert.That(GameSignals.Scoreboard.Value, Is.Null);
            Assert.That(GameSignals.GrenadeCount.Value, Is.Null);
            Assert.That(GameSignals.LocalBlindSeconds.Value, Is.Null);
        }

        [Test]
        public void TheDefaultHudShowsTheNetModelAndTheControlPointRings()
        {
            var view = Hud.Create("NetV2HudTests.Hud");
            _created.Add(view.gameObject);
            _created.Add(view.OwnedPanelSettings);
            var snapshot = Snapshot(MatchObjective.Domination);
            AddPoint(snapshot, 0, Team.Alpha, 100, false);
            AddPoint(snapshot, 1, null, 0, false);

            _hud.Apply(snapshot);

            Assert.That(view.ShownModel, Is.SameAs(_hud.Model));
            Assert.That(view.PauseMenu.TimeMode, Is.EqualTo(PauseTimeMode.KeepRunning), "the match does not stop while paused");
            Assert.That(view.FollowsTeamPalette, Is.True);
            Assert.That(view.TeamColor(Team.Bravo), Is.EqualTo(TeamColors.Of(TeamColors.Current, Team.Bravo, Team.Bravo)), "painted from the local team");
            var rings = view.GetComponent<ControlPointsView>();
            Assert.That(rings.ColorOf(Team.Alpha), Is.EqualTo(view.TeamColor(Team.Alpha)));

            // Edit Mode では UIDocument が木を作らないことがあり、そのときビューは何も受けない
            if (view.View != null)
            {
                Assert.That(view.View.HealthLabel.text, Is.EqualTo(HudNumberFormat.Health(_hud.Model)));
                Assert.That(rings.Statuses, Has.Count.EqualTo(2));
                Assert.That(rings.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }

            Assert.That(view.KillFeed.Names.Describe(new KillFeedEntry(_otherId, _localId, "", false, false)).Victim,
                Is.EqualTo(new KillFeedName(BotRoster.KillFeedName(_localId), Team.Bravo, true)));
        }

        [Test]
        public void BattleRoyalePublishesTheSquadsAndTheZoneInTheShapeOfOffline()
        {
            var snapshot = BattleRoyaleSnapshot(squadsRemaining: 2);

            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<BattleRoyaleStatus>(), "the same detail type as offline, so the Battle Royale formatter reads it");
            var detail = (BattleRoyaleStatus)status.Detail;
            Assert.That(detail.Session, Is.Null, "the session lives on the server");
            Assert.That(detail.Game.EntrantCount, Is.EqualTo(2));
            Assert.That(detail.Game.SquadsRemaining, Is.EqualTo(2));
            Assert.That(detail.LocalPlayerId, Is.EqualTo((int?)_localId));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)new Side(0)), "the squad id is the side");
            Assert.That(detail.Zone.HasValue, Is.True);
            Assert.That(detail.Zone.Value.Stage, Is.EqualTo(ZoneStage.Waiting));
            Assert.That(detail.Zone.Value.SecondsUntilShrink, Is.EqualTo(12f));

            var formatter = new BattleRoyaleHudFormatter();
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(lines, Does.Contain(BattleRoyaleHudFormatter.SquadsLeft(2, 2)));
            Assert.That(lines, Does.Contain(BattleRoyaleHudFormatter.Placement(null, 2)));
            Assert.That(lines, Does.Contain(ZoneHudFormatter.Line(detail.Zone.Value)));
            Assert.That(lines, Does.Contain(BattleRoyaleHudFormatter.Squad(1, 0)));
            Assert.That(formatter.Scores(status), Has.Count.EqualTo(2));
            Assert.That(formatter.Result(status).IsEmpty, Is.True, "the match has not ended");

            _hud.Apply(BattleRoyaleSnapshot(squadsRemaining: 2));
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(status), "unchanged squads are not published again");

            _hud.Apply(Snapshot(MatchObjective.None));
            Assert.That(GameSignals.ModeStatus.Value, Is.Null, "a match without a mode takes the status back");
        }

        [Test]
        public void ExtractionPublishesTheRaidAndTheStash()
        {
            var stash = new StashStateMessage();
            stash.Stash.Add(_ammo, 10);
            stash.Carried.Add(_ammo, 20);
            _hud.Receive(stash);

            Assert.That(GameSignals.Stash.Value.HasValue, Is.True, "the stash panel reads the stash from the server");
            var published = GameSignals.Stash.Value.Value;
            Assert.That(published.Stacks, Has.Count.EqualTo(1));
            Assert.That(published.Stacks[0].ItemId, Is.EqualTo(_ammo));
            Assert.That(published.Stacks[0].Count, Is.EqualTo(10));
            Assert.That(published.Capacity, Is.EqualTo(ExtractionRunner.StashTuning.Capacity));

            var snapshot = Snapshot(MatchObjective.Extraction);
            snapshot.Match.RemainingSeconds = 300;
            ref var point = ref snapshot.ObjectiveItems.Add();
            point.ItemId = 1;
            point.Present = ObjectiveItemFields.All;
            point.Kind = ObjectiveItemKind.ExtractionPoint;
            point.State = ObjectiveItemState.Home;
            point.Opened = true;
            point.Radius = 3f;
            point.Position = new System.Numerics.Vector3(100f, 0f, 100f);
            _hud.Apply(snapshot);

            var status = GameSignals.ModeStatus.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<ExtractionStatus>(), "the same detail type as offline, so the Extraction formatter reads it");
            var detail = (ExtractionStatus)status.Detail;
            Assert.That(detail.IsOver, Is.False);
            Assert.That(detail.RemainingSeconds, Is.EqualTo(300f));
            Assert.That(detail.PointsOpen, Is.True);
            Assert.That(detail.OpenPointCount, Is.EqualTo(1));
            Assert.That(detail.CarriedCount, Is.EqualTo(20));
            Assert.That(status.OwnSide, Is.EqualTo((Side?)Side.Bravo));

            var formatter = new ExtractionHudFormatter();
            var lines = new List<LocalizedText>();
            formatter.AppendLines(status, lines);
            Assert.That(lines, Does.Contain(ExtractionHudFormatter.ExitsOpen(1)));
            Assert.That(lines, Does.Contain(ExtractionHudFormatter.Carrying(20)));
            Assert.That(formatter.Time(status), Is.EqualTo(ModeStatusFormat.Clock(300f)));

            var killed = new StashStateMessage { Outcome = Shooter.Protocol.ExtractionOutcome.Killed };
            killed.Stash.Add(_ammo, 10);
            _hud.Receive(killed);

            var over = (ExtractionStatus)GameSignals.ModeStatus.Value.Detail;
            Assert.That(over.Outcome, Is.EqualTo(Shooter.Core.ExtractionOutcome.Died));
            Assert.That(over.LostCount, Is.EqualTo(20), "what was carried did not reach the stash");
            Assert.That(over.StoredCount, Is.Zero);

            _hud.ClearSignals();
            Assert.That(GameSignals.Stash.Value, Is.Null);
            Assert.That(GameSignals.ModeStatus.Value, Is.Null);
        }

        private static SnapshotMessage BattleRoyaleSnapshot(int squadsRemaining)
        {
            var snapshot = Snapshot(MatchObjective.BattleRoyale);
            snapshot.Match.SquadsRemaining = squadsRemaining;
            snapshot.Match.ZonePhase = 1;
            snapshot.Match.ZoneCenter = new System.Numerics.Vector2(0f, 0f);
            snapshot.Match.ZoneRadius = 100f;
            snapshot.Match.NextZoneCenter = new System.Numerics.Vector2(10f, 5f);
            snapshot.Match.NextZoneRadius = 50f;
            snapshot.Match.ZonePhaseRemainingSeconds = 12;
            snapshot.Players[snapshot.Players.IndexOf(_localId)].SquadId = 0;
            snapshot.Players[snapshot.Players.IndexOf(_otherId)].SquadId = 1;
            return snapshot;
        }

        private static SnapshotMessage Snapshot(MatchObjective objective)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = objective;
            ref var local = ref snapshot.Players.Add();
            local.PlayerId = _localId;
            local.Team = Team.Bravo;
            local.Health = 80;
            local.Kills = 2;
            local.Deaths = 1;
            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _otherId;
            other.Team = Team.Alpha;
            other.Health = 100;
            return snapshot;
        }

        private static SnapshotMessage PayloadSnapshot(Team attackers, float progress, int passed, int pushers)
        {
            var snapshot = Snapshot(MatchObjective.Payload);
            snapshot.Match.PayloadProgress = progress;
            snapshot.Match.PayloadCheckpointsPassed = passed;
            snapshot.Match.PayloadPushers = pushers;
            ref var cart = ref snapshot.ObjectiveItems.Add();
            cart.ItemId = 0;
            cart.Present = ObjectiveItemFields.All;
            cart.Kind = ObjectiveItemKind.Cart;
            cart.Team = attackers;
            cart.State = ObjectiveItemState.Home;
            return snapshot;
        }

        // 中継点は始点と終点を除いた点にしか置けないので、中継点の数 + 2 個の点を並べる
        private void AddCart(int checkpoints)
        {
            var points = new Transform[checkpoints + 2];
            var checkpointPoints = new int[checkpoints];
            for (var i = 0; i < points.Length; i++)
            {
                var point = new GameObject("PayloadPoint" + i);
                point.transform.position = new Vector3(0f, 0f, i * 10f);
                _created.Add(point);
                points[i] = point.transform;
            }

            for (var i = 0; i < checkpoints; i++)
            {
                checkpointPoints[i] = i + 1;
            }

            var cart = new GameObject("PayloadCart").AddComponent<PayloadCart>();
            _created.Add(cart.gameObject);
            cart.SetRoute(points, checkpointPoints);
        }

        private static void AddPoint(SnapshotMessage snapshot, int index, Team? owner, int percent, bool contested)
        {
            ref var point = ref snapshot.ControlPoints.Add();
            point.Index = index;
            point.HasOwner = owner.HasValue;
            point.Owner = owner ?? Team.Alpha;
            point.ControlPercent = (sbyte)percent;
            point.Contested = contested;
        }
    }
}
