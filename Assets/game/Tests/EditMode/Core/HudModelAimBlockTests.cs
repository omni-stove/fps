using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudModelAimBlockTests
    {
        private static List<HudField> Record(HudModel model)
        {
            var changes = new List<HudField>();
            model.Changed += changes.Add;
            return changes;
        }

        [Test]
        public void StartsUnblocked()
        {
            var model = new HudModel();

            Assert.That(model.IsAimBlocked, Is.False);
            Assert.That(model.AimBlockPoint.HasValue, Is.False);
        }

        [Test]
        public void SetAimBlockKeepsThePointAndNotifiesOnlyWhenItChanges()
        {
            var model = new HudModel();
            var changes = Record(model);

            model.SetAimBlock(new Vector2(0.4f, 0.6f));
            model.SetAimBlock(new Vector2(0.4f, 0.6f));
            model.SetAimBlock(new Vector2(0.45f, 0.6f));

            Assert.That(model.IsAimBlocked, Is.True);
            Assert.That(model.AimBlockPoint.Value, Is.EqualTo(new Vector2(0.45f, 0.6f)));
            Assert.That(changes, Is.EqualTo(new[] { HudField.AimBlock, HudField.AimBlock }));
        }

        [Test]
        public void ABlockOffScreenHasNoPoint()
        {
            var model = new HudModel();

            model.SetAimBlock(null);

            Assert.That(model.IsAimBlocked, Is.True);
            Assert.That(model.AimBlockPoint.HasValue, Is.False);
        }

        [Test]
        public void ClearAimBlockUnblocksOnce()
        {
            var model = new HudModel();
            model.SetAimBlock(new Vector2(0.5f, 0.5f));
            var changes = Record(model);

            model.ClearAimBlock();
            model.ClearAimBlock();

            Assert.That(model.IsAimBlocked, Is.False);
            Assert.That(model.AimBlockPoint.HasValue, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { HudField.AimBlock }));
        }

        [Test]
        public void RejectsANonFinitePoint()
        {
            var model = new HudModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetAimBlock(new Vector2(float.NaN, 0.5f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetAimBlock(new Vector2(0.5f, float.PositiveInfinity)));
            Assert.That(model.IsAimBlocked, Is.False);
        }
    }
}
