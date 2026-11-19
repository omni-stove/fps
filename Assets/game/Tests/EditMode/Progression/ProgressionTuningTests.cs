using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class ProgressionTuningTests
    {
        [Test]
        public void TheKitShipsTheDefaultsInResources()
        {
            var asset = Resources.Load<TextAsset>("Tuning/progression");
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/progression.json");

            var spec = ProgressionJson.Parse(asset.text, "Resources/Tuning/progression.json");

            Assert.That(spec.KillXp, Is.EqualTo(ProgressionSpec.DefaultKillXp));
            Assert.That(spec.HeadshotXp, Is.EqualTo(ProgressionSpec.DefaultHeadshotXp));
            Assert.That(spec.AssistXp, Is.EqualTo(ProgressionSpec.DefaultAssistXp));
            Assert.That(spec.MatchXp, Is.EqualTo(ProgressionSpec.DefaultMatchXp));
            Assert.That(spec.WinXp, Is.EqualTo(ProgressionSpec.DefaultWinXp));
            Assert.That(spec.Levels, Is.EqualTo(ProgressionSpec.Default.Levels));
            Assert.That(spec.Unlocks.Count, Is.EqualTo(6));
        }

        [Test]
        public void TheAchievementFileHoldsTheDefaults()
        {
            var asset = Resources.Load<TextAsset>(AchievementJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/" + AchievementJson.ResourcePath + ".json");

            var spec = AchievementJson.Parse(asset.text, AchievementJson.ResourcePath);
            var defaults = AchievementSpec.DefaultAchievements;

            Assert.That(spec.Achievements.Count, Is.EqualTo(defaults.Count));
            for (var i = 0; i < defaults.Count; i++)
            {
                Assert.That(spec.Achievements[i].Id, Is.EqualTo(defaults[i].Id));
                Assert.That(spec.Achievements[i].DisplayNameKey, Is.EqualTo(defaults[i].DisplayNameKey));
                Assert.That(spec.Achievements[i].DescriptionKey, Is.EqualTo(defaults[i].DescriptionKey));
                Assert.That(spec.Achievements[i].Stat, Is.EqualTo(defaults[i].Stat));
                Assert.That(spec.Achievements[i].Target, Is.EqualTo(defaults[i].Target));
                Assert.That(spec.Achievements[i].Hidden, Is.EqualTo(defaults[i].Hidden));
            }

            Assert.That(spec.MeleeWeaponIds, Is.EqualTo(AchievementSpec.DefaultMeleeWeaponIds));
        }

        [Test]
        public void TheKitTuningPathsAreTheOnesTheProgressionReads()
        {
            Assert.That(KitTuning.AchievementsResourcePath, Is.EqualTo(AchievementJson.ResourcePath));
            Assert.That(KitTuning.AchievementsResourcePath, Is.EqualTo("Tuning/achievements"));
            Assert.That(ProgressionRecorder.ResourcePath, Is.EqualTo(KitTuning.ProgressionResourcePath));
            Assert.That(KitTuning.ProgressionResourcePath, Is.EqualTo("Tuning/progression"));
        }

        [Test]
        public void TheCosmeticCatalogIsReadByTheKitAndUsedByTheLocker()
        {
            var locker = new CosmeticLocker(new ProgressionRecorder(new MemorySaveStorage(), ProgressionSpec.Default, new OfflineParticipants()));

            Assert.That(locker.Catalog.Items.Count, Is.EqualTo(CosmeticLooks.LoadCatalog().Items.Count));
        }

        [Test]
        public void EveryShippedUnlockIsAWeaponOrAnAttachmentOfTheShippedInventory()
        {
            var inventory = InventoryJson.Parse(
                Resources.Load<TextAsset>(KitTuning.InventoryResourcePath).text, "Resources/Tuning/inventory.json");
            var spec = ProgressionJson.Parse(Resources.Load<TextAsset>("Tuning/progression").text, "Resources/Tuning/progression.json");
            var known = new HashSet<string>();
            var starting = new HashSet<string>();
            var weaponIdOfItem = new Dictionary<string, string>();
            foreach (var item in inventory.Items)
            {
                if (item.IsWeapon)
                {
                    known.Add(item.WeaponId);
                    weaponIdOfItem[item.Id] = item.WeaponId;
                }

                if (item.IsAttachment)
                {
                    known.Add(item.Id);
                }
            }

            foreach (var entry in inventory.Starting)
            {
                starting.Add(entry.ItemId);
                if (weaponIdOfItem.TryGetValue(entry.ItemId, out var weaponId))
                {
                    starting.Add(weaponId);
                }
            }

            foreach (var unlock in spec.Unlocks)
            {
                Assert.That(known, Does.Contain(unlock.Id), unlock.ToString());
                Assert.That(starting, Does.Not.Contain(unlock.Id), "a starting item is never locked: " + unlock);
            }
        }
    }
}
