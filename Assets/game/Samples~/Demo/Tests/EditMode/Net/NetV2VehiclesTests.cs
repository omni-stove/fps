using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // サーバーが書いたスナップショットを通信の形にして読み直し、運転する人の側の乗り物へ渡す。物理は進めない。
    public sealed class NetV2VehiclesTests
    {
        private const int LocalPlayerId = 3;
        private const float Epsilon = 1e-4f;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private Scene _scene;
        private ClientVehicles _client;
        private NetV2Vehicles _vehicles;
        private int _tick;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            _client = new ClientVehicles(LocalPlayerId);
            _vehicles = new NetV2Vehicles(null);
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _vehicles.Leave();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void BreakingTheLeftWingOnTheServerDropsTheDriversLeftLift()
        {
            var plane = AddVehicle("Plane", withParts: true);
            Deliver(VehiclePartDamageSync.None, driving: true);
            _vehicles.Enter(_scene, _client, _stance, null);
            var source = plane.GetComponent<IAircraftConditionSource>();
            Assert.That(source.Condition, Is.EqualTo(AircraftCondition.Intact));

            Deliver(Taken(AircraftDamageSpec.DefaultLeftWingId, AircraftDamageSpec.DefaultWingDurability), driving: true);

            Assert.That(source.Condition.LeftLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(Epsilon));
            Assert.That(source.Condition.RightLift, Is.EqualTo(1f), "only the left wing was shot");
            Assert.That(source.Condition.Control, Is.EqualTo(1f));
            Assert.That(source.Condition.Thrust, Is.EqualTo(1f));
        }

        [Test]
        public void APartlyDamagedWingIsLeftWithTheRestOfItsDurability()
        {
            var parts = AddVehicle("Plane", withParts: true).GetComponent<AircraftPartDamage>();
            _vehicles.Enter(_scene, _client, _stance, null);

            Deliver(Taken(AircraftDamageSpec.DefaultLeftWingId, 200), driving: true);

            Assert.That(parts.Damage.Remaining(AircraftDamageSpec.DefaultLeftWingId), Is.EqualTo(AircraftDamageSpec.DefaultWingDurability - 200));
            Assert.That(parts.Condition.LeftLift, Is.EqualTo(0.2f + (0.8f / 3f)).Within(Epsilon), "a third left");
        }

        [Test]
        public void ADriverWhoJoinsLateGetsTheWingAlreadyBroken()
        {
            var parts = AddVehicle("Plane", withParts: true).GetComponent<AircraftPartDamage>();
            Deliver(Taken(AircraftDamageSpec.DefaultEngineId, 10000), driving: false);

            _vehicles.Enter(_scene, _client, _stance, null);

            Assert.That(parts.Damage.IsBroken(AircraftDamageSpec.DefaultEngineId), Is.True);
            Assert.That(parts.Condition.Thrust, Is.EqualTo(AircraftDamageSpec.DefaultMinThrust).Within(Epsilon));
        }

        [Test]
        public void TheServerRepairingTheVehicleRepairsTheParts()
        {
            var parts = AddVehicle("Plane", withParts: true).GetComponent<AircraftPartDamage>();
            _vehicles.Enter(_scene, _client, _stance, null);
            Deliver(Taken(AircraftDamageSpec.DefaultTailId, AircraftDamageSpec.DefaultTailDurability), driving: true);
            Assert.That(parts.Condition.Control, Is.EqualTo(AircraftDamageSpec.DefaultMinControl).Within(Epsilon));

            Deliver(VehiclePartDamageSync.None, driving: true);

            Assert.That(parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void PartsTheAircraftDoesNotKnowAreIgnored()
        {
            var parts = AddVehicle("Plane", withParts: true).GetComponent<AircraftPartDamage>();
            _vehicles.Enter(_scene, _client, _stance, null);

            Deliver(Taken("fuel", 500), driving: true);

            Assert.That(parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void AVehicleWithoutPartsTakesTheStateWithoutThem()
        {
            var jeep = AddVehicle("Jeep", withParts: false);
            _vehicles.Enter(_scene, _client, _stance, null);

            Assert.DoesNotThrow(() => Deliver(Taken(AircraftDamageSpec.DefaultLeftWingId, 50), driving: true));

            Assert.That(jeep.GetComponent<AircraftPartDamage>(), Is.Null);
            Assert.That(_vehicles.Count, Is.EqualTo(1));
        }

        [Test]
        public void LocalHitsStopReachingThePartsOnceTheServerDecides()
        {
            var parts = AddVehicle("Plane", withParts: true).GetComponent<AircraftPartDamage>();
            Assert.That(parts.Vehicle, Is.Not.Null);

            _vehicles.Enter(_scene, _client, _stance, null);

            Assert.That(parts.Vehicle, Is.Null);
        }

        private static VehiclePartDamageSync Taken(string partId, int damage)
        {
            Assert.That(VehiclePartDamageSync.None.TryAdd(partId, damage, out var parts), Is.True);
            return parts;
        }

        // サーバーの Contribute と同じ並びの行を書き、基準の無いスナップショットとして通信の形を通す。
        private void Deliver(VehiclePartDamageSync parts, bool driving)
        {
            var sent = new SnapshotMessage { Tick = ++_tick };
            ref var player = ref sent.Players.Add();
            player.PlayerId = LocalPlayerId;
            player.Vehicle = driving ? new VehicleSeatSync(0, 0) : VehicleSeatSync.None;
            ref var row = ref sent.Vehicles.Add();
            row.Index = 0;
            row.Rotation = System.Numerics.Quaternion.Identity;
            row.Health = 5000;
            row.Parts = parts;

            var writer = new MessageWriter();
            writer.WriteMessage(sent);
            var received = (SnapshotMessage)MatchMessages.CreateClientTable().Read(writer.AsSegment());
            Assert.That(received.IsComplete, Is.True);
            _client.Apply(received);
        }

        private GameObject AddVehicle(string name, bool withParts)
        {
            var root = new GameObject(name);
            _created.Add(root);
            SceneManager.MoveGameObjectToScene(root, _scene);
            new GameObject("Pilot").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);
            var vehicle = root.AddComponent<Vehicle>();
            var motor = root.AddComponent<SimpleAircraftMotor>();
            motor.Body.useGravity = false;
            vehicle.Configure(new VehicleSpec(name.ToLowerInvariant(), 5000, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            if (withParts)
            {
                // Edit Mode では OnEnable が走らないので、遊ぶときと同じく乗り物へつないだ状態を作る。
                root.AddComponent<AircraftPartDamage>().Attach(vehicle);
            }

            return root;
        }
    }
}
