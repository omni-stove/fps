using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SprintInputTests
    {
        private static readonly Vector2 _forward = new Vector2(0f, 1f);

        private SprintInput _sprint;
        private List<SprintChange> _changes;

        [SetUp]
        public void SetUp()
        {
            _sprint = new SprintInput();
            _changes = new List<SprintChange>();
            _sprint.Changed += _changes.Add;
        }

        [Test]
        public void FirstUpdateReportsTheMode()
        {
            _sprint.Update(HoldMode.Toggle, Vector2.Zero, false, false);

            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].IsActive, Is.False);
            Assert.That(_changes[0].Mode, Is.EqualTo(HoldMode.Toggle));
            Assert.That(_changes[0].Reason, Is.EqualTo(SprintInput.ReasonMode));
            Assert.That(SprintInput.FormatLog(_changes[0]), Is.EqualTo("[Player] sprint=off mode=toggle reason=mode"));
        }

        [Test]
        public void ToggleKeepsSprintingAfterOnePressAndStopsOnTheNext()
        {
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, true, true), Is.True);
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, false, false), Is.True, "letting go keeps sprinting");
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, false, false), Is.True);
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, true, true), Is.False);
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, false, false), Is.False);
            Assert.That(_changes[_changes.Count - 1].Reason, Is.EqualTo(SprintInput.ReasonButton));
        }

        [Test]
        public void ToggleReleasesWhenMoveInputStops()
        {
            _sprint.Update(HoldMode.Toggle, _forward, true, true);

            Assert.That(_sprint.Update(HoldMode.Toggle, Vector2.Zero, false, false), Is.False);
            Assert.That(SprintInput.FormatLog(_changes[_changes.Count - 1]), Is.EqualTo("[Player] sprint=off mode=toggle reason=stopped"));
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, false, false), Is.False, "moving again does not resume");
        }

        [Test]
        public void ToggleTreatsTinyMoveInputAsStopped()
        {
            _sprint.Update(HoldMode.Toggle, _forward, true, true);

            Assert.That(_sprint.Update(HoldMode.Toggle, new Vector2(0f, SprintInput.MoveThreshold * 0.5f), false, false), Is.False);
        }

        [Test]
        public void TogglePressedWhileStandingStartsWhenMoving()
        {
            Assert.That(_sprint.Update(HoldMode.Toggle, Vector2.Zero, true, true), Is.True);
            Assert.That(_sprint.Update(HoldMode.Toggle, Vector2.Zero, false, false), Is.True, "standing still is not stopping");
            Assert.That(_sprint.Update(HoldMode.Toggle, _forward, false, false), Is.True);
            Assert.That(_sprint.Update(HoldMode.Toggle, Vector2.Zero, false, false), Is.False);
        }

        [Test]
        public void TogglePressedInTheFrameItStopsStaysOn()
        {
            _sprint.Update(HoldMode.Toggle, _forward, false, false);

            Assert.That(_sprint.Update(HoldMode.Toggle, Vector2.Zero, true, true), Is.True);
        }

        [Test]
        public void HoldSprintsOnlyWhilePressedAsBefore()
        {
            Assert.That(_sprint.Update(HoldMode.Hold, _forward, false, false), Is.False);
            Assert.That(_sprint.Update(HoldMode.Hold, _forward, true, true), Is.True);
            Assert.That(_sprint.Update(HoldMode.Hold, _forward, true, false), Is.True);
            Assert.That(_sprint.Update(HoldMode.Hold, _forward, false, false), Is.False);
        }

        [Test]
        public void HoldResumesAfterStoppingWhileStillPressed()
        {
            _sprint.Update(HoldMode.Hold, _forward, true, true);

            Assert.That(_sprint.Update(HoldMode.Hold, Vector2.Zero, true, false), Is.True);
            Assert.That(_sprint.Update(HoldMode.Hold, _forward, true, false), Is.True);
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void ReleaseStopsUntilPressedAgain(HoldMode mode)
        {
            _sprint.Update(mode, _forward, true, true);

            Assert.That(_sprint.Release(SprintInput.ReasonFire), Is.True);
            Assert.That(_sprint.IsActive, Is.False);
            Assert.That(_changes[_changes.Count - 1].Reason, Is.EqualTo(SprintInput.ReasonFire));
            Assert.That(_sprint.Update(mode, _forward, true, false), Is.False);
            Assert.That(_sprint.Update(mode, _forward, true, true), Is.True);
        }

        [Test]
        public void ReleaseWhenNotSprintingDoesNothing()
        {
            _sprint.Update(HoldMode.Toggle, _forward, false, false);
            var count = _changes.Count;

            Assert.That(_sprint.Release(SprintInput.ReasonDeath), Is.False);
            Assert.That(_changes.Count, Is.EqualTo(count));
        }

        [Test]
        public void ReleaseRejectsAnEmptyReason()
        {
            Assert.Throws<ArgumentException>(() => _sprint.Release(null));
            Assert.Throws<ArgumentException>(() => _sprint.Release(string.Empty));
        }

        [Test]
        public void ChangingTheModeReleasesAndReportsIt()
        {
            _sprint.Update(HoldMode.Toggle, _forward, true, true);

            Assert.That(_sprint.Update(HoldMode.Hold, _forward, false, false), Is.False);
            Assert.That(_sprint.Mode, Is.EqualTo(HoldMode.Hold));
            Assert.That(SprintInput.FormatLog(_changes[_changes.Count - 1]), Is.EqualTo("[Player] sprint=off mode=hold reason=mode"));
        }

        [Test]
        public void SameModeIsReportedOnlyOnce()
        {
            _sprint.Update(HoldMode.Hold, _forward, false, false);
            _sprint.Update(HoldMode.Hold, _forward, false, false);

            Assert.That(_changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownModeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _sprint.Update((HoldMode)99, _forward, false, false));
        }
    }
}
