using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyGaitDefinitionTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;
        private EnemyDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.name = "Grunt";

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _host.GetComponent<NavMeshAgent>().speed = 4f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void TheDefaultDefinitionSprintsAndCrouchesAtTheKitRatios()
        {
            _agent.Definition = _definition;

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.WalkSpeed, Is.EqualTo(4f));
            Assert.That(_agent.SprintSpeed, Is.EqualTo(6.4f).Within(1e-5f));
            Assert.That(_agent.CrouchSpeed, Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void TheDefinitionSprintRatioSetsTheSprintSpeed()
        {
            _definition.SprintRatio = 2.5f;
            _definition.CrouchRatio = 0.25f;
            _agent.Definition = _definition;

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.SprintSpeed, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(_agent.CrouchSpeed, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TheDefinitionArrivalDistanceReachesTheBrain()
        {
            _definition.ArrivalDistance = 2f;
            _agent.Definition = _definition;

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.Brain.Spec.ArrivalDistance, Is.EqualTo(2f));
        }
    }
}
