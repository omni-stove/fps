using System;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
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
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.NoticedColor));
                var small = marker.transform.localScale.x;

                marker.Show(AwarenessStage.Alerted, 0.8f);
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.AlertedColor));
                Assert.That(marker.transform.localScale.x, Is.GreaterThan(small), "grows as the meter fills");

                marker.Show(AwarenessStage.Engaged, 1f);
                Assert.That(marker.ShownColor, Is.EqualTo(AwarenessMarker.EngagedColor));
                Assert.That(marker.transform.localScale.x, Is.EqualTo(AwarenessMarker.Size).Within(1e-5f));

                marker.Show(AwarenessStage.Unaware, 0f);
                Assert.That(marker.IsShown, Is.False);
            }
            finally
            {
                marker.Remove();
            }
        }
    }
}
