using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class KillFeedEntryTests
    {
        static readonly int[] Three4 = { 3, 4 };
        static readonly int[] Four3 = { 4, 3 };

        [Test]
        public void AssistersDefaultToEmpty()
        {
            Assert.That(new KillFeedEntry(1, 2, "rifle", false, false).AssisterIds, Is.Empty);
            Assert.That(new KillFeedEntry(1, 2, "rifle", false, false, null).AssisterIds, Is.Empty);
            Assert.That(default(KillFeedEntry).AssisterIds, Is.Not.Null.And.Empty);
        }

        [Test]
        public void AssistersAreCopiedOnConstruction()
        {
            var ids = new[] { 3, 4 };
            var entry = new KillFeedEntry(1, 2, "rifle", false, false, ids);
            ids[0] = 9;

            Assert.That(entry.AssisterIds, Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void AssistersTakePartInEquality()
        {
            var a = new KillFeedEntry(1, 2, "rifle", true, false, Three4);
            var same = new KillFeedEntry(1, 2, "rifle", true, false, Three4);
            var reordered = new KillFeedEntry(1, 2, "rifle", true, false, Four3);
            var none = new KillFeedEntry(1, 2, "rifle", true, false);

            Assert.That(a == same, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(a == reordered, Is.False);
            Assert.That(a == none, Is.False);
            Assert.That(none, Is.EqualTo(new KillFeedEntry(1, 2, "rifle", true, false, System.Array.Empty<int>())));
        }

        [Test]
        public void ToStringAddsAssistersOnlyWhenThereAreAny()
        {
            Assert.That(
                new KillFeedEntry(1, 2, "rifle", true, false).ToString(),
                Is.EqualTo("killer=1 victim=2 weapon=rifle headshot=True friendly=False"));
            Assert.That(
                new KillFeedEntry(1, 2, "rifle", true, false, Three4).ToString(),
                Is.EqualTo("killer=1 victim=2 weapon=rifle headshot=True friendly=False assisters=3,4"));
        }
    }
}
