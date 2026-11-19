using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class HelicopterDefinitionTests
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

            Assert.That(spec.Mass, Is.EqualTo(HelicopterSpec.DefaultMass));
            Assert.That(spec.MaxSpeed, Is.EqualTo(HelicopterSpec.DefaultMaxSpeed));
            Assert.That(spec.HorizontalResponse, Is.EqualTo(HelicopterSpec.DefaultHorizontalResponse));
            Assert.That(spec.MaxHorizontalAcceleration, Is.EqualTo(HelicopterSpec.DefaultMaxHorizontalAcceleration));
            Assert.That(spec.MaxClimbRate, Is.EqualTo(HelicopterSpec.DefaultMaxClimbRate));
            Assert.That(spec.MaxDescentRate, Is.EqualTo(HelicopterSpec.DefaultMaxDescentRate));
            Assert.That(spec.VerticalResponse, Is.EqualTo(HelicopterSpec.DefaultVerticalResponse));
            Assert.That(spec.MaxVerticalAcceleration, Is.EqualTo(HelicopterSpec.DefaultMaxVerticalAcceleration));
            Assert.That(spec.GroundEffectHeight, Is.EqualTo(HelicopterSpec.DefaultGroundEffectHeight));
            Assert.That(spec.GroundEffectBoost, Is.EqualTo(HelicopterSpec.DefaultGroundEffectBoost));
            Assert.That(spec.MaxTiltAngle, Is.EqualTo(HelicopterSpec.DefaultMaxTiltAngle));
            Assert.That(spec.TiltStiffness, Is.EqualTo(HelicopterSpec.DefaultTiltStiffness));
            Assert.That(spec.TiltDamping, Is.EqualTo(HelicopterSpec.DefaultTiltDamping));
            Assert.That(spec.MaxYawRate, Is.EqualTo(HelicopterSpec.DefaultMaxYawRate));
            Assert.That(spec.YawResponse, Is.EqualTo(HelicopterSpec.DefaultYawResponse));
            Assert.That(spec.YawAimAngle, Is.EqualTo(HelicopterSpec.DefaultYawAimAngle));
            Assert.That(spec.SpoolSeconds, Is.EqualTo(HelicopterSpec.DefaultSpoolSeconds));
        }

        [Test]
        public void CopyFromRoundTripsEveryValue()
        {
            var source = new HelicopterSpec(
                mass: 1200f,
                maxSpeed: 22f,
                horizontalResponse: 2.5f,
                maxHorizontalAcceleration: 9f,
                maxClimbRate: 7f,
                maxDescentRate: 5f,
                verticalResponse: 4f,
                maxVerticalAcceleration: 3f,
                groundEffectHeight: 6f,
                groundEffectBoost: 0.25f,
                maxTiltAngle: 35f,
                tiltStiffness: 30f,
                tiltDamping: 11f,
                maxYawRate: 120f,
                yawResponse: 5f,
                yawAimAngle: 50f,
                spoolSeconds: 1.5f);
            var definition = NewDefinition("Copied");

            definition.CopyFrom(source);
            var spec = definition.ToSpec();

            Assert.That(spec.Mass, Is.EqualTo(1200f));
            Assert.That(spec.MaxSpeed, Is.EqualTo(22f));
            Assert.That(spec.HorizontalResponse, Is.EqualTo(2.5f));
            Assert.That(spec.MaxHorizontalAcceleration, Is.EqualTo(9f));
            Assert.That(spec.MaxClimbRate, Is.EqualTo(7f));
            Assert.That(spec.MaxDescentRate, Is.EqualTo(5f));
            Assert.That(spec.VerticalResponse, Is.EqualTo(4f));
            Assert.That(spec.MaxVerticalAcceleration, Is.EqualTo(3f));
            Assert.That(spec.GroundEffectHeight, Is.EqualTo(6f));
            Assert.That(spec.GroundEffectBoost, Is.EqualTo(0.25f));
            Assert.That(spec.MaxTiltAngle, Is.EqualTo(35f));
            Assert.That(spec.TiltStiffness, Is.EqualTo(30f));
            Assert.That(spec.TiltDamping, Is.EqualTo(11f));
            Assert.That(spec.MaxYawRate, Is.EqualTo(120f));
            Assert.That(spec.YawResponse, Is.EqualTo(5f));
            Assert.That(spec.YawAimAngle, Is.EqualTo(50f));
            Assert.That(spec.SpoolSeconds, Is.EqualTo(1.5f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void AnInvalidValueNamesTheAsset()
        {
            var definition = NewDefinition("Broken Heli");
            definition.MaxSpeed = 0f;

            var error = Assert.Throws<ArgumentException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("Broken Heli"));
            Assert.That(error.Message, Does.Contain("maxSpeed"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        private HelicopterDefinition NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<HelicopterDefinition>();
            definition.name = name;
            _created.Add(definition);
            return definition;
        }
    }
}
