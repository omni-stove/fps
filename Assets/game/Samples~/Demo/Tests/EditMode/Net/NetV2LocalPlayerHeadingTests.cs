using System;
using System.Reflection;
using NUnit.Framework;
using Shooter.NetV2;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2LocalPlayerHeadingTests
    {
        private const float Tolerance = 1e-3f;

        private GameObject _root;
        private NetV2LocalPlayer _player;

        [SetUp]
        public void SetUp()
        {
            GameSignals.LocalHeadingDegrees.Clear();
            _root = new GameObject(NetV2LocalPlayer.ObjectName);
            _player = _root.AddComponent<NetV2LocalPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            GameSignals.LocalHeadingDegrees.Clear();
        }

        [Test]
        public void TheHeadingIsTheBodyYawClockwiseFromNorth()
        {
            _root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.That(_player.HeadingDegrees, Is.EqualTo(90f).Within(Tolerance));

            _root.transform.rotation = Quaternion.Euler(0f, -30f, 0f);
            Assert.That(_player.HeadingDegrees, Is.EqualTo(330f).Within(Tolerance));
        }

        [Test]
        public void EnablingPublishesALiveHeadingAndDisablingWithdrawsIt()
        {
            InvokeLifecycle("OnEnable");
            var source = GameSignals.LocalHeadingDegrees.Value;
            Assert.That(source, Is.Not.Null);

            _root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            Assert.That(source(), Is.EqualTo(45f).Within(Tolerance), "the source reads the yaw at the time it is called");
            _root.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            Assert.That(source(), Is.EqualTo(200f).Within(Tolerance));

            InvokeLifecycle("OnDisable");
            Assert.That(GameSignals.LocalHeadingDegrees.Value, Is.Null);
        }

        [Test]
        public void DisablingLeavesASourcePublishedByAnotherPart()
        {
            InvokeLifecycle("OnEnable");
            Func<float> other = () => 10f;
            GameSignals.LocalHeadingDegrees.Publish(other);

            InvokeLifecycle("OnDisable");

            Assert.That(GameSignals.LocalHeadingDegrees.Value, Is.SameAs(other));
        }

        private void InvokeLifecycle(string method)
        {
            // Edit Mode のテストでは OnEnable と OnDisable を Unity が呼ばないので、直に呼ぶ。
            typeof(NetV2LocalPlayer)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_player, null);
        }
    }
}
