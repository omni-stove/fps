using System;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MeleeWitnessesTests
    {
        private GameObject _host;
        private FakeMeleeWitness _witness;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Witness");
            _witness = _host.AddComponent<FakeMeleeWitness>();
        }

        [TearDown]
        public void TearDown()
        {
            MeleeWitnesses.Remove(_witness);
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void AWitnessIsListedOnceUntilRemoved()
        {
            MeleeWitnesses.Add(_witness);
            MeleeWitnesses.Add(_witness);

            Assert.That(MeleeWitnesses.Active, Has.Exactly(1).SameAs(_witness));

            Assert.That(MeleeWitnesses.Remove(_witness), Is.True);
            Assert.That(MeleeWitnesses.Active, Has.No.Member(_witness));
            Assert.That(MeleeWitnesses.Remove(_witness), Is.False, "removing twice is harmless");
        }

        [Test]
        public void NullIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => MeleeWitnesses.Add(null));
            Assert.That(MeleeWitnesses.Remove(null), Is.False);
        }
    }
}
