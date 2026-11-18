using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LootLogTests
    {
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        private const string _bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";

        [Test]
        public void OpenedListsTheItemsAndJoinsSpacedNames()
        {
            var line = LootLog.Opened("Crate 1", "crate", new[] { new ItemCount(_ammo, 3), new ItemCount(_bandage, 1) });

            Assert.That(line, Is.EqualTo("[Loot] opened container=Crate_1 table=crate items=" + _ammo + "x3," + _bandage + "x1"));
        }

        [Test]
        public void AnEmptyOpeningShowsADash()
        {
            Assert.That(LootLog.Opened("Crate", " ", Array.Empty<ItemCount>()), Is.EqualTo("[Loot] opened container=Crate table=- items=-"));
            Assert.Throws<ArgumentNullException>(() => LootLog.Opened("Crate", "crate", null));
        }

        [Test]
        public void UnknownTableNamesTheContainerAndTable()
        {
            Assert.That(
                LootLog.UnknownTable("Crate", "missing"),
                Is.EqualTo("[Loot] container=Crate names the table missing, which is not in the loot tables; it opens empty"));
        }
    }
}
