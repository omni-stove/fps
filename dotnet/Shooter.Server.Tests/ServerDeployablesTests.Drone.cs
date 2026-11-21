using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ドローンを置き、持ち主の操縦 (DroneControlMessage) で飛ばして、もう 1 人のスナップショットの行が動くことと、電池・距離・戻す・断るを確かめる
    public sealed partial class ServerDeployablesTests
    {
        // 床の上面 y=0.5 に置いたドローンの当たりの球の半径 (いちばん狭い高さ 0.25 の半分)
        private const float _droneRadius = DroneSpec.DefaultHeight / 2f;

        private int _droneSequence;

        [Test]
        public void APlacedDroneIsListedAsADroneForTheOtherPlayer()
        {
            StartDrone(DroneSpec.Default);

            Deploy(1, _droneId, _alphaFront, 90f);

            var rows = Rows(2);
            Assert.That(rows, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(rows[0].Kind, Is.EqualTo(DeployableKind.Drone));
            Assert.That(rows[0].TypeId, Is.EqualTo(_droneId));
            Assert.That(rows[0].OwnerId, Is.EqualTo(1));
            Assert.That(rows[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(rows[0].Health, Is.EqualTo(DroneSpec.DefaultMaxHealth));
            Assert.That(rows[0].Yaw, Is.EqualTo(90f));
            Assert.That(_log.ToString(), Does.Contain(
                "[Drone] owner=1 kind=deploy-drone serial=1 armed speed=6.0 range=40.0 battery=30.0 radius=12.0 interval=1.00 reveal=3.0"));
            Assert.That(_log.ToString(), Does.Not.Contain("unusable"));
        }

        [Test]
        public void SteeringFliesTheDroneAtItsSpeedAndTheOtherPlayerSeesItMove()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 0f);

            // 向きを 90 度 (+X) にして前へ 30 tick 飛ばす
            const int ticks = 30;
            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, ticks);

            var row = Rows(2)[0];
            var expected = _alphaFront.X + (ticks * DroneSpec.DefaultSpeed * _tickSeconds);
            Assert.That(row.Position.X, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(row.Position.Y, Is.EqualTo(_alphaFront.Y).Within(1e-4f), "moving forward does not climb");
            Assert.That(row.Position.Z, Is.EqualTo(_alphaFront.Z).Within(1e-4f));
            Assert.That(row.Yaw, Is.EqualTo(90f).Within(1e-4f));
        }

        [Test]
        public void ADiagonalAndClimbingInputIsNoFasterThanTheSpeed()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 0f);

            const int ticks = 20;
            Fly(1, 1, new Vector2(1f, 1f), 1f, 0f, ticks);

            var moved = Rows(2)[0].Position - _alphaFront;
            Assert.That(moved.Length(), Is.EqualTo(ticks * DroneSpec.DefaultSpeed * _tickSeconds).Within(1e-3f));
            Assert.That(moved.Y, Is.GreaterThan(0f), "climbing lifts the drone");
            Assert.That(moved.X, Is.GreaterThan(0f), "right of yaw 0 is +X");
            Assert.That(moved.Z, Is.GreaterThan(0f), "forward of yaw 0 is +Z");
        }

        [Test]
        public void TheDroneStopsWhenTheControlStopsArriving()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);

            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, 1);
            Step(1f);

            var travelled = Rows(2)[0].Position.X - _alphaFront.X;
            var held = DroneSpec.DefaultSpeed * ServerDeployables.DroneInputHoldSeconds;
            Assert.That(travelled, Is.GreaterThan(held - (2f * DroneSpec.DefaultSpeed * _tickSeconds)));
            Assert.That(travelled, Is.LessThan(held + (2f * DroneSpec.DefaultSpeed * _tickSeconds)), "the last input is not flown forever");
        }

        [Test]
        public void AWallStopsTheDroneShortOfIt()
        {
            // x=-1 に立てた厚さ 0.2 の壁。面は x=-1.1
            var wall = WorldCollider.Box(4, 0, WorldCollider.StaticGroup, new Vector3(-1f, 2f, 0f), new Vector3(0.1f, 1.5f, 5f), Quaternion.Identity);
            StartDrone(DroneSpec.Default, wall);
            Deploy(1, _droneId, _alphaFront, 90f);

            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, 60);

            var x = Rows(2)[0].Position.X;
            Assert.That(x, Is.EqualTo(-1.1f - _droneRadius - ServerDeployables.DroneWallSkin).Within(0.01f), _log.ToString());
        }

        [Test]
        public void TheDroneDoesNotSinkThroughTheFloor()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);

            Fly(1, 1, Vector2.Zero, -1f, 90f, 30);

            Assert.That(Rows(2)[0].Position.Y, Is.EqualTo(_alphaFront.Y).Within(1e-4f));
        }

        [Test]
        public void AnEmptyBatteryRemovesTheDrone()
        {
            StartDrone(new DroneSpec(DroneSpec.DefaultSpeed, DroneSpec.DefaultControlRange, 0.5f, 1f, 1f, 1f));
            Deploy(1, _droneId, _alphaFront, 90f);
            Assert.That(_deployables.DroneBatterySecondsLeft(1), Is.EqualTo(0.5f));

            Step(0.25f);
            Assert.That(_deployables.DroneBatterySecondsLeft(1), Is.EqualTo(0.25f).Within(2f * _tickSeconds));
            Step(0.35f);

            Assert.That(Rows(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-drone serial=1 removed reason=Expired"));
        }

        [Test]
        public void FlyingOutOfRangeRemovesTheDrone()
        {
            StartDrone(new DroneSpec(DroneSpec.DefaultSpeed, 1f, DroneSpec.DefaultBatterySeconds, 1f, 1f, 1f));
            Deploy(1, _droneId, _alphaFront, 90f);

            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, 20);

            Assert.That(Rows(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-drone serial=1 removed reason=OutOfRange"));
        }

        [Test]
        public void TheOwnerRecallsTheDrone()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);

            Control(1, new DroneControlMessage { Deployable = 1, Sequence = 0, Recall = true });

            Assert.That(Rows(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Drone] owner=1 kind=deploy-drone serial=1 recalled at=(-3.00, 0.50, 0.00)"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-drone serial=1 removed reason=Recalled"));
        }

        [Test]
        public void AnotherPlayerCannotSteerOrRecallTheDrone()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);

            var refused = _deployables.Steer(2, new DroneControlMessage { Deployable = 1, Sequence = 0, Move = new Vector2(0f, 1f), Recall = true });
            Step(0.2f);

            Assert.That(refused, Is.EqualTo(ServerDroneControlRefusal.NotOwner));
            Assert.That(Rows(2), Has.Count.EqualTo(1));
            Assert.That(Rows(2)[0].Position.X, Is.EqualTo(_alphaFront.X).Within(1e-4f));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=2 kind=- net drone control refused serial=1 reason=NotOwner"));
        }

        [Test]
        public void AControlForAnUnknownIdOrAnotherKindIsRefused()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _shieldId, _alphaFront, 90f);

            Assert.That(
                _deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 0, Recall = true }), Is.EqualTo(ServerDroneControlRefusal.NotDrone));
            Assert.That(_deployables.Steer(1, new DroneControlMessage { Deployable = 9, Sequence = 0 }), Is.EqualTo(ServerDroneControlRefusal.NotFound));
            Assert.That(_deployables.Count, Is.EqualTo(1), "a recall flag on a shield removes nothing");
        }

        [Test]
        public void AnOlderControlDoesNotOverwriteANewerOne()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 0f);

            Assert.That(_deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 5, Yaw = 90f }), Is.EqualTo(ServerDroneControlRefusal.None));
            Assert.That(_deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 4, Yaw = 180f }), Is.EqualTo(ServerDroneControlRefusal.Stale));
            Assert.That(_deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 5, Yaw = 180f }), Is.EqualTo(ServerDroneControlRefusal.Stale));

            Assert.That(Rows(2)[0].Yaw, Is.EqualTo(90f));
        }

        [Test]
        public void AYawOutsideOneTurnIsWrapped()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 0f);

            _deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 0, Yaw = -90f });

            Assert.That(Rows(2)[0].Yaw, Is.EqualTo(270f).Within(1e-4f));
        }

        [Test]
        public void ADeadOwnerCannotSteerAndTheDroneStops()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);
            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, 1);
            var before = Rows(2)[0].Position;

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            var refused = _deployables.Steer(1, new DroneControlMessage { Deployable = 1, Sequence = 100, Move = new Vector2(0f, 1f), Yaw = 90f });
            Step(0.2f);

            Assert.That(refused, Is.EqualTo(ServerDroneControlRefusal.CannotAct));
            Assert.That(Rows(2)[0].Position.X, Is.EqualTo(before.X).Within(1e-4f), "the drone stays where its owner fell");
        }

        [Test]
        public void AnEnemyShotHitsTheDroneWhereItHasFlown()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 90f);
            Fly(1, 1, new Vector2(0f, 1f), 0f, 90f, 30);
            var at = Rows(2)[0].Position;

            var hit = _deployables.Raycast(
                new Vector3(at.X, at.Y + (DroneSpec.DefaultHeight / 2f), 3f), -Vector3.UnitZ, 10f, 2, out var id, out _);
            var missed = _deployables.Raycast(
                new Vector3(_alphaFront.X, _alphaFront.Y + (DroneSpec.DefaultHeight / 2f), 3f), -Vector3.UnitZ, 10f, 2, out _, out _);

            Assert.That(hit, Is.True);
            Assert.That(id, Is.EqualTo(1));
            Assert.That(missed, Is.False, "nothing is left where the drone was placed");
            Assert.That(_deployables.Damage(1, 2, DroneSpec.DefaultMaxHealth), Is.True);
            Assert.That(Rows(2), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-drone serial=1 removed reason=Destroyed"));
        }

        [Test]
        public void TheControlMessageReachesTheDroneThroughTheHost()
        {
            StartDrone(DroneSpec.Default);
            Deploy(1, _droneId, _alphaFront, 0f);

            Control(1, new DroneControlMessage { Deployable = 1, Sequence = 0, Yaw = 45f });

            Assert.That(Rows(2)[0].Yaw, Is.EqualTo(45f));
        }

        // 置く枠の回数で止まらないよう、送れる数より多い回数と間隔 0 の枠にする (Catalog と同じ)
        private void StartDrone(DroneSpec spec, WorldCollider extra = null)
        {
            var free = new AbilitySpec(SnapshotMessage.MaxDeployables + 1, 0f, 0f, 0f);
            var catalog = new DeployCatalog(new[]
            {
                new DeployType(_droneId, DeployRole.Drone, DeployType.DefaultDeploy(DeployRole.Drone), drone: spec, ability: free),
                new DeployType(_shieldId, DeployRole.Shield, DeployType.DefaultDeploy(DeployRole.Shield), ability: free),
            });
            Start(catalog, extra: extra);
            Join(1);
            Join(2);
            Step(0.2f);
            _droneSequence = 0;
        }

        // 持ち主のクライアントと同じく、tick ごとに次の番号で同じ入力を送り直しながら進める
        private void Fly(int playerId, int deployableId, Vector2 move, float climb, float yaw, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                Control(playerId, new DroneControlMessage
                {
                    Deployable = deployableId,
                    Sequence = _droneSequence++,
                    Move = move,
                    Climb = climb,
                    Yaw = yaw,
                });
                Step(_tickSeconds);
            }
        }

        private void Control(int playerId, DroneControlMessage control)
        {
            _host.OnMessage(new ConnectionId((ulong)playerId), control);
        }
    }
}
