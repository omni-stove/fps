using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // bots.json の archetype で選んだ種類の判断を、Bot の判断 (ServerBotAgent) に直に知覚を渡して確かめる
    public sealed class ServerBotArchetypeTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly Vector3 _self = new Vector3(0f, 1f, 0f);

        [Test]
        public void ARiflemanHasNoArchetypeBehaviorAndKeepsItsTactics()
        {
            var agent = Agent("Rifleman");

            Assert.That(agent.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(agent.Brain.Behaviors, Is.Empty);
            Assert.That(agent.UsesTactics, Is.True);
            Assert.That(agent.AllowsMelee, Is.True);
            Assert.That(agent.ShotDamageMultiplier, Is.EqualTo(1f));
            Assert.That(agent.RaisedShield, Is.Null);
            Assert.That(agent.GaitMultiplier, Is.Null);
            Assert.That(agent.SprintAllowed, Is.True);
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.None));
        }

        [TestCase("Shield", typeof(ShieldEnemyBehavior))]
        [TestCase("Sniper", typeof(SniperEnemyBehavior))]
        [TestCase("Charger", typeof(ChargerEnemyBehavior))]
        [TestCase("Bomber", typeof(BomberEnemyBehavior))]
        public void EachArchetypeAddsItsBehaviorAndDropsTheRiflemanTactics(string archetype, Type behavior)
        {
            var agent = Agent(archetype);

            Assert.That(agent.Brain.Behaviors.Select(b => b.GetType()), Is.EqualTo(new[] { behavior }));
            Assert.That(agent.UsesTactics, Is.False);
            Assert.That(agent.CanThrowGrenade, Is.False);
        }

        [TestCase("Sniper", true)]
        [TestCase("Shield", false)]
        [TestCase("Charger", false)]
        [TestCase("Bomber", false)]
        public void OnlyTheRiflemanAndTheSniperSwingTheirMelee(string archetype, bool allowed)
        {
            Assert.That(Agent(archetype).AllowsMelee, Is.EqualTo(allowed));
        }

        [Test]
        public void AChargerWindsUpAtATargetInReachThenCharges()
        {
            var agent = Agent("Charger");
            var target = new Vector3(5f, 1f, 0f);

            agent.Brain.Tick(_tickSeconds, Seen(target));

            Assert.That(agent.ChargerBehavior.Phase, Is.EqualTo(ChargerPhase.WindingUp));
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Warning));
            Assert.That(agent.IsCharging, Is.False);

            TickFor(agent, ChargerSpec.DefaultWindupSeconds, target);

            Assert.That(agent.IsCharging, Is.True);
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Attacking));
            Assert.That(agent.ChargeDirection.Value.X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(agent.ChargeTravelled, Is.Zero);
            Assert.That(agent.TakePhaseLog(), Has.Some.Contains("charger player=1 phase=Charging prev=WindingUp"));
        }

        [Test]
        public void AChargeThatHitsAWallStunsTheCharger()
        {
            var agent = ChargingAgent();
            agent.AddChargeTravel(3f);

            Assert.That(agent.EndCharge(ChargeEnd.Obstacle), Is.True);

            Assert.That(agent.ChargerBehavior.Phase, Is.EqualTo(ChargerPhase.Stunned));
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Exposed));
            Assert.That(agent.ChargeTravelled, Is.EqualTo(3f));
        }

        [Test]
        public void ACancelledChargeIsReadyAgain()
        {
            var agent = ChargingAgent();

            Assert.That(agent.CancelCharge(), Is.True);

            Assert.That(agent.ChargerBehavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(agent.IsCharging, Is.False);
        }

        [Test]
        public void ARespawnedChargerForgetsItsCharge()
        {
            var agent = ChargingAgent();
            agent.MarkDead();

            agent.Restart(true);

            Assert.That(agent.ChargerBehavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(agent.TakePhaseLog(), Is.Empty);
        }

        [Test]
        public void ABomberLightsItsFuseNearATargetAndBurnsItOut()
        {
            var agent = Agent("Bomber");
            var target = new Vector3(2f, 1f, 0f);

            agent.Brain.Tick(_tickSeconds, Seen(target));

            Assert.That(agent.BomberPhase, Is.EqualTo(BomberPhase.Fusing));
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Warning));
            Assert.That(agent.IsFuseBurnt, Is.False);

            TickFor(agent, BomberSpec.DefaultFuseSeconds, target);

            Assert.That(agent.IsFuseBurnt, Is.True);
            Assert.That(agent.Detonate(), Is.EqualTo(BomberPhase.Fusing));
            Assert.That(agent.BomberPhase, Is.EqualTo(BomberPhase.Exploded));
            Assert.That(agent.DeathBlast(), Is.Null, "it already went off");
        }

        [Test]
        public void ABomberKilledBeforeItsFuseMakesTheSmallBlast()
        {
            var agent = Agent("Bomber");

            Assert.That(agent.DeathBlast(), Is.SameAs(BomberSpec.DefaultDeathBlast));
            Assert.That(agent.DeathBlast(), Is.Null, "it goes off once");
        }

        [Test]
        public void ABomberKilledWithItsFuseLitMakesTheFullBlast()
        {
            var agent = Agent("Bomber");
            agent.Brain.Tick(_tickSeconds, Seen(new Vector3(2f, 1f, 0f)));

            Assert.That(agent.DeathBlast(), Is.SameAs(BomberSpec.DefaultBlast));
        }

        [Test]
        public void ABomberRunsFasterThanItWalks()
        {
            var agent = Agent("Bomber");

            Assert.That(agent.GaitMultiplier, Is.EqualTo(BomberSpec.DefaultRunSpeedMultiplier));
        }

        [Test]
        public void ARaisedShieldBlocksWithTheShieldBearersSlowStep()
        {
            var agent = Agent("Shield");

            Assert.That(agent.RaisedShield, Is.Not.Null);
            Assert.That(agent.RaisedShield.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(agent.RaisedShield.Bash, Is.SameAs(ShieldSpec.DefaultBash));
            Assert.That(agent.GaitMultiplier, Is.EqualTo(ShieldSpec.DefaultApproachSpeedMultiplier));
            Assert.That(agent.SprintAllowed, Is.False);
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.None));
        }

        [Test]
        public void ADeadShieldBearerHasNoShield()
        {
            var agent = Agent("Shield");
            agent.MarkDead();

            Assert.That(agent.RaisedShield, Is.Null);
        }

        [Test]
        public void BulletsDoNotLowerTheShieldButMeleeAndExplosionsDo()
        {
            var agent = Agent("Shield");

            Assert.That(agent.LowerShield(DamageKind.Bullet), Is.Zero);
            Assert.That(agent.RaisedShield, Is.Not.Null);

            Assert.That(agent.LowerShield(DamageKind.Melee), Is.EqualTo(ShieldSpec.DefaultMeleeLowerSeconds));
            Assert.That(agent.RaisedShield, Is.Null);
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Exposed));
            Assert.That(agent.GaitMultiplier, Is.Null, "it walks at the normal pace while the shield is down");
        }

        [Test]
        public void ALoweredShieldIsRaisedAgainAfterItsSeconds()
        {
            var agent = Agent("Shield");
            agent.LowerShield(DamageKind.Explosion);

            var raised = false;
            for (var elapsed = 0f; elapsed < ShieldSpec.DefaultExplosionLowerSeconds + _tickSeconds && !raised; elapsed += _tickSeconds)
            {
                raised = agent.TickGuard(_tickSeconds);
            }

            Assert.That(raised, Is.True);
            Assert.That(agent.RaisedShield, Is.Not.Null);
        }

        [Test]
        public void AShieldBearerBashesATargetInReachOnceItFacesIt()
        {
            var agent = Agent("Shield");
            var target = new Vector3(1f, 1f, 0f);
            agent.Brain.Tick(_tickSeconds, Seen(target));

            Assert.That(agent.TickBash(_tickSeconds, false, out var notStarted, out _), Is.False);
            Assert.That(notStarted, Is.False, "it waits until the target is in its arc");

            agent.TickBash(_tickSeconds, true, out var started, out var spec);
            Assert.That(started, Is.True);
            Assert.That(spec, Is.SameAs(ShieldSpec.DefaultBash));

            var struck = false;
            for (var elapsed = _tickSeconds; elapsed < ShieldSpec.DefaultBashImpactSeconds + _tickSeconds && !struck; elapsed += _tickSeconds)
            {
                agent.Brain.Tick(_tickSeconds, Seen(target));
                struck = agent.TickBash(_tickSeconds, true, out _, out _);
            }

            Assert.That(struck, Is.True);
        }

        [Test]
        public void ASniperAimsThenFiresWithItsStrongerShot()
        {
            var agent = Agent("Sniper", "\"attackRange\": 40");
            var target = new Vector3(35f, 1f, 0f);

            agent.Brain.Tick(_tickSeconds, Seen(target));

            Assert.That(agent.SniperBehavior.Phase, Is.EqualTo(SniperPhase.Aiming));
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Warning));

            TickFor(agent, SniperSpec.DefaultAimSeconds, target);

            Assert.That(agent.SniperBehavior.Phase, Is.EqualTo(SniperPhase.Firing));
            Assert.That(agent.Cue, Is.EqualTo(BotArchetypeCue.Attacking));
            Assert.That(agent.ShotDamageMultiplier, Is.EqualTo(SniperSpec.DefaultShotDamageMultiplier));
        }

        [Test]
        public void ASniperMovesToTheNextPointOfItsRouteAfterAShot()
        {
            var agent = Agent("Sniper", "\"attackRange\": 40");
            var perches = new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f) };
            agent.SetRoute(perches);
            var target = new Vector3(35f, 1f, 0f);
            agent.Brain.Tick(_tickSeconds, Seen(target));
            TickFor(agent, SniperSpec.DefaultAimSeconds, target);

            Assert.That(agent.ConfirmShot(Vector3.Zero), Is.True);

            Assert.That(agent.SniperBehavior.Phase, Is.EqualTo(SniperPhase.Relocating));
            Assert.That(agent.SniperBehavior.PerchIndex, Is.EqualTo(1));
        }

        [Test]
        public void ABotThatIsNotASniperDoesNotConfirmShots()
        {
            Assert.That(Agent("Rifleman").ConfirmShot(Vector3.Zero), Is.False);
        }

        [TestCase(0.5f, 1f, 0.5f, false)]
        [TestCase(1f, 1f, 1f, false)]
        [TestCase(1.6f, 1f, 0.8f, true)]
        [TestCase(3f, 1f, 1f, true)]
        public void TheGaitScalesTheWalkingInput(float multiplier, float forward, float expected, bool sprint)
        {
            // 歩き 4m/s・走り 8m/s
            var move = ServerBotAgent.GaitMove(new Vector2(0f, forward), multiplier, 4f, 8f, out var sprinting);

            Assert.That(move.Y, Is.EqualTo(expected).Within(1e-5f));
            Assert.That(sprinting, Is.EqualTo(sprint));
        }

        [Test]
        public void WithoutAFasterSprintTheGaitStaysAtTheWalk()
        {
            var move = ServerBotAgent.GaitMove(new Vector2(1f, 0f), 2f, 4f, 4f, out var sprinting);

            Assert.That(move.X, Is.EqualTo(1f));
            Assert.That(sprinting, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ABadGaitIsRefused(float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerBotAgent.GaitMove(Vector2.One, multiplier, 4f, 8f, out _));
        }

        private static EnemyPerception Seen(Vector3 target)
        {
            return new EnemyPerception(_self, target, true, false, null);
        }

        private static void TickFor(ServerBotAgent agent, float seconds, Vector3 target)
        {
            for (var elapsed = 0f; elapsed < seconds + _tickSeconds; elapsed += _tickSeconds)
            {
                agent.Brain.Tick(_tickSeconds, Seen(target));
            }
        }

        private static ServerBotAgent ChargingAgent()
        {
            var agent = Agent("Charger");
            var target = new Vector3(5f, 1f, 0f);
            agent.Brain.Tick(_tickSeconds, Seen(target));
            TickFor(agent, ChargerSpec.DefaultWindupSeconds, target);
            Assert.That(agent.IsCharging, Is.True);
            return agent;
        }

        private static ServerBotAgent Agent(string archetype, string extra = null)
        {
            var json = "{\"archetype\": \"" + archetype + "\"" + (extra != null ? ", " + extra : string.Empty) + "}";
            var spec = TuningJson.ParseBots(json, "bots.json");
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
