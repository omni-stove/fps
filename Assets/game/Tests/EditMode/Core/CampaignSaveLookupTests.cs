using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignSaveLookupTests
    {
        [Test]
        public void ReadsTheMissionSlot()
        {
            var storage = new Storage();
            storage.Slots[CampaignSaveSlots.For("Campaign02")] = Save("Campaign02", "area-a").ToJson();

            Assert.That(CampaignSaveLookup.TryReadResumable(storage, "Campaign02", out var save, out var problem), Is.True);

            Assert.That(save.CheckpointId, Is.EqualTo("area-a"));
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void NoSaveIsNotAProblem()
        {
            Assert.That(CampaignSaveLookup.TryReadResumable(new Storage(), "Campaign02", out var save, out var problem), Is.False);

            Assert.That(save, Is.Null);
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void OnlyTheLegacyMissionReadsTheLegacySlot()
        {
            var storage = new Storage();
            storage.Slots[CampaignSaveSlots.Legacy] = Save(CampaignSaveSlots.LegacyMissionId, "start").ToJson();

            Assert.That(CampaignSaveLookup.TryReadResumable(storage, CampaignSaveSlots.LegacyMissionId, out _, out _), Is.True);
            Assert.That(CampaignSaveLookup.TryReadResumable(storage, "Campaign02", out _, out _), Is.False);
        }

        [Test]
        public void BrokenOrForeignSavesCannotBeResumed()
        {
            var storage = new Storage();
            storage.Slots[CampaignSaveSlots.For("Campaign01")] = "{ not json";
            storage.Slots[CampaignSaveSlots.For("Campaign02")] = Save("Campaign01", "start").ToJson();

            Assert.That(CampaignSaveLookup.TryReadResumable(storage, "Campaign01", out var broken, out var brokenProblem), Is.False);
            Assert.That(broken, Is.Null);
            Assert.That(brokenProblem, Does.Contain("broken"));

            Assert.That(CampaignSaveLookup.TryReadResumable(storage, "Campaign02", out _, out var foreignProblem), Is.False);
            Assert.That(foreignProblem, Does.Contain("Campaign01"));
        }

        [Test]
        public void DeleteRemovesTheMissionSlotAndTheLegacySlotOfTheLegacyMission()
        {
            var storage = new Storage();
            storage.Slots[CampaignSaveSlots.For("Campaign01")] = "a";
            storage.Slots[CampaignSaveSlots.For("Campaign02")] = "b";
            storage.Slots[CampaignSaveSlots.Legacy] = "c";

            CampaignSaveLookup.Delete(storage, "Campaign02");
            Assert.That(storage.Slots.Keys, Is.EquivalentTo(new[] { CampaignSaveSlots.For("Campaign01"), CampaignSaveSlots.Legacy }));

            CampaignSaveLookup.Delete(storage, "Campaign01");
            Assert.That(storage.Slots, Is.Empty);

            Assert.DoesNotThrow(() => CampaignSaveLookup.Delete(storage, "Campaign01"), "deleting nothing is fine");
        }

        [Test]
        public void DeleteFailuresAreThrown()
        {
            var storage = new Storage { FailDeletes = true };

            Assert.Throws<IOException>(() => CampaignSaveLookup.Delete(storage, "Campaign01"));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSaveLookup.TryReadResumable(null, "Campaign01", out _, out _));
            Assert.Throws<ArgumentException>(() => CampaignSaveLookup.TryReadResumable(new Storage(), "../x", out _, out _));
            Assert.Throws<ArgumentNullException>(() => CampaignSaveLookup.Delete(null, "Campaign01"));
            Assert.Throws<ArgumentException>(() => CampaignSaveLookup.Delete(new Storage(), ""));
        }

        private static CampaignSave Save(string missionId, string checkpointId)
        {
            return new CampaignSave(
                missionId,
                checkpointId,
                100,
                new[] { new WeaponAmmo("rifle", 30, 90) },
                "rifle",
                new[] { new ObjectiveProgress("reach", ObjectiveState.Active, 0) });
        }

        private sealed class Storage : ISaveStorage
        {
            public readonly Dictionary<string, string> Slots = new Dictionary<string, string>(StringComparer.Ordinal);

            public bool FailDeletes { get; set; }

            public bool TryRead(string slot, out string text)
            {
                return Slots.TryGetValue(slot, out text);
            }

            public void Write(string slot, string text)
            {
                Slots[slot] = text;
            }

            public void Delete(string slot)
            {
                if (FailDeletes)
                {
                    throw new IOException("disk is read-only");
                }

                Slots.Remove(slot);
            }
        }
    }
}
