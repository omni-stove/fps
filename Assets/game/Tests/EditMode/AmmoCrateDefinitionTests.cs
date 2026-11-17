using System;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AmmoCrateDefinitionTests
    {
        private AmmoCrate _crate;
        private PickupDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _crate = new GameObject("Crate").AddComponent<AmmoCrate>();
            _crate.Build();
            _definition = ScriptableObject.CreateInstance<PickupDefinition>();
            _definition.name = "CrateDefinition";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_crate.gameObject);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ANewDefinitionHoldsTheAmmoCrateDefaults()
        {
            Assert.That(_definition.AmmoCrateRadius, Is.EqualTo(AmmoCrate.DefaultRadius));
            Assert.That(_definition.AmmoCrateCooldownSeconds, Is.EqualTo(AmmoCrate.DefaultCooldownSeconds));
        }

        [Test]
        public void WithoutADefinitionTheCrateUsesItsOwnValues()
        {
            _crate.Radius = 2.5f;
            _crate.CooldownSeconds = 3f;

            Assert.That(_crate.EffectiveRadius, Is.EqualTo(2.5f));
            Assert.That(_crate.EffectiveCooldownSeconds, Is.EqualTo(3f));
            Assert.That(_crate.GetComponent<SphereCollider>().radius, Is.EqualTo(2.5f));
        }

        [Test]
        public void TheDefinitionCooldownAndRadiusReplaceTheCratesOwn()
        {
            _crate.Radius = 2.5f;
            _crate.CooldownSeconds = 3f;
            _definition.AmmoCrateRadius = 4f;
            _definition.AmmoCrateCooldownSeconds = 5f;

            _crate.Definition = _definition;

            Assert.That(_crate.EffectiveRadius, Is.EqualTo(4f));
            Assert.That(_crate.EffectiveCooldownSeconds, Is.EqualTo(5f));
            Assert.That(_crate.Radius, Is.EqualTo(2.5f), "the crate's own value is kept for when the definition is removed");
            Assert.That(_crate.CooldownSeconds, Is.EqualTo(3f));
            var trigger = _crate.GetComponent<SphereCollider>();
            Assert.That(trigger.radius, Is.EqualTo(4f));
            Assert.That(trigger.center, Is.EqualTo(new Vector3(0f, 4f, 0f)));
        }

        [Test]
        public void RemovingTheDefinitionGoesBackToTheCratesOwnRadius()
        {
            _definition.AmmoCrateRadius = 4f;
            _crate.Definition = _definition;

            _crate.Definition = null;

            Assert.That(_crate.EffectiveRadius, Is.EqualTo(AmmoCrate.DefaultRadius));
            Assert.That(_crate.GetComponent<SphereCollider>().radius, Is.EqualTo(AmmoCrate.DefaultRadius));
        }

        [Test]
        public void ADefinitionRejectsAnInvalidRadiusOrCooldown()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _definition.AmmoCrateRadius = 0.05f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _definition.AmmoCrateRadius = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => _definition.AmmoCrateCooldownSeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _definition.AmmoCrateCooldownSeconds = float.PositiveInfinity);
            Assert.That(_definition.AmmoCrateRadius, Is.EqualTo(AmmoCrate.DefaultRadius));
            Assert.That(_definition.AmmoCrateCooldownSeconds, Is.EqualTo(AmmoCrate.DefaultCooldownSeconds));
        }
    }
}
