using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // Bot の手榴弾・近接・物陰を、判断 (EnemyBrain) と問い合わせの差し替えで確かめる
    public sealed class ServerBotTacticsTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const float _centerHeight = 1f;
        private const float _hideHeight = 0.9f;
        private const float _peekHeight = 1.6f;
        private const float _wallX = 2f;
        private static readonly Vector3 _self = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _threat = new Vector3(10f, 1f, 0f);

        [Test]
        public void ABotThrowsAtATargetItLostWithinTheThrowingDistance()
        {
            var tactics = BotTacticsSpec.Default;
            var lastSeen = new Vector3(12f, 1f, 0f);

            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, lastSeen, tactics), Is.True);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Search, false, _self, lastSeen, tactics), Is.True);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.TakeCover, false, _self, lastSeen, tactics), Is.True);
        }

        [Test]
        public void ABotDoesNotThrowAtWhatItSeesOrWhileItIsNotFighting()
        {
            var tactics = BotTacticsSpec.Default;
            var lastSeen = new Vector3(12f, 1f, 0f);

            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, true, _self, lastSeen, tactics), Is.False, "it sees the target");
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Attack, false, _self, lastSeen, tactics), Is.False);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Patrol, false, _self, lastSeen, tactics), Is.False);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, null, tactics), Is.False, "nowhere to throw");
        }

        [Test]
        public void ABotThrowsOnlyBetweenTheNearestAndFarthestDistances()
        {
            var tactics = new BotTacticsSpec(grenadeMinDistance: 8f, grenadeMaxDistance: 20f);

            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(7.9f, 1f, 0f), tactics), Is.False, "too near");
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(8f, 1f, 0f), tactics), Is.True);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(20f, 1f, 0f), tactics), Is.True);
            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(20.1f, 1f, 0f), tactics), Is.False, "too far");
            Assert.That(
                ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(0f, 30f, 12f), tactics),
                Is.True,
                "the distance is measured on the ground");
        }

        [Test]
        public void ABotThatMayNotThrowGrenadesDoesNot()
        {
            var tactics = TuningJson.ParseBots("{\"throwGrenades\": false}", "bots.json").Tactics;

            Assert.That(ServerBotAgent.WantsGrenade(EnemyState.Chase, false, _self, new Vector3(12f, 1f, 0f), tactics), Is.False);
        }

        [TestCase(12f, 0f, 0f)]
        [TestCase(8f, 0f, 5f)]
        [TestCase(18f, 3f, -4f)]
        [TestCase(10f, -2f, 0f)]
        public void TheAimedGrenadePassesOverTheTarget(float x, float y, float z)
        {
            var spec = GrenadeSpec.Default;
            var eye = new Vector3(0f, 1.6f, 0f);
            var target = new Vector3(x, y, z);

            Assert.That(ServerBotAgent.TryAimGrenade(eye, target, spec, ServerGrenadeFlight.Gravity, out var direction), Is.True);

            Assert.That(direction.Length(), Is.EqualTo(1f).Within(1e-4f));
            var release = eye + (direction * spec.ReleaseDistance);
            var velocity = (direction * spec.ThrowSpeed) + new Vector3(0f, spec.ThrowLiftSpeed, 0f);
            var flat = new Vector2(target.X - release.X, target.Z - release.Z);
            var flatVelocity = new Vector2(velocity.X, velocity.Z);
            Assert.That(Vector2.Dot(Vector2.Normalize(flat), Vector2.Normalize(flatVelocity)), Is.EqualTo(1f).Within(1e-4f), "toward the target");
            var seconds = flat.Length() / flatVelocity.Length();
            var height = release.Y + (velocity.Y * seconds) + (0.5f * ServerGrenadeFlight.Gravity * seconds * seconds);
            Assert.That(height, Is.EqualTo(y).Within(0.05f));
        }

        [Test]
        public void AGrenadeIsNotAimedBeyondItsReach()
        {
            var eye = new Vector3(0f, 1.6f, 0f);

            Assert.That(ServerBotAgent.TryAimGrenade(eye, new Vector3(200f, 0f, 0f), GrenadeSpec.Default, ServerGrenadeFlight.Gravity, out _), Is.False);
            Assert.That(ServerBotAgent.TryAimGrenade(eye, new Vector3(0f, 0f, 0f), GrenadeSpec.Default, ServerGrenadeFlight.Gravity, out _), Is.False, "straight below");
            Assert.That(ServerBotAgent.TryAimGrenade(eye, new Vector3(12f, float.NaN, 0f), GrenadeSpec.Default, ServerGrenadeFlight.Gravity, out _), Is.False);
        }

        [Test]
        public void ALowWallBetweenTheBotAndTheThreatIsLowCover()
        {
            var points = new List<CoverPoint>();

            ServerBotAgent.FindCover(_self, _threat, BotTacticsSpec.Default, Probe(1.2f), points);

            Assert.That(points, Is.Not.Empty);
            Assert.That(points.All(point => point.Position.X < _wallX), Is.True, "only this side of the wall hides the bot");
            Assert.That(points.All(point => point.Height == CoverHeight.Low), Is.True, "the bot sees over it when it stands");
            Assert.That(points.All(point => Math.Abs(point.Position.Y - _self.Y) < 1e-4f), Is.True, "at the height the brain measures");
            Assert.That(points.All(point => Vector3.Dot(point.Facing, Vector3.UnitX) > 0f), Is.True, "facing the threat");
            Assert.That(points.Any(point => Vector3.Distance(point.Position, _self) < 1e-4f), Is.True, "where the bot already stands");
        }

        [Test]
        public void AWallTallerThanTheBotIsHighCover()
        {
            var points = new List<CoverPoint>();

            ServerBotAgent.FindCover(_self, _threat, BotTacticsSpec.Default, Probe(2.5f), points);

            Assert.That(points, Is.Not.Empty);
            Assert.That(points.All(point => point.Height == CoverHeight.High), Is.True);
        }

        [Test]
        public void ThereIsNoCoverWithoutAWallOrWhereTheBotCannotStand()
        {
            var points = new List<CoverPoint>();

            ServerBotAgent.FindCover(_self, _threat, BotTacticsSpec.Default, Probe(0f), points);
            Assert.That(points, Is.Empty, "nothing blocks the line");

            var nowhere = new ServerBotCoverProbe((_, _) => null, (_, _) => true, _centerHeight, _hideHeight, _peekHeight);
            ServerBotAgent.FindCover(_self, _threat, BotTacticsSpec.Default, nowhere, points);
            Assert.That(points, Is.Empty, "no place to stand");
        }

        [Test]
        public void TheSearchCoversTheRingsAndDirectionsOfTheTuning()
        {
            var tried = new List<Vector3>();
            var probe = new ServerBotCoverProbe(
                (_, candidate) =>
                {
                    tried.Add(candidate);
                    return null;
                },
                (_, _) => true,
                _centerHeight,
                _hideHeight,
                _peekHeight);
            var tactics = new BotTacticsSpec(coverSearchRadius: 6f, coverSearchStep: 2f, coverSearchDirections: 4);

            ServerBotAgent.FindCover(_self, _threat, tactics, probe, new List<CoverPoint>());

            Assert.That(tried, Has.Count.EqualTo(1 + (3 * 4)), "the spot itself and 3 rings of 4");
            Assert.That(tried.Max(point => new Vector2(point.X, point.Z).Length()), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(tried.All(point => Math.Abs(point.Y) < 1e-4f), Is.True, "searched at the feet");
        }

        [Test]
        public void AHitBotHidesBehindTheCoverItFound()
        {
            var agent = Agent(NetBotSpec.Default);
            var perception = Hit(1f);

            Assert.That(agent.PrepareCover(perception, Probe(1.2f)), Is.True);
            var decision = agent.Brain.Tick(_tickSeconds, perception);

            Assert.That(agent.Brain.State, Is.EqualTo(EnemyState.TakeCover));
            Assert.That(agent.Brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.Hiding), "it already stands behind the wall");
            Assert.That(decision.Crouch, Is.True);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void ABotKeepsItsCoverWhileItHides()
        {
            var agent = Agent(NetBotSpec.Default);
            agent.PrepareCover(Hit(1f), Probe(1.2f));
            agent.Brain.Tick(_tickSeconds, Hit(1f));
            var point = agent.Brain.CurrentCoverPoint;

            Assert.That(agent.PrepareCover(Hit(1f), Probe(0f)), Is.False, "no new search while in cover");
            Assert.That(agent.Brain.CurrentCoverPoint, Is.EqualTo(point));
        }

        [Test]
        public void AnUnhurtBotDoesNotLookForCover()
        {
            var agent = Agent(NetBotSpec.Default);
            var unhurt = new EnemyPerception(_self, _threat, true, false, null, false, null, 1f);

            Assert.That(agent.PrepareCover(unhurt, Probe(1.2f)), Is.False);
            agent.Brain.Tick(_tickSeconds, unhurt);
            Assert.That(agent.Brain.State, Is.Not.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void AWeakBotLooksForCoverWithoutBeingHit()
        {
            var agent = Agent(NetBotSpec.Default);
            var weak = new EnemyPerception(_self, _threat, true, false, null, false, null, 0.2f);

            Assert.That(agent.PrepareCover(weak, Probe(1.2f)), Is.True);
        }

        [Test]
        public void ABotLooksForCoverAgainOnlyAfterTheSearchInterval()
        {
            var agent = Agent(TuningJson.ParseBots("{\"coverSearchSeconds\": 1}", "bots.json"));
            var probe = Probe(0f);

            Assert.That(agent.PrepareCover(Hit(1f), probe), Is.False, "nothing found");
            agent.Clock += 0.5f;
            Assert.That(agent.PrepareCover(Hit(1f), Probe(1.2f)), Is.False, "too soon");
            agent.Clock += 0.5f;
            Assert.That(agent.PrepareCover(Hit(1f), Probe(1.2f)), Is.True);
        }

        [Test]
        public void ABotThatMayNotTakeCoverStandsItsGround()
        {
            var agent = Agent(TuningJson.ParseBots("{\"takeCover\": false}", "bots.json"));

            Assert.That(agent.PrepareCover(Hit(1f), Probe(1.2f)), Is.False);
            agent.Brain.Tick(_tickSeconds, Hit(1f));
            Assert.That(agent.Brain.State, Is.Not.EqualTo(EnemyState.TakeCover));
        }

        [Test]
        public void ABotSwingsAtATargetWithinReach()
        {
            var agent = Agent(NetBotSpec.Default);
            var swing = new MeleeSwing(MeleeSpec.Default);
            agent.UseMelee(swing);
            agent.EnableMelee(true);

            var decision = agent.Brain.Tick(_tickSeconds, Seeing(new Vector3(1.5f, 1f, 0f)));

            Assert.That(agent.WantsSwing, Is.True);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.MoveTo, Is.Null);
            Assert.That(decision.LookAt, Is.EqualTo(new Vector3(1.5f, 1f, 0f)));
        }

        [Test]
        public void ABotDoesNotSwingOutOfReachOrWhenTheRulesForbidIt()
        {
            var agent = Agent(NetBotSpec.Default);
            agent.UseMelee(new MeleeSwing(MeleeSpec.Default));
            agent.EnableMelee(true);

            agent.Brain.Tick(_tickSeconds, Seeing(new Vector3(5f, 1f, 0f)));
            Assert.That(agent.WantsSwing, Is.False, "out of reach");

            agent.EnableMelee(false);
            agent.Brain.Tick(_tickSeconds, Seeing(new Vector3(1.5f, 1f, 0f)));
            Assert.That(agent.WantsSwing, Is.False, "melee is off in the rules");
        }

        [Test]
        public void ABotThatMayNotSwingDoesNot()
        {
            var agent = Agent(TuningJson.ParseBots("{\"meleeAttacks\": false}", "bots.json"));
            agent.UseMelee(new MeleeSwing(MeleeSpec.Default));
            agent.EnableMelee(true);

            agent.Brain.Tick(_tickSeconds, Seeing(new Vector3(1.5f, 1f, 0f)));

            Assert.That(agent.WantsSwing, Is.False);
        }

        [Test]
        public void TheSwingIsAddedToTheBrainOnceAndCanBeTakenAway()
        {
            var agent = Agent(NetBotSpec.Default);
            var swing = new MeleeSwing(MeleeSpec.Default);

            agent.UseMelee(swing);
            agent.UseMelee(swing);
            Assert.That(agent.Brain.Behaviors, Has.Count.EqualTo(1));
            Assert.That(agent.MeleeBehavior.Swing, Is.SameAs(swing));

            agent.UseMelee(null);
            Assert.That(agent.Brain.Behaviors, Is.Empty);
            Assert.That(agent.WantsSwing, Is.False);
        }

        [Test]
        public void AGrenadeWaitsForTheIntervalAfterAThrow()
        {
            var agent = Agent(TuningJson.ParseBots("{\"grenadeIntervalSeconds\": 4}", "bots.json"));
            Assert.That(agent.CanThrowGrenade, Is.True);

            agent.NoteGrenadeThrown();
            Assert.That(agent.CanThrowGrenade, Is.False);
            agent.Clock = 3.9f;
            Assert.That(agent.CanThrowGrenade, Is.False);
            agent.Clock = 4f;
            Assert.That(agent.CanThrowGrenade, Is.True);
        }

        // x = 2 に高さ wallTop の壁が立つ世界。置ける所はどこでも探した点のまま
        private static ServerBotCoverProbe Probe(float wallTop)
        {
            return new ServerBotCoverProbe(
                (_, candidate) => candidate,
                (from, to) => CrossesWall(from, to, wallTop),
                _centerHeight,
                _hideHeight,
                _peekHeight);
        }

        private static bool CrossesWall(Vector3 from, Vector3 to, float wallTop)
        {
            if ((from.X - _wallX) * (to.X - _wallX) >= 0f)
            {
                return false;
            }

            var t = (_wallX - from.X) / (to.X - from.X);
            return from.Y + ((to.Y - from.Y) * t) <= wallTop;
        }

        private static EnemyPerception Hit(float healthFraction)
        {
            return new EnemyPerception(_self, _threat, true, false, null, true, _threat, healthFraction);
        }

        private static EnemyPerception Seeing(Vector3 target)
        {
            return new EnemyPerception(_self, target, true, false, null);
        }

        private static ServerBotAgent Agent(NetBotSpec spec)
        {
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f)));
            var navigation = new ServerBotNavigation(
                map.Navigation, map.NavMesh.Links, map.Ladders.Ladders, map.World, _ => false, spec.NavSampleRadius, 1f);
            var agent = new ServerBotAgent(1, spec, navigation, Array.Empty<Vector3>(), new Random(1), new SeeEverything());
            agent.Restart(true);
            return agent;
        }

        private sealed class SeeEverything : ISightBlocker
        {
            public bool Blocks(Vector3 from, Vector3 to) => false;
        }
    }
}
