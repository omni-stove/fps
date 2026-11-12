using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    public sealed class VectorConversionsTests
    {
        [Test]
        public void NumericsVector3ToUnityKeepsComponents()
        {
            var unity = new System.Numerics.Vector3(1.5f, -2f, 3.25f).ToUnity();

            Assert.That(unity.x, Is.EqualTo(1.5f));
            Assert.That(unity.y, Is.EqualTo(-2f));
            Assert.That(unity.z, Is.EqualTo(3.25f));
        }

        [Test]
        public void UnityVector3ToNumericsKeepsComponents()
        {
            var numerics = new UnityEngine.Vector3(-4f, 0.5f, 7f).ToNumerics();

            Assert.That(numerics.X, Is.EqualTo(-4f));
            Assert.That(numerics.Y, Is.EqualTo(0.5f));
            Assert.That(numerics.Z, Is.EqualTo(7f));
        }

        [Test]
        public void NumericsVector2ToUnityKeepsComponents()
        {
            var unity = new System.Numerics.Vector2(0.25f, -1f).ToUnity();

            Assert.That(unity.x, Is.EqualTo(0.25f));
            Assert.That(unity.y, Is.EqualTo(-1f));
        }

        [Test]
        public void UnityVector2ToNumericsKeepsComponents()
        {
            var numerics = new UnityEngine.Vector2(-0.75f, 2f).ToNumerics();

            Assert.That(numerics.X, Is.EqualTo(-0.75f));
            Assert.That(numerics.Y, Is.EqualTo(2f));
        }

        [Test]
        public void Vector3RoundTripIsExact()
        {
            var original = new UnityEngine.Vector3(1e-7f, -123456.78f, float.MaxValue);

            Assert.That(original.ToNumerics().ToUnity(), Is.EqualTo(original));
        }

        [Test]
        public void Vector2RoundTripIsExact()
        {
            var original = new System.Numerics.Vector2(float.Epsilon, -9.81f);

            Assert.That(original.ToUnity().ToNumerics(), Is.EqualTo(original));
        }

        [Test]
        public void ZeroStaysZero()
        {
            Assert.That(System.Numerics.Vector3.Zero.ToUnity(), Is.EqualTo(UnityEngine.Vector3.zero));
            Assert.That(UnityEngine.Vector3.zero.ToNumerics(), Is.EqualTo(System.Numerics.Vector3.Zero));
        }
    }
}
