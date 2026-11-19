using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class LockOnWarningTextTests
    {
        [Test]
        public void EachStageHasItsKey()
        {
            Assert.That(LockOnWarningText.Message(new LockOnWarningStatus(LockOnWarningStage.Tracking, 0f)).Key, Is.EqualTo(LockOnWarningText.TrackingKey));
            Assert.That(LockOnWarningText.Message(new LockOnWarningStatus(LockOnWarningStage.Locked, 0f)).Key, Is.EqualTo(LockOnWarningText.LockedKey));
            Assert.That(LockOnWarningText.Message(LockOnWarningStatus.None).IsEmpty, Is.True);
        }

        [Test]
        public void BothLanguagesHaveTheTexts()
        {
            var japanese = Localizer.TableOf(GameLanguage.Japanese);
            var english = Localizer.TableOf(GameLanguage.English);

            Assert.That(japanese.Get(LockOnWarningText.TrackingKey), Is.EqualTo("捕捉されている"));
            Assert.That(japanese.Get(LockOnWarningText.LockedKey), Is.EqualTo("ロックオンされた"));
            Assert.That(english.Get(LockOnWarningText.TrackingKey), Is.EqualTo("TRACKED"));
            Assert.That(english.Get(LockOnWarningText.LockedKey), Is.EqualTo("MISSILE LOCK"));
        }
    }
}
