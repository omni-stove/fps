using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class PauseStateTests
    {
        private const string _pause = "Player/Pause";
        private const string _move = "Player/Move";
        private const string _attack = "Player/Attack";
        private const string _look = "Player/Look";

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Names(params string[] names) => names;

        private static PauseState NewState(PauseTimeMode mode)
        {
            return new PauseState(mode, new[] { _pause });
        }

        [TestCase(PauseTimeMode.Freeze)]
        [TestCase(PauseTimeMode.KeepRunning)]
        public void StartsClosedWithNothingSuspended(PauseTimeMode mode)
        {
            var state = NewState(mode);

            Assert.That(state.IsOpen, Is.False);
            Assert.That(state.TimeMode, Is.EqualTo(mode));
            Assert.That(state.SuspendedActions, Is.Empty);
        }

        [Test]
        public void FreezeStopsTimeOnOpenAndRestoresTheEarlierScaleOnClose()
        {
            var state = NewState(PauseTimeMode.Freeze);

            Assert.That(state.Open(0.5f, out var frozen), Is.True);
            Assert.That(state.IsOpen, Is.True);
            Assert.That(frozen, Is.EqualTo(PauseState.FrozenTimeScale));

            Assert.That(state.Close(out var restored, out _), Is.True);
            Assert.That(state.IsOpen, Is.False);
            Assert.That(restored, Is.EqualTo(0.5f));
        }

        [Test]
        public void KeepRunningNeverTouchesTime()
        {
            var state = NewState(PauseTimeMode.KeepRunning);

            Assert.That(state.Open(1f, out var opened), Is.True);
            Assert.That(opened, Is.Null);
            Assert.That(state.Close(out var closed, out _), Is.True);
            Assert.That(closed, Is.Null);
        }

        [Test]
        public void OpeningTwiceKeepsTheFirstTimeScale()
        {
            var state = NewState(PauseTimeMode.Freeze);
            state.Open(1f, out _);

            Assert.That(state.Open(PauseState.FrozenTimeScale, out var again), Is.False);
            Assert.That(again, Is.Null);

            state.Close(out var restored, out _);
            Assert.That(restored, Is.EqualTo(1f), "the second open must not overwrite the scale to restore with the frozen one");
        }

        [Test]
        public void ClosingWhileClosedDoesNothing()
        {
            var state = NewState(PauseTimeMode.Freeze);

            Assert.That(state.Close(out var timeScale, out var actions), Is.False);
            Assert.That(timeScale, Is.Null);
            Assert.That(actions, Is.Empty);
        }

        [Test]
        public void SuspendsEveryEnabledActionExceptThePauseAction()
        {
            var state = NewState(PauseTimeMode.KeepRunning);
            state.Open(1f, out _);

            var suspended = state.Suspend(new[] { _move, _pause, _attack });

            Assert.That(suspended, Is.EqualTo(new[] { _move, _attack }));
            Assert.That(state.SuspendedActions, Is.EqualTo(new[] { _move, _attack }));
            Assert.That(state.KeepsEnabled(_pause), Is.True);
            Assert.That(state.KeepsEnabled(_move), Is.False);
        }

        [Test]
        public void SuspendingAgainReturnsOnlyActionsEnabledSinceTheLastCall()
        {
            var state = NewState(PauseTimeMode.Freeze);
            state.Open(1f, out _);
            state.Suspend(new[] { _move });

            Assert.That(state.Suspend(new[] { _move, _look, _look }), Is.EqualTo(new[] { _look }), "a respawned part re-enabled Look while paused");
            Assert.That(state.Suspend(new[] { _pause }), Is.Empty);
            Assert.That(state.SuspendedActions, Is.EqualTo(new[] { _move, _look }));
        }

        [Test]
        public void CloseReturnsOnlyTheActionsItSuspendedAndForgetsThem()
        {
            var state = NewState(PauseTimeMode.Freeze);
            state.Open(1f, out _);
            state.Suspend(new[] { _move, _attack });

            state.Close(out _, out var toEnable);

            Assert.That(toEnable, Is.EqualTo(new[] { _move, _attack }));
            Assert.That(state.SuspendedActions, Is.Empty);

            state.Open(1f, out _);
            state.Close(out _, out var nothing);
            Assert.That(nothing, Is.Empty, "actions from the previous pause are not enabled again");
        }

        [Test]
        public void SuspendingWhileClosedThrows()
        {
            var state = NewState(PauseTimeMode.Freeze);

            Assert.Throws<InvalidOperationException>(() => state.Suspend(new[] { _move }));
        }

        [Test]
        public void ActionNamesAreCaseSensitive()
        {
            var state = NewState(PauseTimeMode.KeepRunning);
            state.Open(1f, out _);

            Assert.That(state.Suspend(Names("player/pause")), Is.EqualTo(Names("player/pause")));
        }

        [Test]
        public void TimeModeCanChangeOnlyWhileClosed()
        {
            var state = NewState(PauseTimeMode.KeepRunning);

            state.TimeMode = PauseTimeMode.Freeze;
            Assert.That(state.FreezesTime, Is.True);

            state.Open(1f, out _);
            state.TimeMode = PauseTimeMode.Freeze;
            Assert.Throws<InvalidOperationException>(() => state.TimeMode = PauseTimeMode.KeepRunning);
            Assert.That(state.TimeMode, Is.EqualTo(PauseTimeMode.Freeze));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsAnInvalidTimeScale(float timeScale)
        {
            var state = NewState(PauseTimeMode.Freeze);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Open(timeScale, out _));
            Assert.That(state.IsOpen, Is.False);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PauseState(PauseTimeMode.Freeze, null));
            Assert.Throws<ArgumentException>(() => new PauseState(PauseTimeMode.Freeze, Names("")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PauseState((PauseTimeMode)99, new[] { _pause }));

            var state = NewState(PauseTimeMode.Freeze);
            state.Open(1f, out _);
            Assert.Throws<ArgumentNullException>(() => state.Suspend(null));
            Assert.Throws<ArgumentException>(() => state.Suspend(new[] { _move, null }));
            Assert.That(state.SuspendedActions, Is.Empty, "a rejected call suspends nothing");
            Assert.Throws<ArgumentOutOfRangeException>(() => state.TimeMode = (PauseTimeMode)99);
        }
    }
}
