using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Update は Edit Mode では走らないので、乗っている間に判断を止めることは Play での確認に任せ、ここでは乗り降りの口だけを確かめる。
    public sealed class EnemyAgentVehicleTests
    {
        private static readonly Vector3 _seatPosition = new Vector3(-0.5f, 0.4f, 0f);
        private static readonly Vector3 _exit = new Vector3(-2f, 0f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private EnemyAgent _agent;
        private NavMeshAgent _navAgent;
        private WeaponDefinition _rifle;
        private Vehicle _vehicle;
        private VehicleSeat _seat;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _created.Add(_rifle);

            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = "Enemy";
            host.transform.position = new Vector3(-1.3f, 1f, 0f);
            _created.Add(host);
            _agent = host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            host.GetComponent<Damageable>().Initialize("enemy-1", 10);
            _navAgent = host.GetComponent<NavMeshAgent>();

            var root = new GameObject("Jeep");
            _created.Add(root);
            _seat = new GameObject("Driver").AddComponent<VehicleSeat>();
            _seat.transform.SetParent(root.transform, false);
            _seat.transform.localPosition = _seatPosition;
            var exit = new GameObject("Exit").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = _exit;
            _seat.SetExitPoints(exit);
            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec("jeep", 100, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            Physics.SyncTransforms();
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
        public void AnUninitializedAgentCannotBoard()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' board refused rider=enemy-1 reason=rider_unavailable");
            Assert.That(_agent.TryBoard(_vehicle), Is.False);
        }

        [Test]
        public void BoardingStopsTheNavMeshAgentAndPutsTheBodyInTheSeat()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' board rider=enemy-1 seat=0 role=Driver");
            Assert.That(_agent.TryBoard(_vehicle), Is.True);

            Assert.That(_agent.IsRiding, Is.True);
            Assert.That(_agent.Vehicle, Is.SameAs(_vehicle));
            Assert.That(_agent.Seat, Is.SameAs(_seat));
            Assert.That(_navAgent.enabled, Is.False);
            Assert.That(_agent.IsCrouching, Is.True, "a seated rider crouches");
            AssertNear(_agent.transform.position, _seatPosition + (Vector3.up * _navAgent.baseOffset));
        }

        [Test]
        public void LeavingPutsTheBodyAtTheExitAndRestartsTheNavMeshAgent()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.TryBoard(_vehicle, 0, requireInRange: false);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' leave rider=enemy-1 seat=0 reason=Request exit=0");
            Assert.That(_agent.TryLeaveVehicle(), Is.True);

            Assert.That(_agent.IsRiding, Is.False);
            Assert.That(_navAgent.enabled, Is.True);
            Assert.That(_agent.IsCrouching, Is.False);
            AssertNear(_agent.transform.position, _exit + (Vector3.up * _navAgent.baseOffset));
            Assert.That(_agent.TryLeaveVehicle(), Is.False, "not riding any more");
        }

        [Test]
        public void DyingInTheSeatLeavesWithoutRestartingTheNavMeshAgent()
        {
            Assert.That(_agent.TryInitialize(), Is.True);
            _agent.TryBoard(_vehicle);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Jeep' leave rider=enemy-1 seat=0 reason=RiderDied exit=0");
            _agent.GetComponent<Damageable>().TakeDamage(10);

            Assert.That(_agent.IsRiding, Is.False);
            Assert.That(_navAgent.enabled, Is.False);
            Assert.That(_seat.IsOccupied, Is.False);
        }

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"expected {expected} but was {actual}");
        }
    }
}
