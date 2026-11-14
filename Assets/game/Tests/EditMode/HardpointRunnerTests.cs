using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// HardpointRunner が有効な拠点にいる参加者をハードポイントに伝え、有効な拠点だけを光らせ、HUD に移るまでの秒数を出すことを確かめる。
    /// bot (EnemyAgent) は NavMesh が要るので、巡回路は <see cref="HardpointRunner.RouteAround"/> の値だけを確かめる。
    /// </summary>
    public sealed class HardpointRunnerTests
    {
        private const float RotateSeconds = 10f;
        private const float RespawnSeconds = 1f;

        private static readonly Vector3 PointA = Vector3.zero;
        private static readonly Vector3 PointB = new Vector3(20f, 0f, 0f);

        private readonly List<Object> _objects = new List<Object>();
        private HardpointRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _runner = Track(new GameObject("Runner")).AddComponent<HardpointRunner>();
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
        }

        [Test]
        public void BeginHighlightsOnlyTheFirstPoint()
        {
            var zones = Zones();
            var player = Member("Player", 0, new Vector3(0f, 0f, -20f));
            Begin(zones, new[] { player }, player);

            Assert.That(_runner.Hardpoint.ActivePoint, Is.Zero);
            Assert.That(_runner.Zones, Is.EqualTo(zones));
            Assert.That(zones[0].Highlight, Is.EqualTo((bool?)true));
            Assert.That(zones[1].Highlight, Is.EqualTo((bool?)false));
            Assert.That(zones[0].Owner, Is.Null);
        }

        [Test]
        public void AMemberAloneOnTheActivePointScoresAndTheHudShowsTheCountdown()
        {
            var zones = Zones();
            var player = Member("Player", 0, PointA);
            var bot = Member("Bot-1-1", 1, PointB);
            var hud = BoundHudView();
            Begin(zones, new[] { player, bot }, player, hud);

            _runner.Tick(1f);

            var session = _runner.Session;
            Assert.That(session.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(session.GetScore(Side.Bravo), Is.Zero, "the bot stands on the inactive point");
            Assert.That(zones[0].Owner, Is.EqualTo((Team?)Team.Alpha));
            Assert.That(hud.Lines, Is.EqualTo(new[] { Localizer.Resolve(HardpointText.ActivePoint(0, 1, RotateSeconds - 1f)), "ALPHA が確保中" }));
        }

        [Test]
        public void BothSidesOnTheActivePointContestIt()
        {
            var zones = Zones();
            var player = Member("Player", 0, PointA);
            var bot = Member("Bot-1-1", 1, PointA + new Vector3(1f, 0f, 0f));
            var hud = BoundHudView();
            Begin(zones, new[] { player, bot }, player, hud);

            _runner.Tick(2f);

            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(_runner.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(_runner.Hardpoint.Status, Is.EqualTo(HardpointStatus.Contested));
            Assert.That(zones[0].Owner, Is.Null, "a contested point is shown neutral");
            Assert.That(hud.Lines[1], Is.EqualTo("争奪中"));
        }

        [Test]
        public void ADeadMemberDoesNotContest()
        {
            var zones = Zones();
            var player = Member("Player", 0, PointA);
            var bot = Member("Bot-1-1", 1, PointA + new Vector3(1f, 0f, 0f));
            Begin(zones, new[] { player, bot }, player);

            using (CombatEvents.Attribute(player.gameObject, "rifle"))
            {
                bot.Damageable.Kill();
            }

            _runner.Tick(0.5f);

            Assert.That(_runner.Hardpoint.Holder, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(0.5d));
        }

        [Test]
        public void ThePointMovesAfterTheRotateSecondsAndTheHighlightFollows()
        {
            var zones = Zones();
            var player = Member("Player", 0, PointA);
            Begin(zones, new[] { player }, player);

            _runner.Tick(RotateSeconds);

            Assert.That(_runner.Hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(zones[0].Highlight, Is.EqualTo((bool?)false));
            Assert.That(zones[1].Highlight, Is.EqualTo((bool?)true));
            Assert.That(zones[0].Owner, Is.Null, "the old point no longer shows the holder");

            var before = _runner.Session.GetScore(Side.Alpha);
            _runner.Tick(1f);
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(before), "standing on the old point scores nothing");

            player.transform.position = PointB;
            _runner.Tick(1f);
            Assert.That(_runner.Session.GetScore(Side.Alpha), Is.EqualTo(before + 1d));
        }

        [Test]
        public void BeginRejectsZonesThatDoNotMatchTheHardpoint()
        {
            var player = Member("Player", 0, PointA);
            var zones = Zones();

            var three = new Hardpoint(new HardpointSpec(3, RotateSeconds, 1f, 100, 300f, RespawnSeconds));
            Assert.Throws<ArgumentException>(() => _runner.Begin(three, zones, new[] { player }, Array.Empty<SideSpawnPoint>(), player));

            zones[1].Index = 0;
            Assert.Throws<ArgumentException>(() => _runner.Begin(CreateHardpoint(), zones, new[] { player }, Array.Empty<SideSpawnPoint>(), player));

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(CreateHardpoint(), new[] { zones[0] }, new[] { player }, Array.Empty<SideSpawnPoint>(), player),
                "a hardpoint needs two or more points");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Hardpoint, Is.Null);
        }

        [Test]
        public void AFailedBeginLeavesNoHardpointAttached()
        {
            var zones = Zones();
            var stray = Member("Bot-Stray", 5, PointA);

            Assert.Throws<ArgumentException>(
                () => _runner.Begin(CreateHardpoint(), zones, new[] { stray }, Array.Empty<SideSpawnPoint>()),
                "side 5 is not part of the two teams");
            Assert.That(_runner.IsStarted, Is.False);
            Assert.That(_runner.Hardpoint, Is.Null);
        }

        [Test]
        public void TheBotRouteCrossesThePointAndTurnsPerSlot()
        {
            var center = new Vector3(3f, 0f, 7f);
            var first = HardpointRunner.RouteAround(center, 4f, 0);
            var second = HardpointRunner.RouteAround(center, 4f, 1);

            Assert.That(first, Has.Length.EqualTo(2));
            Assert.That(Vector3.Distance(first[0], center), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Vector3.Distance(first[1], center), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Vector3.Distance((first[0] + first[1]) * 0.5f, center), Is.LessThan(1e-4f), "the two ends face each other across the center");
            Assert.That(first[0].y, Is.EqualTo(center.y));
            Assert.That(Vector3.Distance(first[0], second[0]), Is.GreaterThan(1f), "each bot crosses in its own direction");
            Assert.Throws<ArgumentOutOfRangeException>(() => HardpointRunner.RouteAround(center, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => HardpointRunner.RouteAround(center, 4f, -1));
        }

        [Test]
        public void TheMarkerIsDimmedOnlyWhenNotHighlighted()
        {
            var alpha = ControlPointZone.ColorOf(Team.Alpha);

            Assert.That(ControlPointZone.MarkerColorOf(Team.Alpha, null), Is.EqualTo(alpha), "domination keeps its colors");
            Assert.That(ControlPointZone.MarkerColorOf(Team.Alpha, true), Is.EqualTo(alpha));
            var dimmed = ControlPointZone.MarkerColorOf(Team.Alpha, false);
            Assert.That(dimmed.b, Is.EqualTo(alpha.b * ControlPointZone.DimmedBrightness).Within(1e-6f));
            Assert.That(dimmed.a, Is.EqualTo(alpha.a));
        }

        [Test]
        public void TheSpecComesFromTheInspectorValues()
        {
            _runner.RotateSeconds = 15f;
            _runner.PointsPerSecond = 2f;
            _runner.ScoreLimit = 40;
            _runner.TimeLimitSeconds = 120f;
            _runner.RespawnSeconds = 4f;

            var spec = _runner.CreateSpec(4);

            Assert.That(spec.PointCount, Is.EqualTo(4));
            Assert.That(spec.RotateSeconds, Is.EqualTo(15f));
            Assert.That(spec.PointsPerSecond, Is.EqualTo(2f));
            Assert.That(spec.ScoreLimit, Is.EqualTo(40));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(120f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(4f));

            _runner.RotateSeconds = 0f;
            Assert.Throws<ArgumentOutOfRangeException>(() => _runner.CreateSpec(4));
        }

        private void Begin(IReadOnlyList<ControlPointZone> zones, IReadOnlyList<SideMember> members, SideMember localPlayer, ModeHudView hud = null)
        {
            _runner.Begin(CreateHardpoint(), zones, members, Array.Empty<SideSpawnPoint>(), localPlayer, hud);
        }

        private static Hardpoint CreateHardpoint()
        {
            return new Hardpoint(new HardpointSpec(2, RotateSeconds, 1f, 100, 300f, RespawnSeconds));
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

        private SideMember Member(string name, int side, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private ModeHudView BoundHudView()
        {
            var view = Track(new GameObject("ModeHud")).AddComponent<ModeHudView>();
            view.Bind(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree());
            return view;
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
