using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CloudSaveMergeTests
    {
        private static CloudSaveStamp Saved(long at) => new CloudSaveStamp(at, false);

        private static CloudSaveStamp Removed(long at) => new CloudSaveStamp(at, true);

        [Test]
        public void ALocalSlotWithItsTimeKeepsTheTime()
        {
            Assert.That(CloudSaveMerge.Local(Saved(50), true), Is.EqualTo(Saved(50)));
        }

        [Test]
        public void ALocalSlotWithoutATimeCountsAsTheOldest()
        {
            Assert.That(CloudSaveMerge.Local(null, true), Is.EqualTo(Saved(CloudSaveMerge.UnstampedSavedAt)));
            Assert.That(CloudSaveMerge.Local(Removed(50), true), Is.EqualTo(Saved(0)), "written after it was deleted, outside the sync");
        }

        [Test]
        public void ADeletedLocalSlotKeepsItsDeletion()
        {
            Assert.That(CloudSaveMerge.Local(Removed(50), false), Is.EqualTo(Removed(50)));
        }

        [Test]
        public void AMissingLocalSlotIsNothing()
        {
            Assert.That(CloudSaveMerge.Local(null, false), Is.Null);
            Assert.That(CloudSaveMerge.Local(Saved(50), false), Is.Null, "the file was removed outside the sync");
        }

        [Test]
        public void TheNewerCloudCopyIsDownloaded()
        {
            Assert.That(CloudSaveMerge.Decide(Saved(10), CloudSaveEnvelope.Saved("profile", 20, "x")), Is.EqualTo(CloudSaveAction.Download));
            Assert.That(CloudSaveMerge.Decide(Saved(10), CloudSaveEnvelope.Removed("profile", 20)), Is.EqualTo(CloudSaveAction.Download));
            Assert.That(CloudSaveMerge.Decide(Removed(10), CloudSaveEnvelope.Saved("profile", 20, "x")), Is.EqualTo(CloudSaveAction.Download));
        }

        [Test]
        public void TheNewerLocalCopyIsUploaded()
        {
            Assert.That(CloudSaveMerge.Decide(Saved(30), CloudSaveEnvelope.Saved("profile", 20, "x")), Is.EqualTo(CloudSaveAction.Upload));
            Assert.That(CloudSaveMerge.Decide(Removed(30), CloudSaveEnvelope.Saved("profile", 20, "x")), Is.EqualTo(CloudSaveAction.Upload));
        }

        [Test]
        public void TheSameTimeIsLeftAlone()
        {
            Assert.That(CloudSaveMerge.Decide(Saved(20), CloudSaveEnvelope.Saved("profile", 20, "x")), Is.EqualTo(CloudSaveAction.None));
        }

        [Test]
        public void AnUnstampedLocalSlotLosesToAnyCloudCopy()
        {
            var local = CloudSaveMerge.Local(null, true);

            Assert.That(CloudSaveMerge.Decide(local, CloudSaveEnvelope.Saved("profile", 1, "x")), Is.EqualTo(CloudSaveAction.Download));
            Assert.That(CloudSaveMerge.Decide(local, null), Is.EqualTo(CloudSaveAction.Upload));
        }

        [Test]
        public void OnlyOneSideHavingTheSlotCopiesItAcross()
        {
            Assert.That(CloudSaveMerge.Decide(null, CloudSaveEnvelope.Saved("profile", 5, "x")), Is.EqualTo(CloudSaveAction.Download));
            Assert.That(CloudSaveMerge.Decide(Saved(5), null), Is.EqualTo(CloudSaveAction.Upload));
        }

        [Test]
        public void DeletionsWithNothingOnTheOtherSideAreLeftAlone()
        {
            Assert.That(CloudSaveMerge.Decide(null, null), Is.EqualTo(CloudSaveAction.None));
            Assert.That(CloudSaveMerge.Decide(null, CloudSaveEnvelope.Removed("profile", 5)), Is.EqualTo(CloudSaveAction.None));
            Assert.That(CloudSaveMerge.Decide(Removed(5), null), Is.EqualTo(CloudSaveAction.None));
        }
    }
}
