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
    // 移動と戦闘を動かさず信号を直に出すのは、閾値の境目を tick 単位で狙って確かめるため。見えるかは x=5 に立てた壁 (z が -10 から 0) で分ける
    public sealed class ServerCheatStatsTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _productUserId = "0002aaaabbbbccccddddeeeeffff0001";
        private const string _otherProductUserId = "0002aaaabbbbccccddddeeeeffff0002";
        private static readonly DateTime _time = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        private static readonly Vector3 _viewerSpot = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _hidden = new Vector3(10f, 1f, -5f);
        private static readonly Vector3 _open = new Vector3(10f, 1f, 5f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerCheatSignals _signals;
        private ServerCheatStats _stats;
        private HashSet<int> _alive;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _signals = new ServerCheatSignals();
            _alive = new HashSet<int>();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _stats?.Stop();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void AHeadshotRatioOverTheThresholdIsWrittenAsOneJsonLine()
        {
            Start(Stats());
            Join(1, _productUserId);

            for (var i = 0; i < 3; i++)
            {
                Shoot(1, 2, 0f, HitZone.Head);
            }

            Assert.That(
                Suspicions(),
                Is.EqualTo(new[]
                {
                    "{\"type\":\"cheat-stats\",\"playerId\":\"1\",\"productUserId\":\"" + _productUserId + "\",\"metric\":\"headshotRatio\","
                        + "\"value\":\"1\",\"threshold\":\"0.5\",\"windowSeconds\":\"60\",\"samples\":\"3\"}",
                }));
            Assert.That(FlatJsonObject.TryParse(Suspicions()[0], out var values, out var error), Is.True, error);
            Assert.That(values["metric"], Is.EqualTo("headshotRatio"));
        }

        [Test]
        public void AHeadshotRatioAtOrUnderTheThresholdIsNotWritten()
        {
            Start(Stats());

            Shoot(1, 2, 0f, HitZone.Head);
            Shoot(1, 2, 0f, HitZone.Body);
            Shoot(1, 2, 0f, HitZone.Limb);
            Shoot(1, 2, 0f, HitZone.Body);

            Assert.That(Suspicions(), Is.Empty);
        }

        [Test]
        public void TheHeadshotRatioWaitsForTheMinimumSamples()
        {
            Start(Stats());

            Shoot(1, 2, 0f, HitZone.Head);
            Shoot(1, 2, 0f, HitZone.Head);

            Assert.That(Suspicions(), Is.Empty, "two hits are fewer than minSamples=3");
        }

        [Test]
        public void ThePelletsOfOneShotOnOneTargetCountAsOneHitThatIsHeadIfAnyIs()
        {
            Start(Stats());

            for (var i = 0; i < 3; i++)
            {
                Shoot(1, 2, 0f, HitZone.Body, HitZone.Head, HitZone.Body);
            }

            Assert.That(Suspicions(), Has.Count.EqualTo(1));
            Assert.That(Suspicions()[0], Does.Contain("\"value\":\"1\"").And.Contain("\"samples\":\"3\""));
        }

        [Test]
        public void ThePelletsOfOneShotThatAlternateBetweenTwoTargetsCountOncePerTarget()
        {
            Start(Stats());

            _signals.RaiseShotAccepted(1, Vector3.Zero, Vector3.UnitX, 0f);
            _signals.RaiseHit(1, 2, HitZone.Head, 0f);
            _signals.RaiseHit(1, 3, HitZone.Body, 0f);
            _signals.RaiseHit(1, 2, HitZone.Head, 0f);
            _signals.RaiseHit(1, 3, HitZone.Body, 0f);

            Assert.That(Suspicions(), Is.Empty, "one shot is two hits, fewer than minSamples=3");
        }

        [Test]
        public void TheSameMetricIsWrittenOncePerWindowAndAgainInTheNextWindow()
        {
            Start(Stats(windowSeconds: 1f));

            for (var i = 0; i < 6; i++)
            {
                Shoot(1, 2, 0f, HitZone.Head);
            }

            Assert.That(Suspicions(), Has.Count.EqualTo(1), "still in the same window");

            Step(63);
            for (var i = 0; i < 3; i++)
            {
                Shoot(1, 2, 0f, HitZone.Head);
            }

            Assert.That(Suspicions(), Has.Count.EqualTo(2), "a new window counts from zero and may write again");
            Assert.That(Suspicions()[1], Does.Contain("\"samples\":\"3\"").And.Contain("\"windowSeconds\":\"1\""));
        }

        [Test]
        public void EachPlayerIsWrittenOnTheirOwn()
        {
            Start(Stats());

            for (var i = 0; i < 3; i++)
            {
                Shoot(1, 3, 0f, HitZone.Head);
                Shoot(2, 3, 0f, HitZone.Head);
            }

            Assert.That(Suspicions(), Has.Count.EqualTo(2));
            Assert.That(Suspicions()[0], Does.Contain("\"playerId\":\"1\""));
            Assert.That(Suspicions()[1], Does.Contain("\"playerId\":\"2\""));
        }

        [Test]
        public void APlayerWhoLeavesStartsAgainFromZero()
        {
            Start(Stats());
            Join(1, _productUserId);
            Shoot(1, 2, 0f, HitZone.Head);
            Shoot(1, 2, 0f, HitZone.Head);

            Assert.That(_match.Leave(new ConnectionId(1UL)), Is.True);
            Join(1, _otherProductUserId);
            Shoot(1, 2, 0f, HitZone.Head);

            Assert.That(Suspicions(), Is.Empty, "the two hits before leaving are not added to the new player");
        }

        [Test]
        public void AReactionFasterThanTheThresholdIsWrittenWithTheMeanInMilliseconds()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            Step(1);

            React(3, 3);

            Assert.That(
                Suspicions(),
                Is.EqualTo(new[]
                {
                    "{\"type\":\"cheat-stats\",\"playerId\":\"1\",\"metric\":\"reactionMs\","
                        + "\"value\":\"50\",\"threshold\":\"100\",\"windowSeconds\":\"60\",\"samples\":\"3\"}",
                }));
        }

        [Test]
        public void ATickClaimedBeyondTheRewindRangeIsClampedAndTheReactionStillCounts()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            Step(1);

            React(3, 3, 100);

            Assert.That(Suspicions(), Has.Count.EqualTo(1));
            Assert.That(Suspicions()[0], Does.Contain("\"metric\":\"reactionMs\"").And.Contain("\"value\":\"50\""));
        }

        [Test]
        public void AReactionSlowerThanTheThresholdIsNotWritten()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            Step(1);

            React(3, 12);

            Assert.That(Suspicions(), Is.Empty, "200ms is slower than minReactionMs=100");
        }

        [Test]
        public void AFastReactionIsWrittenOncePerWindow()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            Step(1);

            React(6, 3);

            Assert.That(Suspicions().Count(line => line.Contains("\"metric\":\"reactionMs\"", StringComparison.Ordinal)), Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheFirstHitAfterTheTargetAppearsIsAReaction()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            Step(1);
            Place(2, _open, 0f);
            Step(1);
            var seenTick = _tick;

            Shoot(1, 2, seenTick + 3, HitZone.Body);
            Shoot(1, 2, seenTick + 3, HitZone.Body);
            Shoot(1, 2, seenTick + 3, HitZone.Body);

            Assert.That(Suspicions(), Is.Empty, "the target appeared once, so there is one reaction");
        }

        [Test]
        public void AYawJumpOverTheThresholdInOneTickIsWritten()
        {
            Start(Stats());

            _signals.RaiseLooked(1, 10, 0f, null);
            _signals.RaiseLooked(1, 11, 1f, null);

            Assert.That(
                Suspicions(),
                Is.EqualTo(new[]
                {
                    "{\"type\":\"cheat-stats\",\"playerId\":\"1\",\"metric\":\"snapDegreesPerTick\","
                        + "\"value\":\"57.296\",\"threshold\":\"40\",\"windowSeconds\":\"60\"}",
                }));
        }

        [Test]
        public void AYawTurnUnderTheThresholdIsNotWritten()
        {
            Start(Stats());

            _signals.RaiseLooked(1, 10, 0f, null);
            _signals.RaiseLooked(1, 11, 0.5f, null);
            _signals.RaiseLooked(1, 20, 3.1f, null);
            _signals.RaiseLooked(1, 21, -3.1f, null);

            Assert.That(Suspicions(), Is.Empty, "0.5 rad is about 28.6 degrees, and 3.1 to -3.1 is about 4.8 degrees the short way round");
        }

        [Test]
        public void AYawJumpAcrossADroppedInputIsNotWritten()
        {
            Start(Stats());

            _signals.RaiseLooked(1, 10, 0f, null);
            _signals.RaiseLooked(1, 12, 1.5f, null);

            Assert.That(Suspicions(), Is.Empty, "ticks 10 and 12 are not one tick apart");
        }

        [Test]
        public void AYawJumpRightAfterARespawnIsNotWritten()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            _signals.RaiseLooked(1, 10, 0f, null);

            Place(1, _viewerSpot, 2f);
            _signals.RaiseLooked(1, 11, 2f, null);

            Assert.That(Suspicions(), Is.Empty, "the server turned the body when it spawned it again");
        }

        [Test]
        public void YawJumpsAreWrittenOncePerWindow()
        {
            Start(Stats());

            _signals.RaiseLooked(1, 10, 0f, null);
            _signals.RaiseLooked(1, 11, 1f, null);
            _signals.RaiseLooked(1, 12, 0f, null);
            _signals.RaiseLooked(1, 13, 1.5f, null);

            Assert.That(Suspicions(), Has.Count.EqualTo(1));
            Assert.That(Suspicions()[0], Does.Contain("\"value\":\"57.296\""), "the first jump is the one written");
        }

        [Test]
        public void FacingAHiddenTargetLongerThanTheThresholdIsWritten()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            _signals.RaiseLooked(1, 1, Toward(_viewerSpot, _hidden), null);

            Step(16);
            Assert.That(Suspicions(), Is.Empty, "15 ticks (0.25s) since the first facing tick is not longer than 0.26s");

            Step(1);
            Assert.That(
                Suspicions(),
                Is.EqualTo(new[]
                {
                    "{\"type\":\"cheat-stats\",\"playerId\":\"1\",\"metric\":\"wallTrackSeconds\","
                        + "\"value\":\"0.267\",\"threshold\":\"0.26\",\"windowSeconds\":\"60\"}",
                }));
        }

        [Test]
        public void FacingAHiddenTargetIsWrittenOncePerWindow()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            _signals.RaiseLooked(1, 1, Toward(_viewerSpot, _hidden), null);

            Step(120);

            Assert.That(Suspicions(), Has.Count.EqualTo(1));
        }

        [Test]
        public void FacingAVisibleTargetOrLookingAwayFromAHiddenOneIsNotWritten()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _open, 0f);
            Place(3, _hidden, 0f);
            _signals.RaiseLooked(1, 1, Toward(_viewerSpot, _open), null);

            Step(60);

            Assert.That(Suspicions(), Is.Empty, "player 2 is in the open and player 3 is off to the side");
        }

        [Test]
        public void LookingAwayRestartsTheTimeFacingAHiddenTarget()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            var toward = Toward(_viewerSpot, _hidden);

            _signals.RaiseLooked(1, 1, toward, null);
            Step(12);
            _signals.RaiseLooked(1, 2, toward + 0.3f, null);
            Step(1);
            _signals.RaiseLooked(1, 3, toward, null);
            Step(12);

            Assert.That(Suspicions(), Is.Empty, "two stretches of 0.2s are not one of 0.26s");
        }

        [Test]
        public void ADeadTargetIsNotTracked()
        {
            Start(Stats());
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);
            _alive.Remove(2);
            _signals.RaiseLooked(1, 1, Toward(_viewerSpot, _hidden), null);

            Step(60);

            Assert.That(Suspicions(), Is.Empty);
        }

        [Test]
        public void BotsAreNotCounted()
        {
            Start(Stats());
            var bot = BotRoster.FirstBotPlayerId;
            Place(bot, _viewerSpot, Toward(_viewerSpot, _hidden));
            Place(2, _hidden, 0f);

            for (var i = 0; i < 3; i++)
            {
                Shoot(bot, 2, 0f, HitZone.Head);
            }

            _signals.RaiseLooked(bot, 10, 0f, null);
            _signals.RaiseLooked(bot, 11, 2f, null);
            Step(60);

            Assert.That(Suspicions(), Is.Empty);
        }

        [Test]
        public void HeadshotsOnABotStillCountForThePlayer()
        {
            Start(Stats());

            for (var i = 0; i < 3; i++)
            {
                Shoot(1, BotRoster.FirstBotPlayerId, 0f, HitZone.Head);
            }

            Assert.That(Suspicions(), Has.Count.EqualTo(1));
        }

        [Test]
        public void WithTheStatsOffNothingIsCollected()
        {
            Start(Stats(enabled: false));
            Place(1, _viewerSpot, 0f);
            Place(2, _hidden, 0f);

            for (var i = 0; i < 3; i++)
            {
                Shoot(1, 2, 0f, HitZone.Head);
            }

            _signals.RaiseLooked(1, 10, 0f, null);
            _signals.RaiseLooked(1, 11, Toward(_viewerSpot, _hidden), null);
            Step(60);

            Assert.That(_log.ToString(), Is.Empty, "not even the start line is written");
        }

        [Test]
        public void TheStartLineNamesTheThresholds()
        {
            Start(Stats());

            Assert.That(
                _log.ToString(),
                Does.Contain(
                    "[CheatStats] collecting window=60s headshotRatio>0.5 reactionMs<100 snapDegreesPerTick>40 wallTrackSeconds>0.26 minSamples=3"));
        }

        [Test]
        public void TheArgumentsAreRequired()
        {
            var tuning = Tuning(Stats());

            Assert.Throws<ArgumentNullException>(() => new ServerCheatStats(null, tuning, _ => true, (_, _) => true));
            Assert.Throws<ArgumentNullException>(() => new ServerCheatStats(_signals, null, _ => true, (_, _) => true));
            Assert.Throws<ArgumentNullException>(() => new ServerCheatStats(_signals, tuning, null, (_, _) => true));
            Assert.Throws<ArgumentNullException>(() => new ServerCheatStats(_signals, tuning, _ => true, null));
            Assert.Throws<ArgumentNullException>(() => new ServerCheatStats(_signals, tuning, (ServerCombat)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerCheatStats(_signals, tuning, _ => true, (_, _) => true, 0));
        }

        [Test]
        public void TheLogLineLeavesOutTheProductUserIdAndSamplesWhenThereAreNone()
        {
            var line = ServerCheatStatsLog.Suspicion(4, null, ServerCheatMetric.WallTrackSeconds, 1.75f, 1.5f, 120f);

            Assert.That(
                line,
                Is.EqualTo("{\"type\":\"cheat-stats\",\"playerId\":\"4\",\"metric\":\"wallTrackSeconds\",\"value\":\"1.75\",\"threshold\":\"1.5\",\"windowSeconds\":\"120\"}"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerCheatStatsLog.Key((ServerCheatMetric)99));
        }

        private static AntiCheatTuning.StatsSettings Stats(bool enabled = true, float windowSeconds = 60f)
        {
            return new AntiCheatTuning.StatsSettings(enabled, windowSeconds, 0.5f, 100f, 40f, 0.26f, 3);
        }

        // 余裕の距離と秒を 0 にして、壁の陰に入った tick から見えないと数えさせる
        private static AntiCheatTuning Tuning(AntiCheatTuning.StatsSettings stats)
        {
            return new AntiCheatTuning(
                new AntiCheatTuning.ConcealSettings(false, 150f, 0f, 0f),
                AntiCheatTuning.ViolationSettings.Default,
                stats,
                AntiCheatTuning.EvidenceSettings.Default);
        }

        private static float Toward(Vector3 from, Vector3 to)
        {
            return MathF.Atan2(to.X - from.X, to.Z - from.Z);
        }

        private void Start(AntiCheatTuning.StatsSettings stats)
        {
            var colliders = new[]
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, new Vector3(5f, 2.5f, -5f), new Vector3(0.1f, 2.5f, 5f), Quaternion.Identity),
            };
            var map = new ServerMapData("CheatStatsTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory");
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, TextWriter.Null, new ServerEventWriter(false, TextWriter.Null, TextWriter.Null), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl());
            _stats = new ServerCheatStats(_signals, Tuning(stats), id => _alive.Contains(id), (a, b) => a != b);
            _stats.Start(_context);
        }

        private void Join(int id, string productUserId)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id, productUserId);
        }

        private void Place(int id, Vector3 position, float yaw)
        {
            if (!_context.Bodies.TryGet(id, out _))
            {
                _context.Bodies.Add(id);
            }

            _alive.Add(id);
            _context.Bodies.Teleport(id, position, yaw, _tick);
        }

        private void Shoot(int shooter, int victim, float shotTick, params HitZone[] zones)
        {
            _signals.RaiseShotAccepted(shooter, Vector3.Zero, Vector3.UnitX, shotTick);

            // 当たり判定は申告の tick を [今 - 最大巻き戻し, 今] に丸めて巻き戻し幅を出すので、ここでも同じに丸める
            var rewind = Math.Clamp(_tick - shotTick, 0f, LagCompensator.DefaultMaxRewindTicks);
            foreach (var zone in zones)
            {
                _signals.RaiseHit(shooter, victim, zone, rewind);
            }
        }

        // 毎回陰へ戻すのは、見え始め 1 回につき反応を 1 つしか数えないため
        private void React(int count, int delayTicks, int claimedOffset = 0)
        {
            for (var i = 0; i < count; i++)
            {
                Place(2, _open, 0f);
                Step(1);
                var seenTick = _tick;
                Step(delayTicks);
                Shoot(1, 2, seenTick + delayTicks + claimedOffset, HitZone.Body);
                Place(2, _hidden, 0f);
                Step(1);
            }
        }

        private void Step(int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                _tick++;
                _stats.Tick(_tick, _tickSeconds);
            }
        }

        private List<string> Suspicions()
        {
            return _log.ToString()
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith('{'))
                .ToList();
        }
    }
}
