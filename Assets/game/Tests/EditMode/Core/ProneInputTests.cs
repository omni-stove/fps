using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ProneInputTests
    {
        private ProneInput _prone;
        private List<string> _logs;

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Logs(params string[] expected) => expected;

        [SetUp]
        public void SetUp()
        {
            _prone = new ProneInput();
            _logs = new List<string>();
            _prone.Changed += (input, reason) => _logs.Add(ProneInput.FormatLog(input.IsActive, reason));
        }

        [Test]
        public void StartsStandingAndReportsNothing()
        {
            Assert.That(_prone.IsActive, Is.False);
            Assert.That(Update(), Is.False);
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void EachPressToggles()
        {
            Assert.That(Update(prone: true), Is.True);
            Assert.That(Update(), Is.True, "letting go keeps lying down");
            Assert.That(Update(prone: true), Is.False);

            Assert.That(_logs, Is.EqualTo(Logs("[Player] prone=on reason=button", "[Player] prone=off reason=button")));
        }

        [Test]
        public void CannotLieDownWhenNotAllowed()
        {
            Assert.That(_prone.Update(false, true, false, false, false, out var dive), Is.False);
            Assert.That(dive, Is.False);
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void GettingUpDoesNotNeedPermission()
        {
            Update(prone: true);

            Assert.That(_prone.Update(false, true, false, false, false, out _), Is.False);
        }

        [Test]
        public void PressingWhileSprintingDives()
        {
            Assert.That(_prone.Update(true, true, false, false, true, out var dive), Is.True);

            Assert.That(dive, Is.True);
            Assert.That(_logs, Is.EqualTo(Logs("[Player] prone=on reason=dive")));
        }

        [Test]
        public void PressingWhileStillDoesNotDive()
        {
            _prone.Update(true, true, false, false, false, out var dive);

            Assert.That(dive, Is.False);
        }

        [TestCase(true, false, false, "crouch")]
        [TestCase(false, true, false, "jump")]
        [TestCase(false, false, true, "sprint")]
        public void CrouchJumpOrASprintStartGetsUp(bool crouch, bool jump, bool sprint, string reason)
        {
            Update(prone: true);

            Assert.That(_prone.Update(true, false, crouch, jump, sprint, out _), Is.False);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo("[Player] prone=off reason=" + reason));
        }

        [Test]
        public void ASprintKeptFromBeforeLyingDownDoesNotGetUp()
        {
            _prone.Update(true, true, false, false, true, out _);

            Assert.That(_prone.Update(true, false, false, false, true, out _), Is.True);
        }

        [Test]
        public void ReleaseGetsUpOnceAndNeedsAReason()
        {
            Update(prone: true);

            Assert.That(_prone.Release(ProneInput.ReasonRule), Is.True);
            Assert.That(_prone.Release(ProneInput.ReasonRule), Is.False);
            Assert.That(_prone.IsActive, Is.False);
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo("[Player] prone=off reason=rule"));
            Assert.Throws<ArgumentException>(() => _prone.Release(""));
            Assert.Throws<ArgumentException>(() => _prone.Release(null));
        }

        private bool Update(bool prone = false)
        {
            return _prone.Update(true, prone, false, false, false, out _);
        }
    }
}
