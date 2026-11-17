using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemyAimTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ItFacesTheTargetOnlyWithinTheTolerance()
        {
            var agent = CreateEnemy(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, fireRange: 18f, fireToleranceDegrees: 15f));
            agent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Assert.That(agent.IsFacing(new System.Numerics.Vector3(0f, 0f, 10f)), Is.True, "straight ahead");
            Assert.That(agent.IsFacing(Toward(10f)), Is.True, "10 degrees off");
            Assert.That(agent.IsFacing(Toward(30f)), Is.False, "30 degrees off");
            Assert.That(agent.IsFacing(new System.Numerics.Vector3(0f, 0f, -10f)), Is.False, "behind");
            Assert.That(agent.IsFacing(null), Is.True);
        }

        [Test]
        public void AToleranceOf180FiresWhicheverWayItFaces()
        {
            var agent = CreateEnemy(new EnemySpec(20f, 10f, 1f, 2f, 0.5f));
            agent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Assert.That(agent.IsFacing(new System.Numerics.Vector3(0f, 0f, -10f)), Is.True);
        }

        [Test]
        public void TheDefinitionCarriesTheFireRangeAndTolerance()
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _created.Add(definition);
            definition.FireRange = 16f;
            definition.FireToleranceDegrees = 25f;

            var spec = definition.ToSpec();

            Assert.That(spec.FireRange, Is.EqualTo(16f));
            Assert.That(spec.FireToleranceDegrees, Is.EqualTo(25f));
        }

        private static System.Numerics.Vector3 Toward(float degrees)
        {
            var direction = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * 10f;
            return new System.Numerics.Vector3(direction.x, direction.y, direction.z);
        }

        private EnemyAgent CreateEnemy(EnemySpec spec)
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(rifle);
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());

            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(host);
            host.name = "Enemy";
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = rifle;
            agent.ApplySpec(spec);
            Assert.That(agent.TryInitialize(), Is.True);
            return agent;
        }
    }
}
