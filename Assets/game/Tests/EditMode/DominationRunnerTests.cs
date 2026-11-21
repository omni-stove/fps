using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class DominationRunnerTests
    {
        // 1 秒で占領、1 秒に 10 点、30 点で勝ち、制限時間 60 秒、倒れて 2 秒で戻る。
        private const float CaptureSeconds = 1f;
        private const float PointsPerSecond = 10f;
        private const int ScoreLimit = 30;
        private const float TimeLimit = 60f;
        private const float RespawnSeconds = 2f;

        private static readonly Vector3 PointA = Vector3.zero;
        private static readonly Vector3 PointB = new Vector3(20f, 0f, 0f);
        private static readonly Vector3 Away = new Vector3(0f, 0f, -40f);

        private readonly List<Object> _objects = new List<Object>();
        private DominationRunner _runner;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
            _runner = Track(new GameObject("Runner")).AddComponent<DominationRunner>();
            _runner.CaptureSeconds = CaptureSeconds;
            _runner.PointsPerSecond = PointsPerSecond;
            _runner.ScoreLimit = ScoreLimit;
            _runner.TimeLimitSeconds = TimeLimit;
            _runner.RespawnSeconds = RespawnSeconds;
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Stop();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void BeginShowsEveryPointNeutralAndPublishesTheStatusAsTheDetail()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            Begin(zones, new[] { player }, player);

            Assert.That(_runner.Zones, Is.EqualTo(zones));
            Assert.That(zones[0].Owner, Is.Null);
            Assert.That(zones[1].Owner, Is.Null);
            Assert.That(GameSignals.ModeStatus.Value, Is.SameAs(_runner.Status));
            Assert.That(GameSignals.PaletteViewer.Value, Is.EqualTo((Team?)TeamColors.OfflineViewer));

            var detail = _runner.Status.Detail as DominationStatus;
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail.Points, Is.EqualTo(new[]
            {
                new ControlPointStatus(null, 0, false),
                new ControlPointStatus(null, 0, false),
            }));
            Assert.That(detail.RemainingSeconds, Is.EqualTo(TimeLimit));
            Assert.That(detail.IsEnded, Is.False);
            Assert.That(_runner.Status.OwnSide, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void StandingAloneOnAPointCapturesItAndScoresForEveryOwnedSecond()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, PointA);
            var bot = Member("Bot-1", Side.Bravo, Away);
            Begin(zones, new[] { player, bot }, player);

            _runner.Tick(CaptureSeconds);

            Assert.That(_runner.Domination.GetOwner(0), Is.EqualTo((Team?)Team.Alpha));
            Assert.That(_runner.Domination.GetOwner(1), Is.Null);
            Assert.That(zones[0].Owner, Is.EqualTo((Team?)Team.Alpha));
            var detail = (DominationStatus)_runner.Status.Detail;
            Assert.That(detail.Points[0], Is.EqualTo(new ControlPointStatus(Team.Alpha, ControlPointStatus.FullPercent, false)));

            var before = _runner.Session.GetScore(Side.Alpha);
            _runner.Tick(1f);

            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(before + PointsPerSecond));
            Assert.That(_runner.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(((DominationStatus)_runner.Status.Detail).AlphaScore, Is.EqualTo((float)(before + PointsPerSecond)));
        }

        [Test]
        public void BothSidesOnAPointContestItAndNobodyCapturesIt()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, PointA);
            var bot = Member("Bot-1", Side.Bravo, PointA + new Vector3(1f, 0f, 0f));
            Begin(zones, new[] { player, bot }, player);

            _runner.Tick(2f);

            Assert.That(_runner.Domination.GetOwner(0), Is.Null);
            Assert.That(_runner.Domination.IsContested(0), Is.True);
            Assert.That(((DominationStatus)_runner.Status.Detail).Points[0].Contested, Is.True);
            Assert.That(zones[0].Owner, Is.Null);
        }

        [Test]
        public void ADeadMemberLeavesThePoint()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, PointA);
            var bot = Member("Bot-1", Side.Bravo, PointA + new Vector3(1f, 0f, 0f));
            Begin(zones, new[] { player, bot }, player);

            using (CombatEvents.Attribute(player.gameObject, "rifle"))
            {
                bot.Damageable.Kill();
            }

            _runner.Tick(CaptureSeconds);

            Assert.That(_runner.Domination.GetPlayerPoint(_runner.IdOf(bot)), Is.Null);
            Assert.That(_runner.Domination.GetOwner(0), Is.EqualTo((Team?)Team.Alpha));
            Assert.That(_runner.Session.GetKills(_runner.IdOf(player)), Is.EqualTo(1));
        }

        [Test]
        public void ADeadMemberRespawnsAfterTheRespawnSeconds()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            var bot = Member("Bot-1", Side.Bravo, PointB);
            Begin(zones, new[] { player, bot }, player);

            using (CombatEvents.Attribute(player.gameObject, "rifle"))
            {
                bot.Damageable.Kill();
            }

            _runner.Tick(RespawnSeconds - 0.5f);
            Assert.That(bot.Damageable.IsDead, Is.True);

            _runner.Tick(1f);
            Assert.That(bot.Damageable.IsDead, Is.False);
        }

        [Test]
        public void ReachingTheScoreLimitEndsTheMatchWithTheWinnerOnce()
        {
            var results = new List<MatchResult>();
            CombatEvents.MatchFinished += results.Add;
            var zones = Zones();
            var player = Member("Player", Side.Alpha, PointA);
            var bot = Member("Bot-1", Side.Bravo, Away);
            Begin(zones, new[] { player, bot }, player);

            _runner.Tick(CaptureSeconds);
            _runner.Tick(ScoreLimit / PointsPerSecond);
            _runner.Tick(1f);

            Assert.That(_runner.IsMatchOver, Is.True);
            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.EqualTo((Side?)Side.Alpha));
            var detail = (DominationStatus)_runner.Status.Detail;
            Assert.That(detail.IsEnded, Is.True);
            Assert.That(detail.Winner, Is.EqualTo((Team?)Team.Alpha));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Outcome, Is.EqualTo(MatchOutcome.Won));
            Assert.That(results[0].OwnScore, Is.EqualTo(ScoreLimit));
        }

        [Test]
        public void RunningOutOfTimeWithNoPointsIsADraw()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            Begin(zones, new[] { player }, player);

            _runner.Tick(TimeLimit);

            Assert.That(_runner.Status.IsEnded, Is.True);
            Assert.That(_runner.Status.Winner, Is.Null);
            Assert.That(((DominationStatus)_runner.Status.Detail).Winner, Is.Null);
        }

        [Test]
        public void BeginAndAKillPublishTheMembersOnTheScoreboard()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            var bot = Member("Bot-1", Side.Bravo, PointB);
            Begin(zones, new[] { player, bot }, player);

            var entries = GameSignals.Scoreboard.Value;
            Assert.That(entries, Is.SameAs(_runner.PublishedScoreboard));
            Assert.That(entries.Count(), Is.EqualTo(2));
            Assert.That(entries[0].Name == ScoreboardName.LocalPlayer, Is.True);
            Assert.That(entries[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(entries[0].IsLocal, Is.True);
            Assert.That(entries[1].Name == ScoreboardName.Literal("Bot-1"), Is.True);
            Assert.That(entries[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(entries[1].IsLocal, Is.False);

            using (CombatEvents.Attribute(player.gameObject, "rifle"))
            {
                bot.Damageable.Kill();
            }

            var after = GameSignals.Scoreboard.Value;
            Assert.That(after, Is.Not.SameAs(entries), "a new array is published for every change");
            Assert.That(after[0].Kills, Is.EqualTo(1));
            Assert.That(after[1].Deaths, Is.EqualTo(1));
        }

        [Test]
        public void StopClearsOnlyWhatThisRunnerPublished()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            Begin(zones, new[] { player }, player);

            _runner.Stop();

            Assert.That(GameSignals.ModeStatus.HasValue, Is.False);
            Assert.That(GameSignals.Scoreboard.HasValue, Is.False);
            Assert.That(GameSignals.PaletteViewer.HasValue, Is.False);
            Assert.That(_runner.Domination, Is.Null);
            Assert.That(_runner.Zones, Is.Empty);
        }

        [Test]
        public void StopKeepsTheScoreboardAnotherRunnerPublished()
        {
            var zones = Zones();
            var player = Member("Player", Side.Alpha, Away);
            Begin(zones, new[] { player }, player);
            var other = new[] { new ScoreboardEntry(7, ScoreboardName.Literal("other"), Team.Bravo, 0, 0, 0, ScoreboardConnection.None, false) };
            GameSignals.Scoreboard.Publish(other);

            _runner.Stop();

            Assert.That(GameSignals.Scoreboard.Value, Is.SameAs(other));
        }

        [Test]
        public void DuplicatePointIndicesAreRejected()
        {
            var player = Member("Player", Side.Alpha, Away);
            var first = Zone("ControlPoint-A", 0, PointA);
            var second = Zone("ControlPoint-B", 0, PointB);

            var e = Assert.Throws<ArgumentException>(
                () => _runner.Begin(_runner.CreateDomination(2), new[] { first, second }, new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            StringAssert.Contains("used more than once", e.Message);
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void PointCountMustMatchTheDomination()
        {
            var player = Member("Player", Side.Alpha, Away);

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(_runner.CreateDomination(3), Zones(), new[] { player }, Array.Empty<SideSpawnPoint>(), player));
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Domination, Is.Null);
        }

        [Test]
        public void InvalidSettingsAreRejected()
        {
            _runner.ScoreLimit = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));

            _runner.ScoreLimit = ScoreLimit;
            _runner.RespawnSeconds = -1f;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateDomination(1));
        }

        [Test]
        public void TheTuningAssetDecidesTheSpecAndTheRespawnSeconds()
        {
            var tuning = Tuning();
            tuning.CaptureSeconds = 2f;
            tuning.PointsPerSecond = 5f;
            tuning.ScoreLimit = 10;
            tuning.TimeLimitSeconds = 90f;
            tuning.MaxCapturers = 2;
            tuning.RespawnSeconds = RespawnSeconds * 2f;
            _runner.Definition = tuning;

            var spec = _runner.CreateSpec(2);

            Assert.That(spec.CaptureSeconds, Is.EqualTo(2f));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(5f));
            Assert.That(spec.ScoreLimit, Is.EqualTo(10));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(spec.MaxCapturers, Is.EqualTo(2));
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(RespawnSeconds * 2f));
        }

        [Test]
        public void WithoutTuningTheRunnerUsesItsOwnValues()
        {
            Assert.That(_runner.Definition, Is.Null);
            Assert.That(_runner.EffectiveRespawnSeconds, Is.EqualTo(RespawnSeconds));

            var spec = _runner.CreateSpec(2);

            Assert.That(spec.PointCount, Is.EqualTo(2));
            Assert.That(spec.CaptureSeconds, Is.EqualTo(CaptureSeconds));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(PointsPerSecond));
            Assert.That(spec.ScoreLimit, Is.EqualTo(ScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(TimeLimit));
        }

        [Test]
        public void ANewTuningAssetHoldsTheDefaults()
        {
            var tuning = Tuning();
            var spec = tuning.ToSpec(3);

            Assert.That(spec.PointCount, Is.EqualTo(3));
            Assert.That(spec.CaptureSeconds, Is.EqualTo(DominationRunner.DefaultCaptureSeconds));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(DominationRunner.DefaultPointsPerSecond));
            Assert.That(spec.ScoreLimit, Is.EqualTo(DominationRunner.DefaultScoreLimit));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(DominationRunner.DefaultTimeLimitSeconds));
            Assert.That(spec.MaxCapturers, Is.EqualTo(DominationSpec.DefaultMaxCapturers));
            Assert.That(tuning.RespawnSeconds, Is.EqualTo(DominationRunner.DefaultRespawnSeconds));
            Assert.DoesNotThrow(tuning.Validate);
        }

        [Test]
        public void AnInvalidTuningAssetIsRejectedWithItsName()
        {
            var tuning = Tuning();
            tuning.ScoreLimit = 0;
            _runner.Definition = tuning;

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));
            StringAssert.Contains("'DominationTuning'", e.Message);
            Assert.That(e.ParamName, Is.EqualTo("scoreLimit"));

            tuning.ScoreLimit = 1;
            tuning.RespawnSeconds = -1f;
            Assert.Throws<ArgumentOutOfRangeException>(tuning.Validate);
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(1));
            Assert.That(_runner.IsStarted, Is.False);
        }

        [Test]
        public void CopyFromTakesEveryValueOfTheTuningAsset()
        {
            var tuning = Tuning();
            tuning.CaptureSeconds = 3f;
            tuning.PointsPerSecond = 4f;
            tuning.ScoreLimit = 50;
            tuning.TimeLimitSeconds = 90f;
            tuning.MaxCapturers = 2;
            tuning.RespawnSeconds = 6f;

            _runner.CopyFrom(tuning);

            Assert.That(_runner.CaptureSeconds, Is.EqualTo(3f));
            Assert.That(_runner.PointsPerSecond, Is.EqualTo(4f));
            Assert.That(_runner.ScoreLimit, Is.EqualTo(50));
            Assert.That(_runner.TimeLimitSeconds, Is.EqualTo(90f));
            Assert.That(_runner.MaxCapturers, Is.EqualTo(2));
            Assert.That(_runner.RespawnSeconds, Is.EqualTo(6f));
            Assert.That(_runner.Definition, Is.Null, "copying does not wire the asset");
            Assert.Throws<ArgumentNullException>(() => _runner.CopyFrom(null));
        }

        [Test]
        public void ZoneScanCountsOnlyLivingCandidatesInside()
        {
            var zone = Zone("ControlPoint-A", 0, PointA);
            var inside = Member("Inside", Side.Alpha, PointA).Damageable;
            var outside = Member("Outside", Side.Bravo, Away).Damageable;
            zone.Scan(new[] { inside, outside, null });

            Assert.That(zone.Occupants, Is.EqualTo(new[] { inside }));

            inside.Kill();
            zone.Scan(new[] { inside });
            Assert.That(zone.Occupants, Is.Empty);
        }

        private DominationDefinition Tuning()
        {
            var tuning = ScriptableObject.CreateInstance<DominationDefinition>();
            tuning.name = "DominationTuning";
            _objects.Add(tuning);
            return tuning;
        }

        private void Begin(IReadOnlyList<ControlPointZone> zones, IReadOnlyList<SideMember> members, SideMember localPlayer)
        {
            _runner.Begin(_runner.CreateDomination(zones.Count), zones, members, Array.Empty<SideSpawnPoint>(), localPlayer);
        }

        private ControlPointZone[] Zones()
        {
            return new[] { Zone("ControlPoint-A", 0, PointA), Zone("ControlPoint-B", 1, PointB) };
        }

        private ControlPointZone Zone(string name, int index, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var zone = go.AddComponent<ControlPointZone>();
            zone.ShowsMarker = false;
            zone.Index = index;
            zone.Radius = 4f;
            return zone;
        }

        private SideMember Member(string name, Side side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = side;
            return member;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
