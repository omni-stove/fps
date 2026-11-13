using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CrouchInputTests
    {
        private CrouchInput _crouch;
        private List<string> _reasons;
        private bool _canStand;
        private int _headroomChecks;

        [SetUp]
        public void SetUp()
        {
            _canStand = true;
            _headroomChecks = 0;
            _crouch = new CrouchInput(() =>
            {
                _headroomChecks++;
                return _canStand;
            });
            _reasons = new List<string>();
            _crouch.Changed += (input, reason) => _reasons.Add(CrouchInput.FormatLog(input.IsActive, input.Mode, reason));
        }

        [Test]
        public void RejectsAMissingHeadroomCheck()
        {
            Assert.Throws<ArgumentNullException>(() => new CrouchInput(null));
        }

        [Test]
        public void FirstUpdateReportsTheMode()
        {
            _crouch.Update(HoldMode.Toggle, false, false, false, false);

            Assert.That(_reasons, Is.EqualTo(new[] { "[Player] crouch=off mode=toggle reason=mode" }));
        }

        [Test]
        public void ToggleStaysCrouchedAfterOnePressAndStandsOnTheNext()
        {
            Assert.That(_crouch.Update(HoldMode.Toggle, true, true, false, false), Is.True);
            Assert.That(_crouch.Update(HoldMode.Toggle, false, false, false, false), Is.True, "letting go keeps crouching");
            Assert.That(_crouch.Update(HoldMode.Toggle, false, false, false, false), Is.True);
            Assert.That(_crouch.Update(HoldMode.Toggle, true, true, false, false), Is.False);
            Assert.That(_crouch.Update(HoldMode.Toggle, false, false, false, false), Is.False);
            Assert.That(_reasons[_reasons.Count - 1], Is.EqualTo("[Player] crouch=off mode=toggle reason=button"));
        }

        [Test]
        public void HoldCrouchesOnlyWhilePressed()
        {
            Assert.That(_crouch.Update(HoldMode.Hold, true, true, false, false), Is.True);
            Assert.That(_crouch.Update(HoldMode.Hold, true, false, false, false), Is.True);
            Assert.That(_crouch.Update(HoldMode.Hold, false, false, false, false), Is.False);
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void JumpingReleasesTheCrouch(HoldMode mode)
        {
            _crouch.Update(mode, true, true, false, false);

            Assert.That(_crouch.Update(mode, true, false, true, false), Is.False);
            Assert.That(_reasons[_reasons.Count - 1], Does.EndWith("crouch=off mode=" + ModeName(mode) + " reason=jump"));
            Assert.That(_crouch.Update(mode, true, false, false, false), Is.False, "holding the button does not crouch again");
            Assert.That(_crouch.Update(mode, true, true, false, false), Is.True, "a fresh press crouches again");
        }

        [Test]
        public void StartingToSprintReleasesTheCrouch()
        {
            _crouch.Update(HoldMode.Toggle, true, true, false, false);

            Assert.That(_crouch.Update(HoldMode.Toggle, false, false, false, true), Is.False);
            Assert.That(_reasons[_reasons.Count - 1], Is.EqualTo("[Player] crouch=off mode=toggle reason=sprint"));
        }

        [Test]
        public void SprintingThatAlreadyStartedDoesNotReleaseANewCrouch()
        {
            _crouch.Update(HoldMode.Toggle, false, false, false, true);

            Assert.That(_crouch.Update(HoldMode.Toggle, true, true, false, true), Is.True);
            Assert.That(_crouch.Update(HoldMode.Toggle, false, false, false, true), Is.True, "only the start of a sprint releases");
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void BlockedHeadroomKeepsTheCrouchThroughJumpAndSprint(HoldMode mode)
        {
            _crouch.Update(mode, true, true, false, false);
            _canStand = false;

            Assert.That(_crouch.Update(mode, true, false, true, false), Is.True);
            Assert.That(_crouch.Update(mode, true, false, false, true), Is.True);
            Assert.That(_crouch.IsActive, Is.True);
            Assert.That(_headroomChecks, Is.EqualTo(2));
        }

        [Test]
        public void HeadroomIsCheckedOnlyWhenReleasing()
        {
            _crouch.Update(HoldMode.Toggle, false, false, true, false);
            _crouch.Update(HoldMode.Toggle, true, true, false, false);
            _crouch.Update(HoldMode.Toggle, false, false, false, false);

            Assert.That(_headroomChecks, Is.Zero);
        }

        [Test]
        public void PressingCrouchAndJumpTogetherFromStandingCrouches()
        {
            Assert.That(_crouch.Update(HoldMode.Toggle, true, true, true, false), Is.True);
        }

        [Test]
        public void PressingToggleAndJumpTogetherWhileCrouchedStandsOnce()
        {
            _crouch.Update(HoldMode.Toggle, true, true, false, false);

            Assert.That(_crouch.Update(HoldMode.Toggle, true, true, true, false), Is.False);
            Assert.That(_reasons[_reasons.Count - 1], Is.EqualTo("[Player] crouch=off mode=toggle reason=button"));
        }

        [Test]
        public void ReleaseIgnoresTheHeadroomAndNeedsAFreshPress()
        {
            _crouch.Update(HoldMode.Hold, true, true, false, false);
            _canStand = false;

            Assert.That(_crouch.Release(CrouchInput.ReasonDeath), Is.True);
            Assert.That(_crouch.IsActive, Is.False);
            Assert.That(_reasons[_reasons.Count - 1], Is.EqualTo("[Player] crouch=off mode=hold reason=death"));
            Assert.That(_crouch.Update(HoldMode.Hold, true, false, false, false), Is.False);
            Assert.That(_crouch.Release(CrouchInput.ReasonRespawn), Is.False, "already released");
            Assert.That(_headroomChecks, Is.Zero);
        }

        [Test]
        public void ReleaseRejectsAnEmptyReason()
        {
            Assert.Throws<ArgumentException>(() => _crouch.Release(null));
            Assert.Throws<ArgumentException>(() => _crouch.Release(string.Empty));
        }

        [Test]
        public void ChangingTheModeReleasesAndReportsIt()
        {
            _crouch.Update(HoldMode.Toggle, true, true, false, false);

            Assert.That(_crouch.Update(HoldMode.Hold, false, false, false, false), Is.False);
            Assert.That(_crouch.Mode, Is.EqualTo(HoldMode.Hold));
            Assert.That(_reasons[_reasons.Count - 1], Is.EqualTo("[Player] crouch=off mode=hold reason=mode"));
        }

        [Test]
        public void UnknownModeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _crouch.Update((HoldMode)99, false, false, false, false));
        }

        private static string ModeName(HoldMode mode)
        {
            return mode == HoldMode.Toggle ? "toggle" : "hold";
        }
    }
}
