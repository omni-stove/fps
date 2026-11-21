using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class HorseDefinitionTests
    {
        private readonly List<Object> _created = new List<Object>();

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
        public void ANewDefinitionStartsFromTheSpecDefaults()
        {
            var spec = NewDefinition("Fresh").ToSpec();

            Assert.That(spec.Mass, Is.EqualTo(HorseSpec.DefaultMass));
            Assert.That(spec.WalkSpeed, Is.EqualTo(HorseSpec.DefaultWalkSpeed));
            Assert.That(spec.TrotSpeed, Is.EqualTo(HorseSpec.DefaultTrotSpeed));
            Assert.That(spec.CanterSpeed, Is.EqualTo(HorseSpec.DefaultCanterSpeed));
            Assert.That(spec.ReverseSpeed, Is.EqualTo(HorseSpec.DefaultReverseSpeed));
            Assert.That(spec.Acceleration, Is.EqualTo(HorseSpec.DefaultAcceleration));
            Assert.That(spec.Deceleration, Is.EqualTo(HorseSpec.DefaultDeceleration));
            Assert.That(spec.TurnRate, Is.EqualTo(HorseSpec.DefaultTurnRate));
            Assert.That(spec.CanterTurnScale, Is.EqualTo(HorseSpec.DefaultCanterTurnScale));
            Assert.That(spec.JumpSpeed, Is.EqualTo(HorseSpec.DefaultJumpSpeed));
            Assert.That(spec.MaxSlopeAngle, Is.EqualTo(HorseSpec.DefaultMaxSlopeAngle));
            Assert.That(spec.WalkThreshold, Is.EqualTo(HorseSpec.DefaultWalkThreshold));
            Assert.That(spec.TrotThreshold, Is.EqualTo(HorseSpec.DefaultTrotThreshold));
            Assert.That(spec.CanterThreshold, Is.EqualTo(HorseSpec.DefaultCanterThreshold));
        }

        [Test]
        public void CopyFromAndToSpecKeepEveryValue()
        {
            var source = new HorseSpec(
                mass: 650f,
                walkSpeed: 2f,
                trotSpeed: 5f,
                canterSpeed: 11f,
                reverseSpeed: 1f,
                acceleration: 3f,
                deceleration: 7f,
                turnRate: 90f,
                canterTurnScale: 0.5f,
                jumpSpeed: 6f,
                maxSlopeAngle: 35f,
                walkThreshold: 0.3f,
                trotThreshold: 3f,
                canterThreshold: 7f);
            var definition = NewDefinition("Destrier");

            definition.CopyFrom(source);
            var spec = definition.ToSpec();

            Assert.That(spec.Mass, Is.EqualTo(650f));
            Assert.That(spec.WalkSpeed, Is.EqualTo(2f));
            Assert.That(spec.TrotSpeed, Is.EqualTo(5f));
            Assert.That(spec.CanterSpeed, Is.EqualTo(11f));
            Assert.That(spec.ReverseSpeed, Is.EqualTo(1f));
            Assert.That(spec.Acceleration, Is.EqualTo(3f));
            Assert.That(spec.Deceleration, Is.EqualTo(7f));
            Assert.That(spec.TurnRate, Is.EqualTo(90f));
            Assert.That(spec.CanterTurnScale, Is.EqualTo(0.5f));
            Assert.That(spec.JumpSpeed, Is.EqualTo(6f));
            Assert.That(spec.MaxSlopeAngle, Is.EqualTo(35f));
            Assert.That(spec.WalkThreshold, Is.EqualTo(0.3f));
            Assert.That(spec.TrotThreshold, Is.EqualTo(3f));
            Assert.That(spec.CanterThreshold, Is.EqualTo(7f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ThePropertiesWriteTheFieldsToSpecReads()
        {
            var definition = NewDefinition("Pony");

            definition.Mass = 300f;
            definition.CanterSpeed = 7f;
            definition.CanterThreshold = 5f;
            definition.JumpSpeed = 0f;
            var spec = definition.ToSpec();

            Assert.That(definition.Mass, Is.EqualTo(300f));
            Assert.That(spec.Mass, Is.EqualTo(300f));
            Assert.That(spec.CanterSpeed, Is.EqualTo(7f));
            Assert.That(spec.CanterThreshold, Is.EqualTo(5f));
            Assert.That(spec.JumpSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void AnInvalidValueNamesTheAsset()
        {
            var definition = NewDefinition("Lame");
            definition.WalkSpeed = 3f;
            definition.TrotSpeed = 2f;

            var error = Assert.Throws<ArgumentException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("horse 'Lame' is invalid"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AnUnnamedAssetIsCalledOutAsUnnamed()
        {
            var definition = NewDefinition(string.Empty);
            definition.MaxSlopeAngle = 90f;

            var error = Assert.Throws<ArgumentException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("horse '(unnamed horse)' is invalid"));
        }

        private HorseDefinition NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<HorseDefinition>();
            definition.name = name;
            _created.Add(definition);
            return definition;
        }
    }
}
