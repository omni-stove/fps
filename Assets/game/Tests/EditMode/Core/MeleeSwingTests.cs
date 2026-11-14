using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MeleeSwingTests
    {
        private static MeleeSpec Spec() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f);

        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = MeleeSpec.Default;
            Assert.That(spec.Damage, Is.EqualTo(MeleeSpec.DefaultDamage));
            Assert.That(spec.ImpactSeconds, Is.LessThanOrEqualTo(spec.SwingSeconds));
            Assert.That(spec.HalfArcDegrees, Is.EqualTo(MeleeSpec.DefaultArcDegrees / 2f));
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(0, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 0f, 90f, 0.5f, 0.4f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, float.PositiveInfinity, 90f, 0.5f, 0.4f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 0f, 0.5f, 0.4f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 361f, 0.5f, 0.4f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0f, 0.4f, 0f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, -1f, 0.2f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.6f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, float.NaN, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, -0.1f));
        }

        [Test]
        public void LungeSpeedCoversTheDistanceByTheImpact()
        {
            Assert.That(Spec().LungeSpeed, Is.EqualTo(0.75f / 0.2f).Within(1e-5f));
            Assert.That(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0f, 0.75f).LungeSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void TheImpactHappensOnceAtItsMoment()
        {
            var swing = new MeleeSwing(Spec());
            Assert.That(swing.TryStart(), Is.True);
            Assert.That(swing.IsSwinging, Is.True);
            Assert.That(swing.IsLunging, Is.True);

            Assert.That(swing.Tick(0.1f), Is.False);
            Assert.That(swing.Tick(0.1f), Is.True, "0.2s is the impact");
            Assert.That(swing.IsLunging, Is.False);
            Assert.That(swing.IsSwinging, Is.True);
            Assert.That(swing.Tick(0.2f), Is.False);
            Assert.That(swing.IsSwinging, Is.True);
            Assert.That(swing.Tick(0.15f), Is.False);
            Assert.That(swing.IsSwinging, Is.False, "0.5s ends the swing");
        }

        [Test]
        public void ALongTickStillReportsTheImpact()
        {
            var swing = new MeleeSwing(Spec());
            swing.TryStart();
            Assert.That(swing.Tick(2f), Is.True);
            Assert.That(swing.IsSwinging, Is.False);
        }

        [Test]
        public void TheCooldownStartsWhenTheSwingEnds()
        {
            var swing = new MeleeSwing(Spec());
            swing.TryStart();
            Assert.That(swing.TryStart(), Is.False, "already swinging");
            swing.Tick(0.5f);
            Assert.That(swing.CooldownRemaining, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(swing.TryStart(), Is.False);
            swing.Tick(0.3f);
            Assert.That(swing.CanStart, Is.False);
            swing.Tick(0.15f);
            Assert.That(swing.CanStart, Is.True);
            Assert.That(swing.TryStart(), Is.True);
        }

        [Test]
        public void CancellingSkipsTheImpactButKeepsTheCooldown()
        {
            var swing = new MeleeSwing(Spec());
            swing.TryStart();
            Assert.That(swing.Cancel(), Is.True);
            Assert.That(swing.IsSwinging, Is.False);
            Assert.That(swing.Tick(0.3f), Is.False);
            Assert.That(swing.CanStart, Is.False);
            Assert.That(swing.Cancel(), Is.False, "nothing to cancel");
            swing.Reset();
            Assert.That(swing.CanStart, Is.True);
        }

        [Test]
        public void ChangingTheSpecMidSwingKeepsTheSwingsOwn()
        {
            var swing = new MeleeSwing(Spec());
            swing.TryStart();
            swing.Spec = new MeleeSpec(10, 1f, 60f, 2f, 0f, 1f, 0f);
            Assert.That(swing.CurrentSpec.Damage, Is.EqualTo(50));
            Assert.That(swing.Tick(0.2f), Is.True);
            swing.Tick(0.4f);
            Assert.That(swing.IsSwinging, Is.False);
            Assert.That(swing.CurrentSpec.Damage, Is.EqualTo(10));
            Assert.Throws<ArgumentNullException>(() => swing.Spec = null);
            Assert.Throws<ArgumentNullException>(() => new MeleeSwing(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => swing.Tick(-1f));
        }

        [Test]
        public void NoLungeWhenTheDistanceIsZero()
        {
            var swing = new MeleeSwing(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0f));
            swing.TryStart();
            Assert.That(swing.IsLunging, Is.False);
        }

        [Test]
        public void LogsCarryTheTag()
        {
            Assert.That(MeleeLog.Swing("Player"), Is.EqualTo("[Melee] swing id=Player"));
            Assert.That(MeleeLog.Hit("Player", "Dummy", 50, 1.234f), Is.EqualTo("[Melee] hit id=Player target=Dummy dmg=50 distance=1.23 kind=Melee"));
            Assert.That(MeleeLog.Hit("Player", "Dummy", 50, 1.234f), Does.StartWith(MeleeLog.HitPrefix("Player", "Dummy")));
            Assert.That(MeleeLog.Miss("Player"), Is.EqualTo("[Melee] miss id=Player"));
            Assert.That(MeleeLog.Refused("Player", MeleeRefusal.Off), Is.EqualTo("[Melee] refused id=Player reason=Off"));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeleeLog.Refused("Player", MeleeRefusal.None));
        }
    }
}
