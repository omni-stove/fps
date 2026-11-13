using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ControlPointTests
    {
        private const float _captureSeconds = 5f;

        private static void UpdateMany(ControlPoint point, int alpha, int bravo, float seconds, float step = 0.1f)
        {
            var steps = (int)Math.Round(seconds / step);
            for (var i = 0; i < steps; i++)
            {
                point.Update(alpha, bravo, step);
            }
        }

        [Test]
        public void OnePlayerCapturesAfterCaptureSeconds()
        {
            var point = new ControlPoint(_captureSeconds);
            var captured = new List<Team>();
            point.Captured += captured.Add;

            UpdateMany(point, 1, 0, _captureSeconds - 0.1f);
            Assert.That(point.Owner, Is.Null);
            Assert.That(point.LeaningTeam, Is.EqualTo(Team.Alpha));

            point.Update(1, 0, 0.1f);

            Assert.That(point.Owner, Is.EqualTo(Team.Alpha));
            Assert.That(point.Control, Is.EqualTo(1f));
            Assert.That(captured, Is.EqualTo(new[] { Team.Alpha }));
        }

        [Test]
        public void BravoCapturesTowardNegativeEnd()
        {
            var point = new ControlPoint(_captureSeconds);

            point.Update(0, 1, _captureSeconds);

            Assert.That(point.Owner, Is.EqualTo(Team.Bravo));
            Assert.That(point.Control, Is.EqualTo(-1f));
        }

        [Test]
        public void BothTeamsInsideStopsProgress()
        {
            var point = new ControlPoint(_captureSeconds);
            point.Update(1, 0, 2f);
            var before = point.Control;

            point.Update(1, 1, 10f);

            Assert.That(point.IsContested, Is.True);
            Assert.That(point.Control, Is.EqualTo(before));
            Assert.That(point.Owner, Is.Null);

            point.Update(1, 0, 0f);
            Assert.That(point.IsContested, Is.False);
        }

        [Test]
        public void MorePlayersCaptureFasterUpToMaxCapturers()
        {
            var two = new ControlPoint(_captureSeconds, maxCapturers: 3);
            two.Update(2, 0, _captureSeconds / 2f);
            Assert.That(two.Owner, Is.EqualTo(Team.Alpha));

            var capped = new ControlPoint(_captureSeconds, maxCapturers: 2);
            capped.Update(5, 0, _captureSeconds / 4f);
            Assert.That(capped.Control, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void EnemyNeutralizesThenCaptures()
        {
            var point = new ControlPoint(_captureSeconds);
            var events = new List<string>();
            point.Captured += team => events.Add($"captured {team}");
            point.Neutralized += team => events.Add($"neutralized {team}");
            point.Update(1, 0, _captureSeconds);

            UpdateMany(point, 0, 1, _captureSeconds);

            Assert.That(point.Owner, Is.Null);
            Assert.That(point.Control, Is.EqualTo(0f));

            UpdateMany(point, 0, 1, _captureSeconds);

            Assert.That(point.Owner, Is.EqualTo(Team.Bravo));
            Assert.That(events, Is.EqualTo(new[] { "captured Alpha", "neutralized Alpha", "captured Bravo" }));
        }

        [Test]
        public void LongUpdateNeutralizesAndCapturesInOrder()
        {
            var point = new ControlPoint(_captureSeconds);
            point.Update(0, 1, _captureSeconds);
            var events = new List<string>();
            point.Captured += team => events.Add($"captured {team}");
            point.Neutralized += team => events.Add($"neutralized {team}");

            point.Update(1, 0, _captureSeconds * 10f);

            Assert.That(point.Owner, Is.EqualTo(Team.Alpha));
            Assert.That(events, Is.EqualTo(new[] { "neutralized Bravo", "captured Alpha" }));
        }

        [Test]
        public void EmptyPartialProgressReturnsToNeutral()
        {
            var point = new ControlPoint(_captureSeconds);
            point.Update(1, 0, 3f);

            point.Update(0, 0, 1f);
            Assert.That(point.Control, Is.EqualTo(0.4f).Within(1e-6f));

            point.Update(0, 0, 10f);
            Assert.That(point.Control, Is.EqualTo(0f));
            Assert.That(point.LeaningTeam, Is.Null);
            Assert.That(point.Owner, Is.Null);
        }

        [Test]
        public void EmptyOwnedPointRecoversToOwner()
        {
            var point = new ControlPoint(_captureSeconds);
            point.Update(1, 0, _captureSeconds);
            point.Update(0, 1, 2f);
            Assert.That(point.Owner, Is.EqualTo(Team.Alpha));

            point.Update(0, 0, 10f);

            Assert.That(point.Owner, Is.EqualTo(Team.Alpha));
            Assert.That(point.Control, Is.EqualTo(1f));
        }

        [Test]
        public void ResetReturnsToNeutralWithoutEvents()
        {
            var point = new ControlPoint(_captureSeconds);
            point.Update(1, 0, _captureSeconds);
            var events = 0;
            point.Captured += _ => events++;
            point.Neutralized += _ => events++;

            point.Reset();

            Assert.That(point.Owner, Is.Null);
            Assert.That(point.Control, Is.EqualTo(0f));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ControlPoint(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ControlPoint(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ControlPoint(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ControlPoint(_captureSeconds, 0));

            var point = new ControlPoint(_captureSeconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => point.Update(-1, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => point.Update(0, -1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => point.Update(1, 0, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => point.Update(1, 0, float.NaN));
        }
    }
}
