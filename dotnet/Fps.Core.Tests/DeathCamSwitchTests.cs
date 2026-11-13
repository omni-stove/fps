using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DeathCamSwitchTests
    {
        [Test]
        public void StartsWhenDyingAndStopsOnRespawn()
        {
            var deathCam = new DeathCamSwitch();

            Assert.That(deathCam.Update(DeathCamera.Killer, true, true, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.Started));
            Assert.That(deathCam.IsActive, Is.True);

            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.IsActive, Is.True, "keeps watching until the respawn");

            Assert.That(deathCam.Update(DeathCamera.Killer, true, true, false), Is.EqualTo(DeathCamTransition.Respawned));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void NoneNeverStarts()
        {
            var deathCam = new DeathCamSwitch();

            deathCam.Update(DeathCamera.None, true, true, false);

            Assert.That(deathCam.Update(DeathCamera.None, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void DoesNotStartBeforeEverBeingAlive()
        {
            // 参加前は体力 0 で届くので、倒れたのと区別する
            var deathCam = new DeathCamSwitch();

            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void DoesNotStartWithoutTheLocalView()
        {
            var deathCam = new DeathCamSwitch();
            deathCam.Update(DeathCamera.Killer, false, true, false);

            Assert.That(deathCam.Update(DeathCamera.Killer, false, false, false), Is.EqualTo(DeathCamTransition.None));
        }

        [Test]
        public void DoesNotStartAfterTheMatchEnded()
        {
            var deathCam = new DeathCamSwitch();
            deathCam.Update(DeathCamera.Killer, true, true, false);

            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, true), Is.EqualTo(DeathCamTransition.None));
        }

        [Test]
        public void MatchEndYieldsToTheEndOfMatchView()
        {
            var deathCam = Dead();

            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, true), Is.EqualTo(DeathCamTransition.MatchEnded));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void LosingTheViewStops()
        {
            var deathCam = Dead();

            Assert.That(deathCam.Update(DeathCamera.Killer, false, false, false), Is.EqualTo(DeathCamTransition.ViewLost));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void RuleTurningOffStops()
        {
            var deathCam = Dead();

            Assert.That(deathCam.Update(DeathCamera.None, true, false, false), Is.EqualTo(DeathCamTransition.RuleDisabled));
        }

        [TestCase(DeathCamera.Ally)]
        [TestCase(DeathCamera.Free)]
        public void AllyAndFreeStartAndStopLikeKiller(DeathCamera mode)
        {
            var deathCam = new DeathCamSwitch();
            deathCam.Update(mode, true, true, false);

            Assert.That(deathCam.Update(mode, true, false, false), Is.EqualTo(DeathCamTransition.Started));
            Assert.That(deathCam.Update(mode, true, true, false), Is.EqualTo(DeathCamTransition.Respawned));
        }

        [Test]
        public void SwitchingBetweenShowingRulesKeepsWatching()
        {
            // 何を映すかは呼び出し側が今のルールで決めるので、None になるまでは止めない
            var deathCam = Dead();

            Assert.That(deathCam.Update(DeathCamera.Ally, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.Update(DeathCamera.Free, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.IsActive, Is.True);
            Assert.That(deathCam.Update(DeathCamera.None, true, false, false), Is.EqualTo(DeathCamTransition.RuleDisabled));
        }

        [Test]
        public void MatchEndWinsOverRespawnInTheSameUpdate()
        {
            var deathCam = Dead();

            Assert.That(deathCam.Update(DeathCamera.Killer, true, true, true), Is.EqualTo(DeathCamTransition.MatchEnded));
        }

        [Test]
        public void DoesNotRestartWhileStillDeadAfterStopping()
        {
            var deathCam = Dead();
            deathCam.Update(DeathCamera.Killer, true, false, true);

            // 試合が次へ進んでも、倒れたままなら次に倒れるまで始めない
            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.Update(DeathCamera.Killer, true, true, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.Started));
        }

        [Test]
        public void ResetStopsAndForgetsBeingAlive()
        {
            var deathCam = Dead();

            Assert.That(deathCam.Reset(), Is.True);
            Assert.That(deathCam.IsActive, Is.False);
            Assert.That(deathCam.Reset(), Is.False);
            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.None));
        }

        private static DeathCamSwitch Dead()
        {
            var deathCam = new DeathCamSwitch();
            deathCam.Update(DeathCamera.Killer, true, true, false);
            Assert.That(deathCam.Update(DeathCamera.Killer, true, false, false), Is.EqualTo(DeathCamTransition.Started));
            return deathCam;
        }
    }
}
