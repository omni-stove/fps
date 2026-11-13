using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class RecoilStateTests
    {
        private const string _id = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";

        private static WeaponSpec Spec(float pitch = 0f, float yaw = 0f, float recovery = 0f)
        {
            return new WeaponSpec(
                _id, "Test", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 10, 100f, DamageFalloff.None,
                recoilPitchDegrees: pitch, recoilYawDegrees: yaw, recoilRecoveryPerSecond: recovery);
        }

        [Test]
        public void StartsAtRest()
        {
            var recoil = new RecoilState();

            Assert.That(recoil.IsAtRest, Is.True);
            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(0f));
            Assert.That(recoil.YawDegrees, Is.EqualTo(0f));
            Assert.That(recoil.RecoveryDelaySeconds, Is.EqualTo(RecoilState.DefaultRecoveryDelaySeconds));
        }

        [Test]
        public void EachKickAddsThePitch()
        {
            var recoil = new RecoilState();
            var spec = Spec(pitch: 1.5f);

            recoil.Kick(spec, 1, 0);
            recoil.Kick(spec, 1, 1);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(3f));
            Assert.That(recoil.YawDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void YawKickStaysWithinTheWeaponLimitAndGoesBothWays()
        {
            var spec = Spec(yaw: 2f);
            var left = 0;
            var right = 0;

            for (var shot = 0; shot < 200; shot++)
            {
                var yaw = RecoilState.YawKick(spec, 9, shot);

                Assert.That(Math.Abs(yaw), Is.LessThanOrEqualTo(2f));
                left += yaw < 0f ? 1 : 0;
                right += yaw > 0f ? 1 : 0;
            }

            Assert.That(left, Is.GreaterThan(50));
            Assert.That(right, Is.GreaterThan(50));
        }

        [Test]
        public void SameSeedAlwaysKicksTheSameWay()
        {
            var spec = Spec(pitch: 1f, yaw: 2f);
            var first = new RecoilState();
            var second = new RecoilState();

            for (var shot = 0; shot < 20; shot++)
            {
                first.Kick(spec, 4, shot);
                second.Kick(spec, 4, shot);
            }

            Assert.That(second.PitchUpDegrees, Is.EqualTo(first.PitchUpDegrees));
            Assert.That(second.YawDegrees, Is.EqualTo(first.YawDegrees));
        }

        [Test]
        public void KickAddsTheSeededYaw()
        {
            var spec = Spec(yaw: 2f);
            var recoil = new RecoilState();

            recoil.Kick(spec, 4, 0);
            recoil.Kick(spec, 4, 1);

            var expected = RecoilState.YawKick(spec, 4, 0) + RecoilState.YawKick(spec, 4, 1);
            Assert.That(recoil.YawDegrees, Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void DoesNotRecoverBeforeTheDelay()
        {
            var recoil = new RecoilState(0.2f);
            recoil.Kick(Spec(pitch: 2f, recovery: 10f), 1, 0);

            recoil.Tick(0.1f);
            recoil.Tick(0.1f);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void RecoversAtTheWeaponRateAfterTheDelay()
        {
            var recoil = new RecoilState(0.2f);
            recoil.Kick(Spec(pitch: 2f, recovery: 10f), 1, 0);

            // 0.2 秒は待ち、残りの 0.1 秒で 1° 戻る。
            recoil.Tick(0.3f);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ReturnsToRestAndStaysThere()
        {
            var recoil = new RecoilState(0f);
            recoil.Kick(Spec(pitch: 2f, yaw: 3f, recovery: 10f), 1, 0);

            recoil.Tick(10f);

            Assert.That(recoil.IsAtRest, Is.True);
            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(0f));
            Assert.That(recoil.YawDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void RecoversPitchAndYawAlongAStraightLine()
        {
            var recoil = new RecoilState(0f);
            recoil.Kick(Spec(pitch: 4f, yaw: 6f, recovery: 1f), 1, 0);
            var ratio = recoil.YawDegrees / recoil.PitchUpDegrees;

            recoil.Tick(0.5f);

            Assert.That(recoil.IsAtRest, Is.False);
            Assert.That(recoil.YawDegrees / recoil.PitchUpDegrees, Is.EqualTo(ratio).Within(1e-4f));
        }

        [Test]
        public void KickingAgainRestartsTheDelay()
        {
            var recoil = new RecoilState(0.2f);
            var spec = Spec(pitch: 1f, recovery: 10f);
            recoil.Kick(spec, 1, 0);
            recoil.Tick(0.15f);
            recoil.Kick(spec, 1, 1);

            recoil.Tick(0.15f);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void NeverRecoversWithoutARecoveryRate()
        {
            var recoil = new RecoilState(0f);
            recoil.Kick(Spec(pitch: 2f), 1, 0);

            recoil.Tick(60f);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void OffsetIsCappedAtTheMaximum()
        {
            var recoil = new RecoilState();
            var spec = Spec(pitch: 50f);

            recoil.Kick(spec, 1, 0);
            recoil.Kick(spec, 1, 1);
            recoil.Kick(spec, 1, 2);

            Assert.That(recoil.PitchUpDegrees, Is.EqualTo(RecoilState.MaxOffsetDegrees));
        }

        [Test]
        public void AWeaponWithoutRecoilLeavesTheViewAtRest()
        {
            var recoil = new RecoilState();

            recoil.Kick(Spec(), 1, 0);
            recoil.Tick(0.016f);

            Assert.That(recoil.IsAtRest, Is.True);
        }

        [Test]
        public void ResetClearsTheOffset()
        {
            var recoil = new RecoilState();
            recoil.Kick(Spec(pitch: 2f, yaw: 2f), 1, 0);

            recoil.Reset();

            Assert.That(recoil.IsAtRest, Is.True);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecoilState(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecoilState(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecoilState(float.PositiveInfinity));

            var recoil = new RecoilState();
            Assert.Throws<ArgumentNullException>(() => recoil.Kick(null, 1, 0));
            Assert.Throws<ArgumentNullException>(() => RecoilState.YawKick(null, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => recoil.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => recoil.Tick(float.NaN));
        }
    }
}
