using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // 移動・ルール・戦闘を同じホストで動かし、射撃・巻き戻し・壁・爆発を確かめる
    public sealed partial class ServerCombatTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        // 床 (y=0.5 が上面) に足を付けた体の原点 (カプセルの中心、高さ 2)
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private static readonly int[] _wallHealth = { 20 };
        private static readonly bool[] _wallGrounded = { true };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerCombat _combat;
        private int _inputTick;
        private int _shotIndex;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _inputTick = 0;
            _shotIndex = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void ShotsKillAndTheKillIsWrittenAsJson()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 4; i++)
            {
                FireAt(1, Body(2).Center);
                Step(0.2f);
            }

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var kill = Lines(_json).Single(line => line.Contains("\"type\":\"kill\"", StringComparison.Ordinal));
            Assert.That(kill, Does.Contain("\"killerId\":\"1\"").And.Contain("\"victimId\":\"2\"").And.Contain("\"teamKill\":\"false\""));
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=0.00 zone=Body dmg=25 hp=75 outcome=Damaged"));

            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(_transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Killed = m.Killed }).Last().Killed, Is.True);
            var taken = _transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { HasSource = m.HasSource, SourcePosition = m.SourcePosition, Amount = m.Amount });
            Assert.That(taken, Has.Count.EqualTo(4));
            Assert.That(taken[0].SourcePosition, Is.EqualTo(_alphaSpawn));
            Assert.That(_transport.Read<ShotMessage>(1, m => m), Is.Empty, "the shooter shows its own shot without waiting");
            Assert.That(_transport.Read<ShotMessage>(2, m => new ShotMessage { ImpactIsCharacter = m.ImpactIsCharacter }).All(s => s.ImpactIsCharacter), Is.True);
        }

        [Test]
        public void EveryPelletOfAShotgunHitsButTheShooterAndTheTargetAreToldOnce()
        {
            // 狭い円錐にして、10 m 先の胴にすべてのペレットが当たるようにする
            var shotgun = new WeaponSpec(
                TestRules.RifleId,
                "weapon.shotgun",
                WeaponCategory.AssaultRifle,
                round: new RoundSpec(range: 40f, pelletCount: 8, pelletSpreadDegrees: 0.5f),
                impact: new ImpactSpec(10));
            Start(Rules(), rifle: shotgun);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 80), _log.ToString());
            var confirms = _transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Killed = m.Killed });
            Assert.That(confirms, Has.Count.EqualTo(1));
            Assert.That(confirms[0].Damage, Is.EqualTo(80));
            Assert.That(confirms[0].Killed, Is.False);
            var taken = _transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { Amount = m.Amount });
            Assert.That(taken, Has.Count.EqualTo(1));
            Assert.That(taken[0].Amount, Is.EqualTo(80));
            var shot = _transport.Read<ShotMessage>(2, CopySegments).Single();
            Assert.That(shot.SegmentCount, Is.EqualTo(8));
        }

        [Test]
        public void ShotgunPelletsThatKillAreToldAsOneKill()
        {
            var shotgun = new WeaponSpec(
                TestRules.RifleId,
                "weapon.shotgun",
                WeaponCategory.AssaultRifle,
                round: new RoundSpec(range: 40f, pelletCount: 8, pelletSpreadDegrees: 0.5f),
                impact: new ImpactSpec(20));
            Start(Rules(), rifle: shotgun);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var confirms = _transport.Read<HitConfirmMessage>(1, m => new HitConfirmMessage { Damage = m.Damage, Killed = m.Killed });
            Assert.That(confirms, Has.Count.EqualTo(1));
            Assert.That(confirms[0].Killed, Is.True);
            Assert.That(confirms[0].Damage, Is.GreaterThanOrEqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_transport.Read<DamageTakenMessage>(2, m => new DamageTakenMessage { Amount = m.Amount }), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheShotIsJudgedAgainstWhereTheTargetWasWhenTheShooterSawIt()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;
            var seen = Body(2).Center;

            Walk(2, new Vector2(0f, 1f), 11, InputButtons.Sprint);
            Assert.That(Vector3.Distance(Body(2).Center, seen), Is.GreaterThan(1f), "the target must have left the spot");

            FireAt(1, seen, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] hit attacker=1 victim=2 rewindTicks=11.00"));
        }

        [Test]
        public void AShotAtWhereTheTargetIsNowMissesWhenTheShooterSawAnOlderTick()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;

            Walk(2, new Vector2(0f, 1f), 11, InputButtons.Sprint);
            FireAt(1, Body(2).Center, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void RewindingStopsAtTheLimit()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var seenTick = (int)_host.TickCount;
            var seen = Body(2).Center;

            Walk(2, new Vector2(0f, 1f), LagCompensator.DefaultMaxRewindTicks + 30);
            FireAt(1, seen, seenTick);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), _log.ToString());
        }

        [Test]
        public void AWallStopsTheBullet()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            var shot = _transport.Read<ShotMessage>(2, m => new ShotMessage { HasImpact = m.HasImpact, ImpactIsCharacter = m.ImpactIsCharacter, ImpactPoint = m.ImpactPoint }).Single();
            Assert.That(shot.HasImpact, Is.True);
            Assert.That(shot.ImpactIsCharacter, Is.False);
            Assert.That(shot.ImpactPoint.X, Is.EqualTo(-0.25f).Within(1e-3f));
        }

        [Test]
        public void APenetratingRoundGoesThroughTheWallAndHurtsLess()
        {
            var plain = TestRules.Rifle();
            var rifle = new WeaponSpec(
                plain.Id,
                plain.NameKey,
                plain.Category,
                plain.Trigger,
                plain.Ammo,
                new RoundSpec(range: plain.Range, penetration: new PenetrationSpec(1, 0.5f, 0.6f)),
                new ImpactSpec(40),
                plain.Aim);
            Start(Rules(), wall: true, rifle: rifle);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 20), _log.ToString());
            var shot = _transport.Read<ShotMessage>(2, CopySegments).Single();
            Assert.That(shot.SegmentCount, Is.EqualTo(2));
        }

        [Test]
        public void ABrokenWallLetsTheNextBulletThrough()
        {
            Start(Rules().WithDestruction(Destruction.On), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Destruction] structure=0 destroyed=0 collapsed=-"));
            var state = _transport.Read<StructureStateMessage>(2, m => m).Single();
            Assert.That(state.StructureId, Is.EqualTo(0));

            Step(0.2f);
            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25));
        }

        [Test]
        public void AnExplosionBreakingManyPiecesOfOneStructureSendsItsStateOnce()
        {
            Start(Rules().WithDestruction(Destruction.On), wall: true, destructibleWall: true, wallPieces: 2);
            Join(1);
            Join(2);
            Step(0.2f);

            _combat.Explode(new Vector3(-1f, 2.5f, 0f), GrenadeSpec.Default.Impact.Explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(_log.ToString(), Does.Contain("[Destruction] structure=0"));
            Assert.That(_context.Map.Collision.IsGroupEnabled(MapColliderGroups.Piece(0, 0)), Is.False);
            Assert.That(_context.Map.Collision.IsGroupEnabled(MapColliderGroups.Piece(0, 1)), Is.False);
            Assert.That(_transport.Read<StructureStateMessage>(2, m => m), Has.Count.EqualTo(1));
        }

        [Test]
        public void ALatePlayerIsToldWhichPiecesAreBroken()
        {
            Start(Rules().WithDestruction(Destruction.On), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            FireAt(1, Body(2).Center);

            Join(3);

            Assert.That(_transport.Read<StructureStateMessage>(3, m => m), Has.Count.EqualTo(1));
        }

        [Test]
        public void WithoutDestructionTheWallStaysAndIsNotSent()
        {
            Start(Rules(), wall: true, destructibleWall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            Assert.That(_transport.Read<StructureStateMessage>(2, m => m), Is.Empty);
            Assert.That(_context.Map.Collision.IsGroupEnabled(MapColliderGroups.Piece(0, 0)), Is.True);
        }

        [Test]
        public void ShotsFromBehindAWallAreRejected()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, new Vector3(-1.5f, _alphaSpawn.Y, 0f), 0f, _host.TickCount);
            var origin = Body(1).EyePosition + new Vector3(2f, 0f, 0f);
            Fire(1, origin, Body(2).Center - origin, (int)_host.TickCount);

            Assert.That(_log.ToString(), Does.Contain("[Net] rejected shot attacker=1 reason=OriginBlocked"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AHitscanShotIsSentAsOneSegment()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, Body(2).Center);

            var shot = _transport.Read<ShotMessage>(2, CopySegments).Single();
            Assert.That(shot.SegmentCount, Is.EqualTo(1));
            Assert.That(shot.HasImpact, Is.True);
            Assert.That(shot.ImpactIsCharacter, Is.True);
            Assert.That(shot.HasImpactZone, Is.True);
            Assert.That(shot.WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void ChargeSentForAWeaponWithoutChargeIsIgnored()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var eye = Body(1).EyePosition;

            Send(1, new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = Body(2).Center - eye,
                ClientTick = (int)_host.TickCount,
                ShotIndex = _shotIndex++,
                ChargeFraction = 1f,
            });

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), _log.ToString());
            Assert.That(Snapshot(1).Owner.Weapon.ChargeFraction, Is.EqualTo(0f));
        }

        [Test]
        public void ReleasingTheTriggerFiresNothing()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var eye = Body(1).EyePosition;

            Send(1, new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = Body(2).Center - eye,
                ClientTick = (int)_host.TickCount,
                ShotIndex = _shotIndex,
                Trigger = TriggerEdge.Released,
            });

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(_transport.Read<ShotMessage>(2, m => m), Is.Empty);
        }

        [Test]
        public void SwitchingTheAmmoTypeOfAWeaponWithoutTypesChangesNothing()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new WeaponActionRequestMessage { Action = WeaponAction.SwitchAmmoType });
            FireAt(1, Body(2).Center);

            Assert.That(_log.ToString(), Does.Not.Contain("[Net] ammo-type"));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25));
            var weapon = Snapshot(1).Owner.Weapon;
            Assert.That(weapon.AmmoTypeIndex, Is.EqualTo(0));
            Assert.That(weapon.Heat, Is.EqualTo(0f));
        }

        [Test]
        public void SwitchingTheAmmoTypeNeedsAReloadAndThenShotsUseTheServersType()
        {
            var piercing = new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a3", "ammo.dmr.armorPiercing", impact: new ImpactSpec(60));
            var rifle = new WeaponSpec(
                TestRules.RifleId,
                "weapon.rifle",
                WeaponCategory.AssaultRifle,
                ammo: new AmmoSpec(30, 90, 2f),
                impact: new ImpactSpec(25),
                ammoTypes: new[] { new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a2", "ammo.dmr.standard"), piercing });
            Start(Rules(), rifle: rifle);
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(Snapshot(1).Owner.Weapon.AmmoTypeIndex, Is.EqualTo(1), "the first type is loaded");
            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25));

            _host.OnMessage(new ConnectionId(1), new WeaponActionRequestMessage { Action = WeaponAction.SwitchAmmoType });

            var switched = Snapshot(1).Owner.Weapon;
            Assert.That(switched.AmmoTypeIndex, Is.EqualTo(2));
            Assert.That(switched.AmmoInMagazine, Is.Zero, "the shared magazine is emptied");
            Assert.That(switched.ReserveAmmo, Is.EqualTo(90 + 29));
            Assert.That(_log.ToString(), Does.Contain("[Net] ammo-type player=1 weapon=" + TestRules.RifleId + " index=2"));

            Step(0.2f);
            FireAt(1, Body(2).Center);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25), "no rounds until reloaded");

            Step(2.2f);
            Assert.That(Snapshot(1).Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
            var eye = Body(1).EyePosition;
            _host.OnMessage(new ConnectionId(1), new FireRequestMessage
            {
                WeaponId = TestRules.RifleId,
                Origin = eye,
                Direction = Body(2).Center - eye,
                ClientTick = (int)_host.TickCount,
                ShotIndex = _shotIndex++,
                AmmoTypeIndex = 1,
            });

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 25 - 60), "the server's type, not the request's");
        }

        [Test]
        public void AWeaponExplosionIsCreditedToTheWeapon()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var explosion = new ExplosionSpec(new ExplosionDamage(NetMatchSettings.DefaultMaxHealth * 2, 4f, 1f));

            _combat.Explode(victim.Position + new Vector3(-(victim.Radius + 0.5f), 0f, 0f), explosion, 1, TestRules.RifleId);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_transport.Read<KilledMessage>(2, Copy).Single().WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(_transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { WeaponId = m.WeaponId }).Single().WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(_log.ToString(), Does.Contain("[Combat] net exploded thrower=1").And.Not.Contain("[Grenade] net exploded"));
        }

        [Test]
        public void AnExplodingRoundHurtsTheBodiesNearWhereItHitByDistance()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(60, 5f));
            Start(Rules(), rifle: ExplodingRifle(explosion));
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var floor = new Vector3(victim.Position.X - 1.5f, 0.5f, 0f);

            FireAt(1, floor);

            var center = floor + new Vector3(0f, ImpactResolver.DetonationStandoff, 0f);
            var distance = ServerCombat.DistanceToCapsule(
                center, victim.Position + new Vector3(0f, victim.Shape.BottomY, 0f), victim.Position + new Vector3(0f, victim.Shape.TopY, 0f), victim.Radius);
            var expected = explosion.Damage.At(distance, false);
            Assert.That(expected, Is.GreaterThan(0).And.LessThan(explosion.Damage.MaxDamage), "the body is inside the radius but off the center");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - expected), _log.ToString());
            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth), "the shooter is out of the radius");
            var sent = _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center, HasThrower = m.HasThrower, ThrowerId = m.ThrowerId, WeaponId = m.WeaponId }).Single();
            Assert.That(sent.WeaponId, Is.EqualTo(TestRules.RifleId));
            Assert.That(sent.HasThrower, Is.True);
            Assert.That(sent.ThrowerId, Is.EqualTo(1));
            Assert.That(Vector3.Distance(sent.Center, center), Is.LessThan(1e-3f));
        }

        [Test]
        public void AKillByAnExplodingRoundIsCreditedToTheWeapon()
        {
            Start(Rules(), rifle: ExplodingRifle(new ExplosionSpec(new ExplosionDamage(NetMatchSettings.DefaultMaxHealth * 2, 4f, 2f))));
            Join(1);
            Join(2);
            Step(0.2f);

            FireAt(1, new Vector3(Body(2).Position.X - 1f, 0.5f, 0f));

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            var killed = _transport.Read<KilledMessage>(2, Copy).Single();
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void AWallBetweenAnExplodingRoundAndTheBodyBlocksIt()
        {
            var explosion = new ExplosionSpec(new ExplosionDamage(NetMatchSettings.DefaultMaxHealth, 8f));
            Start(Rules(), wall: true, rifle: ExplodingRifle(explosion));
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var center = new Vector3(-0.25f - ImpactResolver.DetonationStandoff, 1.5f, 0f);
            var distance = ServerCombat.DistanceToCapsule(
                center, victim.Position + new Vector3(0f, victim.Shape.BottomY, 0f), victim.Position + new Vector3(0f, victim.Shape.TopY, 0f), victim.Radius);

            FireAt(1, new Vector3(0f, 1.5f, 0f));

            Assert.That(explosion.Damage.At(distance, false), Is.GreaterThan(0), "without the wall it would hurt");
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Has.Count.EqualTo(1), _log.ToString());
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void ShotsUseTheMagazineAndAnEmptyOneReloads()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < 30; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
                Step(0.1f);
            }

            FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            Assert.That(_log.ToString(), Does.Contain("[Net] reload start client=1 auto=True ammo=0 reserve=90"));
            Step(2.1f);
            Assert.That(_log.ToString(), Does.Contain("[Net] reloaded client=1 ammo=30 reserve=60"));
        }

        [Test]
        public void TooFastShotsAreRejectedWithoutUsingAmmo()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            for (var i = 0; i < ShotBudget.DefaultBurst + 2; i++)
            {
                FireAt(1, Body(2).Center + new Vector3(0f, 0f, 3f));
            }

            Assert.That(_log.ToString(), Does.Contain("reason=FireRateExceeded"));
            var snapshot = Snapshot(1);
            Assert.That(snapshot.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30 - ShotBudget.DefaultBurst));
        }

        [Test]
        public void TheOwnerSnapshotCarriesTheWeaponsAndGrenades()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            var snapshot = Snapshot(1);

            Assert.That(snapshot.Owner.Weapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(snapshot.Owner.Weapon.WeaponCount, Is.EqualTo(2));
            Assert.That(snapshot.Owner.EquippedWeaponIds[0], Is.EqualTo(TestRules.RifleId));
            Assert.That(snapshot.Owner.Grenades.Enabled, Is.True);
            Assert.That(snapshot.Owner.Grenades.RemainingOf(GrenadeSpec.DefaultId), Is.EqualTo(2));
            Assert.That(snapshot.Players[0].WeaponId, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void SwitchingWeaponsChangesWhatTheServerAcceptsAfterTheSwitchTime()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new SwitchWeaponRequestMessage { WeaponId = TestRules.HandgunId });
            Fire(1, Body(1).EyePosition, Body(2).Center - Body(1).EyePosition, (int)_host.TickCount, TestRules.HandgunId);
            Assert.That(_log.ToString(), Does.Contain("reason=Switching"));

            Step(0.5f);
            Fire(1, Body(1).EyePosition, Body(2).Center - Body(1).EyePosition, (int)_host.TickCount, TestRules.HandgunId);
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth - 34));
        }

        [Test]
        public void AMismatchedShotIndexIsCorrected()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            _shotIndex = 5;

            FireAt(1, Body(2).Center);

            var corrected = _transport.Read<ShotIndexCorrectedMessage>(
                1, m => new ShotIndexCorrectedMessage { Generation = m.Generation, ClientIndex = m.ClientIndex, ServerIndex = m.ServerIndex }).Single();
            Assert.That(corrected.ClientIndex, Is.EqualTo(5));
            Assert.That(corrected.ServerIndex, Is.EqualTo(0));
            Assert.That(corrected.Generation, Is.EqualTo(1));
        }

        [TestCase(0.5f)]
        [TestCase(2.5f)]
        [TestCase(4f)]
        [TestCase(5.5f)]
        public void TheExplosionDamageFallsOffWithDistance(float distance)
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var center = victim.Position + new Vector3(-(victim.Radius + distance), 0f, 0f);

            _combat.Explode(center, GrenadeSpec.Default.Impact.Explosion, 1, ServerCombat.GrenadeWeaponId);

            var expected = GrenadeSpec.Default.Explosion.At(distance, false);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(Math.Max(0, NetMatchSettings.DefaultMaxHealth - expected)), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net exploded thrower=1"));
            Assert.That(_transport.Read<ExplosionMessage>(2, m => m), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheExplosionDoesNotReachBeyondItsRadius()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);

            var center = victim.Position + new Vector3(-(victim.Radius + GrenadeSpec.DefaultDamageRadius + 0.5f), 0f, 0f);
            _combat.Explode(center, GrenadeSpec.Default.Impact.Explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        [Test]
        public void AWallBetweenTheExplosionAndTheBodyBlocksIt()
        {
            Start(Rules(), wall: true);
            Join(1);
            Join(2);
            Step(0.2f);
            var victim = Body(2);
            var center = new Vector3(-1f, victim.Position.Y, 0f);
            var distance = ServerCombat.DistanceToCapsule(
                center, victim.Position + new Vector3(0f, victim.Shape.BottomY, 0f), victim.Position + new Vector3(0f, victim.Shape.TopY, 0f), victim.Radius);

            _combat.Explode(center, GrenadeSpec.Default.Impact.Explosion, 1, ServerCombat.GrenadeWeaponId);

            Assert.That(GrenadeSpec.Default.Explosion.At(distance, false), Is.GreaterThan(0), "without the wall it would hurt");
            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net blocked victim=2"));
        }

        [Test]
        public void AThrownGrenadeFliesBouncesAndExplodesAfterItsFuse()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = new Vector3(1f, 0f, 0f) });
            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net thrown player=1 remaining=1"));
            Step(0.5f);
            var snapshot = Snapshot(2);
            Assert.That(snapshot.Grenades.Count, Is.EqualTo(1));
            Assert.That(snapshot.Grenades[0].Position.X, Is.GreaterThan(_alphaSpawn.X + 2f));

            Step(GrenadeSpec.DefaultFuseSeconds);

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net exploded thrower=1"));
            Assert.That(
                _transport.Read<ExplosionMessage>(2, m => new ExplosionMessage { Center = m.Center }).Single().Center.Y,
                Is.EqualTo(0.5f + ServerGrenadeFlight.BodyRadius).Within(0.01f),
                "the grenade should roll on the floor");
        }

        [Test]
        public void AStickyGrenadeSticksToThePersonItHitsAndGoesOffOnThem()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { _sticky }));
            Join(1);
            Join(2);
            Step(0.2f);
            _context.Bodies.TryGet(2, out var target);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = _sticky.Id });
            Step(1f);

            var grenade = Snapshot(2).Grenades[0];
            Assert.That(grenade.IsStuck, Is.True);
            Assert.That(grenade.HasStuckTo, Is.True);
            Assert.That(grenade.StuckToId, Is.EqualTo(2));
            Assert.That(grenade.Position.X, Is.LessThan(target.Position.X), "it stays on the side it hit");

            Step(_sticky.FuseSeconds);

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_rules.Session.IsAlive(2), Is.False, "it goes off on the person it is stuck to");
        }

        [Test]
        public void GrenadesAreLimitedByCountAndInterval()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            var throwRequest = new ThrowGrenadeRequestMessage { Direction = new Vector3(0f, 1f, 1f) };

            _host.OnMessage(new ConnectionId(1), throwRequest);
            _host.OnMessage(new ConnectionId(1), throwRequest);
            Assert.That(_log.ToString(), Does.Contain("[Grenade] net rejected player=1 reason=TooSoon"));
            Step(1f);
            _host.OnMessage(new ConnectionId(1), throwRequest);
            Step(1f);
            _host.OnMessage(new ConnectionId(1), throwRequest);

            Assert.That(Lines(_log).Count(line => line.StartsWith("[Grenade] net thrown", StringComparison.Ordinal)), Is.EqualTo(2));
        }

        [Test]
        public void EachCarriedTypeHasItsOwnCountAndGoesOffWithItsImpact()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { GrenadeSpec.Default, _smoke }, new[] { GrenadeSpec.DefaultId, _smoke.Id }));
            Join(1);
            Join(2);
            Step(0.2f);

            var before = Snapshot(1).Owner.Grenades;
            Assert.That(before.Count, Is.EqualTo(2));
            Assert.That(before.RemainingOf(_smoke.Id), Is.EqualTo(2));

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = _smoke.Id });
            var owner = Snapshot(1).Owner.Grenades;
            Assert.That(owner.RemainingOf(_smoke.Id), Is.EqualTo(1));
            Assert.That(owner.RemainingOf(GrenadeSpec.DefaultId), Is.EqualTo(2), "throwing smoke leaves the frags");
            Assert.That(Snapshot(2).Grenades[0].TypeId, Is.EqualTo(_smoke.Id));

            Step(_smoke.FuseSeconds + 0.1f);

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_combat.AreaEffects.Count, Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Not.Contain("[Grenade] net exploded"), "smoke does not explode");
            var spread = _transport.Read<AreaEffectMessage>(2, Copy).Single();
            Assert.That(spread.KindId, Is.EqualTo("smoke"));
            Assert.That(spread.Radius, Is.EqualTo(4f));
            Assert.That(spread.RemainingSeconds, Is.EqualTo(3f));

            Step(3f);

            Assert.That(_combat.AreaEffects.Count, Is.EqualTo(0));
            var messages = _transport.Read<AreaEffectMessage>(2, Copy);
            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[1].AreaId, Is.EqualTo(spread.AreaId));
            Assert.That(messages[1].RemainingSeconds, Is.Zero, "the end is sent with no time left");
        }

        [Test]
        public void ATypeThatIsNotCarriedIsRejected()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = _smoke.Id });

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("reason=UnknownType"));
        }

        [Test]
        public void APlayerWhoJoinsLateSeesTheAreaEffectsAlreadyThere()
        {
            Start(Rules(), grenades: new GrenadeCatalog(new[] { _smoke }));
            Join(1);
            Join(2);
            Step(0.2f);
            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });
            Step(_smoke.FuseSeconds + 0.1f);

            Join(3);

            Assert.That(_transport.Read<AreaEffectMessage>(3, Copy).Single().RemainingSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void GrenadesAreOffWhenTheRulesSaySo()
        {
            Start(Rules().WithActions(Rules().Actions.WithGrenade(Grenade.Off)));
            Join(1);
            Join(2);
            Step(0.2f);

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX });

            Assert.That(_combat.LiveGrenadeCount, Is.EqualTo(0));
            Assert.That(_log.ToString(), Does.Contain("reason=Disabled"));
        }

        [Test]
        public void AHardLandingHurtsWhenTheRuleIsOn()
        {
            Start(Rules().WithFallDamage(FallDamage.On));
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, _alphaSpawn + new Vector3(0f, 30f, 0f), 0f, _host.TickCount);
            Walk(1, Vector2.Zero, 180);

            var health = _rules.Session.GetHealth(1);
            Assert.That(health, Is.LessThan(NetMatchSettings.DefaultMaxHealth), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] fall owner=1 tick="));
            Assert.That(_log.ToString(), Does.Contain("damage=" + (NetMatchSettings.DefaultMaxHealth - health).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void AHardLandingDoesNotHurtWhenTheRuleIsOff()
        {
            Start(Rules().WithFallDamage(FallDamage.Off));
            Join(1);
            Join(2);
            Step(0.2f);

            _context.Bodies.Teleport(1, _alphaSpawn + new Vector3(0f, 30f, 0f), 0f, _host.TickCount);
            Walk(1, Vector2.Zero, 180);

            Assert.That(_rules.Session.GetHealth(1), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] fall owner="));
        }

        [Test]
        public void DeadPlayersCannotShoot()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Step(0.2f);
            _rules.ApplyDamage(2, 1, 1000);

            FireAt(1, Body(2).Center);

            Assert.That(_rules.Session.GetHealth(2), Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
        }

        private static WeaponSpec ExplodingRifle(ExplosionSpec explosion)
        {
            var rifle = TestRules.Rifle();
            var impact = new ImpactSpec(rifle.Impact.Damage, rifle.Impact.Falloff, rifle.Impact.StructureRadius, explosion);
            return new WeaponSpec(rifle.Id, rifle.NameKey, rifle.Category, rifle.Trigger, rifle.Ammo, rifle.Round, impact, rifle.Aim, rifle.AmmoTypes, rifle.ShotNoiseRadius);
        }

        private static GameRules Rules()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithGrenade(Grenade.On).WithGrenadeCount(2);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private static readonly GrenadeSpec _smoke = new GrenadeSpec(
            "ecaf6e57-cb77-4707-9321-6ea5b659408f",
            "grenade.smoke",
            new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(4f, 3f, 0.5f, 0, visualId: "smoke")),
            fuseSeconds: 1f);

        private static readonly GrenadeSpec _sticky = new GrenadeSpec(
            "5eb3292d-6300-4ec2-b325-6ad793017e49",
            "grenade.sticky",
            GrenadeSpec.Default.Impact,
            stickiness: GrenadeStickiness.SurfacesAndCharacters);

        private static AreaEffectMessage Copy(AreaEffectMessage m)
        {
            return new AreaEffectMessage { AreaId = m.AreaId, KindId = m.KindId, Center = m.Center, Radius = m.Radius, RemainingSeconds = m.RemainingSeconds };
        }

        private void Start(
            GameRules rules, bool wall = false, bool destructibleWall = false, int wallPieces = 1, WeaponSpec rifle = null, GrenadeCatalog grenades = null)
        {
            var colliders = new List<WorldCollider>
            {
                WorldCollider.Box(1, 0, WorldCollider.StaticGroup, Vector3.Zero, new Vector3(60f, 0.5f, 60f), Quaternion.Identity),
            };
            NetMapStructures structures = null;
            if (wall)
            {
                // 部品を横に並べた壁。どの部品も地面に接していて、互いに支え合わない
                var width = 6f / wallPieces;
                for (var i = 0; i < wallPieces; i++)
                {
                    var group = destructibleWall ? MapColliderGroups.Piece(0, i) : WorldCollider.StaticGroup;
                    var center = new Vector3(0f, 2.5f, -3f + (width * (i + 0.5f)));
                    colliders.Add(WorldCollider.Box(2 + i, 0, group, center, new Vector3(0.25f, 2f, width / 2f), Quaternion.Identity));
                }

                if (destructibleWall)
                {
                    var health = new int[wallPieces];
                    var grounded = new bool[wallPieces];
                    Array.Fill(health, _wallHealth[0]);
                    Array.Fill(grounded, _wallGrounded[0]);
                    structures = new NetMapStructures(new[] { new StructureLayout(health, grounded, Array.Empty<(int, int)>()) });
                }
            }

            var points = new NetMapPoints(new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) }, Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", new WorldMap(colliders), TestMaps.NavMesh(), "memory", points, structures);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules, rifle: rifle), PingSpec.Default, "memory", null, grenades);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            _context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                map,
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _combat = new ServerCombat(_rules, _movement);
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(_combat);
            _host.Start(_context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private ServerBody Body(int id)
        {
            Assert.That(_context.Bodies.TryGet(id, out var body), Is.True);
            return body;
        }

        private void FireAt(int shooter, Vector3 target, int? seenTick = null)
        {
            var eye = Body(shooter).EyePosition;
            Fire(shooter, eye, target - eye, seenTick ?? (int)_host.TickCount);
        }

        private void Fire(int shooter, Vector3 origin, Vector3 direction, int tick, string weaponId = TestRules.RifleId)
        {
            _host.OnMessage(new ConnectionId((ulong)shooter), new FireRequestMessage
            {
                WeaponId = weaponId,
                Origin = origin,
                Direction = direction,
                ClientTick = tick,
                InterpolationAlpha = 0f,
                ShotIndex = _shotIndex++,
                ShotGeneration = 0,
            });
        }

        private void Send(int shooter, FireRequestMessage request)
        {
            _host.OnMessage(new ConnectionId((ulong)shooter), request);
        }

        private void Walk(int id, Vector2 move, int ticks, InputButtons buttons = InputButtons.None)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, move, 0f, buttons));
                _host.OnMessage(new ConnectionId((ulong)id), input);
                Tick();
            }
        }

        private SnapshotMessage Snapshot(int receiverId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                snapshot.Players.Add().PlayerId = player.PlayerId;
            }

            Assert.That(_match.TryGetPlayer(receiverId, out var receiver), Is.True);
            _combat.Contribute(snapshot, receiver);
            return snapshot;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private static KilledMessage Copy(KilledMessage m)
        {
            return new KilledMessage { KillerId = m.KillerId, VictimId = m.VictimId, WeaponId = m.WeaponId, Headshot = m.Headshot, Friendly = m.Friendly };
        }

        private static ShotMessage CopySegments(ShotMessage m)
        {
            var copy = new ShotMessage
            {
                WeaponId = m.WeaponId,
                HasImpact = m.HasImpact,
                ImpactIsCharacter = m.ImpactIsCharacter,
                HasImpactZone = m.HasImpactZone,
            };
            for (var i = 1; i < m.SegmentCount; i++)
            {
                copy.AddSegment();
            }

            return copy;
        }

        private static IEnumerable<string> Lines(StringWriter writer)
        {
            return writer.ToString().Split('\n').Select(line => line.TrimEnd('\r'));
        }
    }
}
