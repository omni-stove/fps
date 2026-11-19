using NUnit.Framework;
using Shooter.Editor;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayModeReloadLockTests
    {
        private int _locks;
        private int _unlocks;
        private PlayModeReloadLock _lock;

        [SetUp]
        public void SetUp()
        {
            _locks = 0;
            _unlocks = 0;
            _lock = new PlayModeReloadLock(() => _locks++, () => _unlocks++);
        }

        [Test]
        public void ReloadingIsHeldForTheWholePlay()
        {
            _lock.Handle(PlayModeStateChange.ExitingEditMode);
            Assert.That(_lock.IsHeld, Is.False, "not before Play starts");

            _lock.Handle(PlayModeStateChange.EnteredPlayMode);
            Assert.That(_lock.IsHeld, Is.True);
            Assert.That(_locks, Is.EqualTo(1));
            Assert.That(_unlocks, Is.EqualTo(0));

            _lock.Handle(PlayModeStateChange.ExitingPlayMode);
            Assert.That(_lock.IsHeld, Is.False);
            Assert.That(_unlocks, Is.EqualTo(1));
        }

        [Test]
        public void LocksAndUnlocksStayBalanced()
        {
            _lock.Hold();
            _lock.Handle(PlayModeStateChange.EnteredPlayMode);
            Assert.That(_locks, Is.EqualTo(1), "holding twice locks once");

            _lock.Handle(PlayModeStateChange.ExitingPlayMode);
            _lock.Handle(PlayModeStateChange.EnteredEditMode);
            Assert.That(_unlocks, Is.EqualTo(1), "releasing twice unlocks once");

            _lock.Release();
            Assert.That(_unlocks, Is.EqualTo(1), "nothing to release before holding");
        }

        [Test]
        public void ACompileDuringPlayIsReportedOncePerPlay()
        {
            Assert.That(_lock.NoteCompiled(), Is.False, "outside Play scripts reload as usual");

            _lock.Handle(PlayModeStateChange.EnteredPlayMode);
            Assert.That(_lock.NoteCompiled(), Is.True);
            Assert.That(_lock.HasDeferred, Is.True);
            Assert.That(_lock.NoteCompiled(), Is.False);

            _lock.Handle(PlayModeStateChange.ExitingPlayMode);
            _lock.Handle(PlayModeStateChange.EnteredPlayMode);
            Assert.That(_lock.HasDeferred, Is.False, "a new Play starts clean");
            Assert.That(_lock.NoteCompiled(), Is.True);
        }

        [Test]
        public void TheLogLineCarriesTheTag()
        {
            Assert.That(PlayModeReloadLog.Deferred(), Does.StartWith(PlayModeReloadLog.LogTag));
            Assert.That(PlayModeReloadLog.Deferred(), Is.EqualTo("[PlayModeReload] scripts compiled during Play; they are reloaded when Play stops"));
        }
    }
}
