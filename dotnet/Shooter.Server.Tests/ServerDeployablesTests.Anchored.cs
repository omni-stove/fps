using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 人や乗り物に貼った C4 が、貼った先と一緒に動いて今の所で爆発することと、貼った先を失うとその場に残ることを確かめる
    public sealed partial class ServerDeployablesTests
    {
        // Bravo (2) を Alpha の持ち主 (x=-5) の前に立たせる位置
        private static readonly Vector3 _enemyInFront = new Vector3(-3.5f, 1.6f, 0f);

        // 床の上の車。車体の箱は x -3.5〜-2.5、y 0.8〜2.2、z -1〜1 で、Alpha の持ち主へ x=-3.5 の面を向ける
        private static readonly Vector3 _carPosition = new Vector3(-3f, 0.5f, 0f);

        // 運転席の乗り口。持ち主から車体の面へ引く線に掛からない、車の向こう側に置く
        private static readonly Vector3 _carEnter = new Vector3(2f, 0f, 0f);

        [Test]
        public void ARemoteChargeOnAnEnemyMovesWithThemAndBlowsUpWhereTheyAre()
        {
            Start(Explosives());
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;
            Join(1);
            Join(2);
            Step(0.2f);
            Teleport(2, _enemyInFront);
            var radius = _context.Bodies.Geometry.Radius;
            var height = BodyOf(1).Center.Y;

            // 持ち主の体の中心から水平に引いた線は、Bravo の体の持ち主側の面 (x=-3.5-半径) に当たる
            Assert.That(
                _deployables.Place(1, _chargeId, new Vector3(_enemyInFront.X - radius, height, 0f), 90f),
                Is.EqualTo(ServerDeployRefusal.None),
                _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 attached to=player:2"));
            Assert.That(Rows(1)[0].Position.X, Is.EqualTo(_enemyInFront.X - radius).Within(1e-3f));

            // 右へ向いて歩いた Bravo の左 (+Z) の脇に付いていく
            var walked = new Vector3(0.5f, 1.6f, 4f);
            BodyOf(2).SetPose(walked, 90f, PlayerStance.Standing);
            Step(_tickSeconds);

            var expected = new Vector3(walked.X, height, walked.Z + radius);
            var row = Rows(1)[0];
            Assert.That(Vector3.Distance(row.Position, expected), Is.LessThan(1e-3f), "the charge keeps its place on the body's left side");
            Assert.That(row.Yaw, Is.EqualTo(180f).Within(1e-3f), "the charge turns with the body it is stuck on");

            Teleport(1, _farAway);
            Detonate(1, _chargeId);

            var blasts = _transport.Read<ExplosionMessage>(1, m => new ExplosionMessage { Center = m.Center, WeaponId = m.WeaponId });
            Assert.That(blasts, Has.Count.EqualTo(1));
            var blastCenter = expected + new Vector3(0f, 0f, ServerDeployables.BlastLift);
            Assert.That(Vector3.Distance(blasts[0].Center, blastCenter), Is.LessThan(1e-3f), "the blast starts off the body, on its turned side");
            Assert.That(_rules.Session.IsAlive(2), Is.False);
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
            Assert.That(kills[0].WeaponId, Is.EqualTo(_chargeId));
        }

        [Test]
        public void ARemoteChargeStaysWhereTheEnemyItIsStuckOnFell()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);
            Teleport(2, _enemyInFront);
            var stuckAt = new Vector3(_enemyInFront.X - _context.Bodies.Geometry.Radius, BodyOf(1).Center.Y, 0f);
            Assert.That(_deployables.Place(1, _chargeId, stuckAt, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_tickSeconds);
            BodyOf(2).SetPose(new Vector3(5f, 1.6f, 5f), 0f, PlayerStance.Standing);
            Step(_tickSeconds);

            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 net released from=player:2 reason=Died"));
            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(1), "the charge is still the owner's to detonate");
            Assert.That(Vector3.Distance(rows[0].Position, stuckAt), Is.LessThan(1e-3f), "the server keeps no body, so the charge stays where the enemy fell");
        }

        [Test]
        public void ARemoteChargeStaysWhereTheEnemyItIsStuckOnLeftTheMatch()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);
            Teleport(2, _enemyInFront);
            var stuckAt = new Vector3(_enemyInFront.X - _context.Bodies.Geometry.Radius, BodyOf(1).Center.Y, 0f);
            Assert.That(_deployables.Place(1, _chargeId, stuckAt, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            _match.Leave(new ConnectionId(2));
            Step(_tickSeconds);

            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 net released from=player:2 reason=Left"));
            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(Vector3.Distance(rows[0].Position, stuckAt), Is.LessThan(1e-3f));
        }

        [Test]
        public void ARemoteChargeIsNotStuckOnTheOwner()
        {
            Start(Explosives());
            Join(1);
            Join(2);
            Step(0.2f);

            // 体の中心から自分の体の中を通る線は、自分の体に当たらず足元の床まで届く
            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-4.8f, 0.5f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("attached to=collider:1"));
            Assert.That(_log.ToString(), Does.Not.Contain("attached to=player:1"));
        }

        [Test]
        public void ARemoteChargeOnAVehicleMovesAndTurnsWithIt()
        {
            Start(Explosives(), vehicles: new NetMapVehicles(new[] { Car() }));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _chargeId, new Vector3(-3.5f, 1.2f, 0f), 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[RemoteCharge] owner=1 kind=deploy-remote-charge serial=1 attached to=vehicle:car#0"));

            Teleport(2, MapVehicle.RiderOrigin(_carPosition + _carEnter, _context.Bodies.Geometry, crouched: false));
            Assert.That(_vehicles.Handle(2, VehicleAction.Board, 0, 0), Is.True, _log.ToString());
            var moved = _carPosition + new Vector3(0f, 0f, 2f);
            var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f);
            _host.OnMessage(new ConnectionId(2), new VehicleMoveMessage
            {
                Vehicle = 0,
                Sequence = 0,
                CorrectionGeneration = 1,
                Position = moved,
                Rotation = turned,
            });
            Step(_tickSeconds);

            Assert.That(_vehicles.TryGetState(0, out var carPosition, out _, out _, out _), Is.True);
            Assert.That(carPosition, Is.EqualTo(moved), _log.ToString());
            var expected = moved + Vector3.Transform(new Vector3(-0.5f, 0.7f, 0f), turned);
            var row = Rows(1)[0];
            Assert.That(Vector3.Distance(row.Position, expected), Is.LessThan(1e-3f), "the charge keeps its place on the hull");
            Assert.That(row.Yaw, Is.EqualTo(90f + (0.2f * 180f / MathF.PI)).Within(1e-2f));

            Teleport(1, _farAway);
            Assert.That(_deployables.Detonate(1, _chargeId), Is.EqualTo(ServerDeployRefusal.None));
            var blasts = _transport.Read<ExplosionMessage>(1, m => new ExplosionMessage { Center = m.Center });
            var blastCenter = expected + (Vector3.Transform(-Vector3.UnitX, turned) * ServerDeployables.BlastLift);
            Assert.That(Vector3.Distance(blasts[0].Center, blastCenter), Is.LessThan(1e-3f), "the blast starts off the turned side of the hull");
        }

        // 運転席だけの車。車体の箱は床 (y=0.5) から浮かせ、運転の確かめで床に埋まっているとされないようにする
        private static MapVehicle Car()
        {
            var spec = new VehicleSpec("car", 1000, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) });
            return new MapVehicle(
                spec,
                _carPosition,
                Quaternion.Identity,
                new[] { new MapVehicleSeat(new Vector3(0f, 0.5f, 0f), 0f, new[] { _carEnter }) },
                new[] { new MapVehicleHullBox(new Vector3(0f, 1f, 0f), new Vector3(0.5f, 0.7f, 1f), Quaternion.Identity) },
                new[] { new Vector3(3f, 0f, 0f) });
        }

        private ServerBody BodyOf(int playerId)
        {
            Assert.That(_context.Bodies.TryGet(playerId, out var body), Is.True);
            return body;
        }
    }
}
