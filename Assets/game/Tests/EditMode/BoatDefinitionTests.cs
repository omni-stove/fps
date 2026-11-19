using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class BoatDefinitionTests
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

            Assert.That(spec.Mass, Is.EqualTo(BoatSpec.DefaultMass));
            Assert.That(spec.Length, Is.EqualTo(BoatSpec.DefaultLength));
            Assert.That(spec.Width, Is.EqualTo(BoatSpec.DefaultWidth));
            Assert.That(spec.HullBottom, Is.EqualTo(BoatSpec.DefaultHullBottom));
            Assert.That(spec.HullHeight, Is.EqualTo(BoatSpec.DefaultHullHeight));
            Assert.That(spec.Draft, Is.EqualTo(BoatSpec.DefaultDraft));
            Assert.That(spec.HeaveDamping, Is.EqualTo(BoatSpec.DefaultHeaveDamping));
            Assert.That(spec.ForwardDrag, Is.EqualTo(BoatSpec.DefaultForwardDrag));
            Assert.That(spec.SideDrag, Is.EqualTo(BoatSpec.DefaultSideDrag));
            Assert.That(spec.Thrust, Is.EqualTo(BoatSpec.DefaultThrust));
            Assert.That(spec.MaxForwardSpeed, Is.EqualTo(BoatSpec.DefaultMaxForwardSpeed));
            Assert.That(spec.MaxReverseSpeed, Is.EqualTo(BoatSpec.DefaultMaxReverseSpeed));
            Assert.That(spec.TurnRate, Is.EqualTo(BoatSpec.DefaultTurnRate));
            Assert.That(spec.TurnSpeed, Is.EqualTo(BoatSpec.DefaultTurnSpeed));
            Assert.That(spec.TurnResponsiveness, Is.EqualTo(BoatSpec.DefaultTurnResponsiveness));
        }

        [Test]
        public void CopyFromAndToSpecKeepEveryValue()
        {
            var source = new BoatSpec(
                mass: 1500f,
                length: 6f,
                width: 2.5f,
                hullBottom: -0.8f,
                hullHeight: 1.4f,
                draft: 0.5f,
                heaveDamping: 6f,
                forwardDrag: 0.2f,
                sideDrag: 4f,
                thrust: 8f,
                maxForwardSpeed: 18f,
                maxReverseSpeed: 3f,
                turnRate: 40f,
                turnSpeed: 5f,
                turnResponsiveness: 1.5f);
            var definition = NewDefinition("Patrol");

            definition.CopyFrom(source);
            var spec = definition.ToSpec();

            Assert.That(spec.Mass, Is.EqualTo(1500f));
            Assert.That(spec.Length, Is.EqualTo(6f));
            Assert.That(spec.Width, Is.EqualTo(2.5f));
            Assert.That(spec.HullBottom, Is.EqualTo(-0.8f));
            Assert.That(spec.HullHeight, Is.EqualTo(1.4f));
            Assert.That(spec.Draft, Is.EqualTo(0.5f));
            Assert.That(spec.HeaveDamping, Is.EqualTo(6f));
            Assert.That(spec.ForwardDrag, Is.EqualTo(0.2f));
            Assert.That(spec.SideDrag, Is.EqualTo(4f));
            Assert.That(spec.Thrust, Is.EqualTo(8f));
            Assert.That(spec.MaxForwardSpeed, Is.EqualTo(18f));
            Assert.That(spec.MaxReverseSpeed, Is.EqualTo(3f));
            Assert.That(spec.TurnRate, Is.EqualTo(40f));
            Assert.That(spec.TurnSpeed, Is.EqualTo(5f));
            Assert.That(spec.TurnResponsiveness, Is.EqualTo(1.5f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void AnInvalidValueNamesTheAsset()
        {
            var definition = NewDefinition("Leaky");
            definition.HullHeight = 0.5f;
            definition.Draft = 0.8f;

            var error = Assert.Throws<ArgumentException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("boat 'Leaky' is invalid"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        private BoatDefinition NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<BoatDefinition>();
            definition.name = name;
            _created.Add(definition);
            return definition;
        }
    }
}
