using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // 持っている盾の板の置き場と、正面の弾を止めて背中と扇の外の弾を通すことを、当たり判定だけで確かめる
    public sealed class ServerHitboxesShieldTests
    {
        // 原点 y=1 に置いた体 (足元 0・目 1.6・半径 0.5)。-X を向くと、原点 (撃つ人) の方を向く
        private const float _facingTheShooter = -MathF.PI / 2f;
        private const float _facingAway = MathF.PI / 2f;
        private const int _holderId = 2;
        private const int _shooterId = 1;

        private static readonly StanceGeometry _geometry = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);
        private static readonly Vector3 _holderPosition = new Vector3(10f, 1f, 0f);

        // 板の前の面は、体の原点から HeldForward 前に出した板の中心から、厚さの半分だけ手前
        private static readonly float _plateFront = _holderPosition.X - RiotShieldSpec.HeldForward - (RiotShieldSpec.HeldThickness / 2f);

        private ServerBodies _bodies;

        [SetUp]
        public void SetUp()
        {
            _bodies = new ServerBodies(_geometry);
        }

        [Test]
        public void ARaisedShieldStopsAShotFromTheFront()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var hit), Is.True);
            Assert.That(hit.HitShield, Is.True);
            Assert.That(hit.ShieldHolderId, Is.EqualTo(_holderId));
            Assert.That(hit.HitTarget, Is.False, "the holder is not hit");
            Assert.That(hit.Distance, Is.EqualTo(_plateFront).Within(1e-4f));
            Assert.That(hit.Point.X, Is.EqualTo(_plateFront).Within(1e-4f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void AShotFromBehindHitsTheHolder()
        {
            var hitboxes = Hitboxes();
            Holder(_facingAway, RiotShieldSpec.Default);
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var hit), Is.True);
            Assert.That(hit.HitTarget, Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(_holderId));
            Assert.That(hit.HitShield, Is.False);
            Assert.That(hit.Distance, Is.EqualTo(9.5f).Within(1e-4f));
        }

        [Test]
        public void ALoweredShieldLetsTheShotHitTheHolder()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, null);
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var hit), Is.True);
            Assert.That(hit.HitTarget, Is.True);
            Assert.That(hit.HitShield, Is.False);
            Assert.That(hit.Distance, Is.EqualTo(9.5f).Within(1e-4f));
        }

        [Test]
        public void AShotOverTheTopOfTheShieldHitsTheHead()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            // 板の上の端は原点 + 高さの半分 (1.6)。頭の球の中心 (1.5) より上を水平に撃つ
            var origin = new Vector3(0f, 1.75f, 0f);
            Assert.That(hitboxes.Raycast(origin, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var hit), Is.True);
            Assert.That(hit.HitTarget, Is.True);
            Assert.That(hit.HitShield, Is.False);
        }

        [Test]
        public void AShotFromOutsideTheArcPassesThePlate()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            // 板の中心の真横 (扇の 120 度の外) から、体の横 (軸から 0.6) を通って板の端だけをかすめる線
            var plateX = _holderPosition.X - RiotShieldSpec.HeldForward;
            var origin = new Vector3(plateX, 1f, -5f);

            Assert.That(hitboxes.Raycast(origin, Vector3.UnitZ, 100f, 1f, _shooterId, _ => true, out _), Is.False);
        }

        [Test]
        public void AFullCircleArcStopsAShotFromTheSide()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, new RiotShieldSpec(blockArcDegrees: RiotShieldSpec.MaxBlockArcDegrees));
            hitboxes.Record(1);
            var plateX = _holderPosition.X - RiotShieldSpec.HeldForward;
            var origin = new Vector3(plateX, 1f, -5f);

            Assert.That(hitboxes.Raycast(origin, Vector3.UnitZ, 100f, 1f, _shooterId, _ => true, out var hit), Is.True);
            Assert.That(hit.HitShield, Is.True);
            Assert.That(hit.Distance, Is.EqualTo(5f - (RiotShieldSpec.DefaultWidth / 2f)).Within(1e-4f));
        }

        [Test]
        public void TheShieldIsRewoundWithTheHolder()
        {
            var hitboxes = Hitboxes();
            var holder = Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);
            holder.RaisedShield = null;
            hitboxes.Record(2);

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var then), Is.True);
            Assert.That(then.HitShield, Is.True, "the shooter saw the raised shield");
            Assert.That(then.RewindTicks, Is.EqualTo(1f));
            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 2f, _shooterId, _ => true, out var now), Is.True);
            Assert.That(now.HitTarget, Is.True);
        }

        [Test]
        public void TheShieldTurnsWithTheHolderAtTheRewoundTick()
        {
            var hitboxes = Hitboxes();
            var holder = Holder(_facingAway, RiotShieldSpec.Default);
            hitboxes.Record(1);
            holder.SetPose(_holderPosition, _facingTheShooter, PlayerStance.Standing);
            hitboxes.Record(2);

            Assert.That(hitboxes.TryGetHeldShield(_holderId, 1f, out var thenCenter, out var thenForward, out _), Is.True);
            Assert.That(thenForward.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(thenCenter.X, Is.EqualTo(_holderPosition.X + RiotShieldSpec.HeldForward).Within(1e-4f));
            Assert.That(hitboxes.TryGetHeldShield(_holderId, 2f, out var nowCenter, out var nowForward, out var spec), Is.True);
            Assert.That(nowForward.X, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(nowCenter.X, Is.EqualTo(_holderPosition.X - RiotShieldSpec.HeldForward).Within(1e-4f));
            Assert.That(spec, Is.SameAs(RiotShieldSpec.Default));

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, _ => true, out var then), Is.True);
            Assert.That(then.HitTarget, Is.True, "the holder had their back to the shooter at that tick");
        }

        [Test]
        public void TheHoldersOwnShotsPassTheirShield()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(_holderPosition, -Vector3.UnitX, 100f, 1f, _holderId, _ => true, out _), Is.False);
        }

        [Test]
        public void ADeadHoldersShieldStopsNothing()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            Assert.That(hitboxes.Raycast(Vector3.UnitY, Vector3.UnitX, 100f, 1f, _shooterId, id => id != _holderId, out _), Is.False);
        }

        [Test]
        public void APenetratingCastStopsAtTheShield()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);
            var hits = new List<ShotHit>();

            Assert.That(hitboxes.Cast(Vector3.UnitY, Vector3.UnitX, 0f, 100f, 3, 1f, _shooterId, _ => true, hits), Is.True);
            Assert.That(hits[0].HitShield, Is.True, "the plate is in front of the holder");
            Assert.That(hits[0].Distance, Is.EqualTo(_plateFront).Within(1e-4f));
            Assert.That(hitboxes.Thickness(hits[0], Vector3.UnitX, 10f), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void AThickRoundIsStoppedByTheShield()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);
            var hits = new List<ShotHit>();

            Assert.That(hitboxes.Cast(Vector3.UnitY, Vector3.UnitX, 0.1f, 100f, 1, 1f, _shooterId, _ => true, hits), Is.True);
            Assert.That(hits[0].HitShield, Is.True);
            Assert.That(hits[0].Distance, Is.EqualTo(_plateFront - 0.1f).Within(1e-4f));
        }

        [Test]
        public void ForgettingTheHolderDropsTheShieldHistory()
        {
            var hitboxes = Hitboxes();
            Holder(_facingTheShooter, RiotShieldSpec.Default);
            hitboxes.Record(1);

            hitboxes.Forget(_holderId);

            Assert.That(hitboxes.TryGetHeldShield(_holderId, 1f, out _, out _, out _), Is.False);
        }

        [Test]
        public void OnlyAShieldHitIsAShieldHitAndItKeepsItsHolderWhenMoved()
        {
            Assert.That(default(ShotHit).HitShield, Is.False);
            Assert.That(ShotHit.Target(Vector3.Zero, Vector3.UnitX, 1f, 0, Vector3.Zero, false, HitZone.Body).HitShield, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => ShotHit.Shield(Vector3.Zero, Vector3.UnitX, 1f, Shooter.Protocol.PlayerIds.None));

            var moved = ShotHit.Shield(Vector3.Zero, -Vector3.UnitX, 1f, 0).WithRewind(2f).WithDistance(7f);

            Assert.That(moved.HitShield, Is.True);
            Assert.That(moved.ShieldHolderId, Is.Zero);
            Assert.That(moved.Distance, Is.EqualTo(7f));
            Assert.That(moved.RewindTicks, Is.EqualTo(2f));
            Assert.That(moved.HitTarget, Is.False);
        }

        [Test]
        public void ARayStartingInsideThePlateDoesNotHitIt()
        {
            var half = new Vector3(0.3f, 0.6f, 0.025f);

            Assert.That(ServerHitboxes.RayPlate(Vector3.Zero, Vector3.UnitZ, Vector3.Zero, Vector3.UnitZ, half, 10f, out _, out _), Is.False);
            Assert.That(
                ServerHitboxes.RayPlate(new Vector3(0f, 0f, -1f), Vector3.UnitZ, Vector3.Zero, Vector3.UnitZ, half, 10f, out var distance, out var normal),
                Is.True);
            Assert.That(distance, Is.EqualTo(0.975f).Within(1e-5f));
            Assert.That(normal.Z, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(
                ServerHitboxes.RayPlate(new Vector3(0f, 0f, -1f), Vector3.UnitZ, Vector3.Zero, Vector3.UnitZ, half, 0.5f, out _, out _),
                Is.False,
                "the plate is beyond the reach");
        }

        private ServerHitboxes Hitboxes()
        {
            return new ServerHitboxes(_bodies, new BvhWorldCollision(new WorldMap(Array.Empty<WorldCollider>())));
        }

        private ServerBody Holder(float yaw, RiotShieldSpec raised)
        {
            _bodies.Add(_holderId);
            _bodies.Teleport(_holderId, _holderPosition, yaw, 0);
            Assert.That(_bodies.TryGet(_holderId, out var body), Is.True);
            body.RaisedShield = raised;
            return body;
        }
    }
}
