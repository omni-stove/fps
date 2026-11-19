using System;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StealthTargetTests
    {
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void PostureComesFromCrouchingAndProne()
        {
            var motion = _player.AddComponent<FakeCharacterMotionSource>();
            var target = _player.AddComponent<StealthTarget>();
            Assert.That(target.Posture, Is.EqualTo(AwarenessPosture.Standing));

            motion.IsCrouched = true;
            Assert.That(target.Posture, Is.EqualTo(AwarenessPosture.Crouching));

            target.IsProne = true;
            Assert.That(target.Posture, Is.EqualTo(AwarenessPosture.Prone), "prone wins over crouching");
        }

        [Test]
        public void WithoutAMotionSourceTheTargetStands()
        {
            var target = _player.AddComponent<StealthTarget>();

            Assert.That(target.Posture, Is.EqualTo(AwarenessPosture.Standing));
            Assert.That(target.Speed, Is.EqualTo(0f));
            Assert.That(target.LightFactor, Is.EqualTo(1f));
            Assert.That(target.Rule, Is.EqualTo(Stealth.Off));
        }

        [Test]
        public void OutOfRangeValuesAreRejected()
        {
            var target = _player.AddComponent<StealthTarget>();

            Assert.Throws<ArgumentOutOfRangeException>(() => target.LightFactor = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => target.LightFactor = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => target.Rule = (Stealth)5);

            target.LightFactor = 0.25f;
            Assert.That(target.LightFactor, Is.EqualTo(0.25f));
        }

        [Test]
        public void TheInstallerPutsTheRuleOnThePlayerAndIsRegistered()
        {
            var installer = new StealthActionInstaller();

            installer.Install(_player, GameRules.Default.WithActions(ActionRules.Default.WithStealth(Stealth.On)));
            Assert.That(_player.GetComponent<StealthTarget>().Rule, Is.EqualTo(Stealth.On));

            installer.Install(_player, GameRules.Default);
            Assert.That(_player.GetComponents<StealthTarget>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponent<StealthTarget>().Rule, Is.EqualTo(Stealth.Off));

            Assert.That(PlayerActions.Installers.Any(i => i is StealthActionInstaller), Is.True);
            Assert.That(StealthActionInstaller.InstallOrder, Is.LessThan(GrenadeActionInstaller.InstallOrder));
        }

        [Test]
        public void ThePlaceholderMarkerShowsTheStageColor()
        {
            var marker = AwarenessMarker.Create("enemy-1");
            try
            {
                Assert.That(marker.name, Is.EqualTo(AwarenessMarker.ObjectNamePrefix + "enemy-1"));
                Assert.That(marker.GetComponent<Collider>(), Is.Null, "the marker must not stop bullets");
                Assert.That(marker.IsShown, Is.False);

                marker.Show(AwarenessStage.Noticed, 0.2f);
                Assert.That(marker.IsShown, Is.True);
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.NoticedColor).Using(ColorEqualityComparer.Instance));
                var small = marker.transform.localScale.x;

                marker.Show(AwarenessStage.Alerted, 0.8f);
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.AlertedColor).Using(ColorEqualityComparer.Instance));
                Assert.That(marker.transform.localScale.x, Is.GreaterThan(small), "grows as the meter fills");

                marker.Show(AwarenessStage.Engaged, 1f);
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.EngagedColor).Using(ColorEqualityComparer.Instance));
                Assert.That(marker.transform.localScale.x, Is.EqualTo(AwarenessMarker.Size).Within(1e-5f));

                marker.Show(AwarenessStage.Unaware, 0f);
                Assert.That(marker.IsShown, Is.False);
            }
            finally
            {
                marker.Remove();
            }
        }

        [Test]
        public void WalkingWithStealthOnMakesFootstepNoises()
        {
            _player.AddComponent<FakeCharacterMotionSource>();
            var target = _player.AddComponent<StealthTarget>();
            target.Rule = Stealth.On;

            Assert.That(Walk(target, 30), Is.GreaterThan(0));
        }

        [Test]
        public void NoFootstepNoisesWithStealthOffASilentRadiusOrNoMotion()
        {
            var target = _player.AddComponent<StealthTarget>();
            target.Rule = Stealth.On;
            Assert.That(Walk(target, 30), Is.Zero, "without a motion source it cannot tell whether the feet touch the ground");

            _player.AddComponent<FakeCharacterMotionSource>();
            target.Rule = Stealth.Off;
            Assert.That(Walk(target, 30), Is.Zero);

            target.Rule = Stealth.On;
            target.FootstepNoiseRadius = 0f;
            Assert.That(Walk(target, 30), Is.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => target.FootstepNoiseRadius = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => target.FootstepNoiseRadius = float.NaN);
        }

        // 1 フレーム 0.02 秒で 0.1 m (5 m/s、歩きの速さ) 進める。
        private static int Walk(StealthTarget target, int frames)
        {
            var steps = 0;
            for (var i = 0; i < frames; i++)
            {
                if (target.AdvanceFootsteps(new Vector3(0f, 0f, 0.1f), 0.02f))
                {
                    steps++;
                }
            }

            return steps;
        }
    }
}
