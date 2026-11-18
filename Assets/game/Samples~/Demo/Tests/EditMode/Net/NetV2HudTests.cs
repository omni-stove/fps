using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2HudTests
    {
        private const int _localId = 3;
        private const int _otherId = 7;

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
            GameSignals.Domination.Clear();
            GameSignals.Scoreboard.Clear();
            GameSignals.GrenadeCount.Clear();
            GameSignals.LocalBlindSeconds.Clear();
            GameSignals.Inventory.Clear();
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

            var status = GameSignals.Domination.Value;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.AlphaScore, Is.EqualTo(30f));
            Assert.That(status.BravoScore, Is.EqualTo(12f));
            Assert.That(status.RemainingSeconds, Is.EqualTo(90f));
            Assert.That(status.Points, Is.EqualTo(new[] { new ControlPointStatus(Team.Alpha, 100, false), new ControlPointStatus(null, -40, true) }),
                "point 2 is missing, so point 3 is not shown under the name C");
            Assert.That(_hud.Model.FirstScore, Is.EqualTo(12), "the HUD's team scores are the domination points");

            _hud.Apply(snapshot);
            Assert.That(GameSignals.Domination.Value, Is.SameAs(status), "an unchanged state is not published again");

            snapshot.Match.Objective = MatchObjective.None;
            _hud.Apply(snapshot);
            Assert.That(GameSignals.Domination.Value, Is.Null);
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
            Assert.That(GameSignals.Domination.Value, Is.Null);
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
            Assert.That(view.Domination.Source, Is.Null, "the scores and the time are on the HUD already");

            // Edit Mode では UIDocument が木を作らないことがあり、そのときビューは何も受けない
            if (view.View != null)
            {
                Assert.That(view.View.HealthLabel.text, Is.EqualTo(HudNumberFormat.Health(_hud.Model)));
                Assert.That(rings.Statuses, Has.Count.EqualTo(2));
                Assert.That(rings.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.Domination.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            }

            Assert.That(view.KillFeed.Names.Describe(new KillFeedEntry(_otherId, _localId, "", false, false)).Victim,
                Is.EqualTo(new KillFeedName(BotRoster.KillFeedName(_localId), Team.Bravo, true)));
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
