using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Bot が乗り物の操縦の部品を選ぶ口 (IBotPilotedMotor / IBotPilot) に、本物の飛行機の部品がつながることを確かめる。口そのものは EnemyAgentVehicleControlTests が偽の部品で確かめる。
    public sealed class EnemyAgentAircraftTests
    {
        private const float Altitude = 500f;

        private readonly List<Object> _created = new List<Object>();
        private EnemyAgent _agent;

        [SetUp]
        public void SetUp()
        {
            var rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());

            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "Enemy";
            host.transform.position = new Vector3(-1.3f, 1f, 0f);
            _agent = host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = rifle;
            _agent.Id = "enemy-1";
            host.GetComponent<Damageable>().Initialize("enemy-1", 10);
            Assert.That(_agent.TryInitialize(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AnAircraftNeedsABotPilotAndItsPilotIsABotPilotType()
        {
            Assert.That(EnemyAgent.NeedsBotPilot(CreateAircraft("Plane")), Is.True);
            Assert.That(EnemyAgent.IsBotPilotType(typeof(BotAircraftPilot)), Is.True);
        }

        // 飛行機の動き方が自分の操縦の部品を名指し、その部品が乗った Bot の座席を通って飛ばすところまでを、本物の部品どうしで確かめる。
        [Test]
        public void AnAircraftGetsItsOwnPilotWhichFliesThroughTheBotsSeatAndLeavingHandsBackTheControls()
        {
            var plane = CreateAircraft("Plane");
            var link = plane.GetComponent<VehicleMotorLink>();
            var before = link.ControlScheme;
            var route = plane.gameObject.AddComponent<VehicleRoute>();
            route.SetPoints(Point("A", new Vector3(1000f, Altitude, 0f)));

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bot takes controls rider=enemy-1 as=pilot route=Plane");
            _agent.TryBoard(plane, 0, requireInRange: false);
            var pilot = plane.GetComponent<BotAircraftPilot>();
            Assert.That(pilot, Is.Not.Null);
            Assert.That(_agent.VehiclePilot, Is.SameAs(pilot));

            pilot.Tick(0.02f);

            Assert.That(pilot.IsFlying, Is.True);
            Assert.That(plane.Seats[0].Occupant, Is.SameAs(_agent), "the pilot does not seat a stand-in of its own");

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' bot gives up controls rider=enemy-1");
            Assert.That(_agent.TryLeaveVehicle(), Is.True);

            Assert.That(plane.GetComponent<BotAircraftPilot>(), Is.Null);
            Assert.That(_agent.VehiclePilot, Is.Null);
            Assert.That(_agent.IsDrivingVehicle, Is.False);
            Assert.That(link.ControlScheme, Is.SameAs(before));
        }

        private Vehicle CreateAircraft(string name)
        {
            var root = Track(new GameObject(name));
            root.transform.position = new Vector3(0f, Altitude, 0f);
            var seat = new GameObject("Pilot Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            var exit = new GameObject("Exit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(-3f, 0f, 0f);
            seat.SetExitPoints(exit);
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec(name.ToLowerInvariant(), 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            var motor = root.AddComponent<SimpleAircraftMotor>();
            motor.Body.useGravity = false;
            motor.Body.linearVelocity = new Vector3(0f, 0f, 55f);
            Assert.That(root.AddComponent<VehicleMotorLink>().Bind(), Is.True);
            Physics.SyncTransforms();
            return vehicle;
        }

        private Transform Point(string name, Vector3 position)
        {
            var point = Track(new GameObject(name)).transform;
            point.position = position;
            return point;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
