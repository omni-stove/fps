using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class InventoryTuningFilesTests
    {
        private static readonly string[] _languages = { "ja", "en" };
        private static readonly string[] _shippedNameKeys =
        {
            "item.bandage", "item.medkit", "item.armorPlate", "item.keycard", "item.toolbox", "item.ammo9mm", "item.ammo556", "item.ammo12ga",
            "weapon.rifle", "weapon.burstRifle", "weapon.handgun", "weapon.ricochetPistol", "weapon.shotgun",
        };
        private static readonly string[] _usableNameKeys = { "item.bandage", "item.medkit", "item.armorPlate" };

        [Test]
        public void TheServerReadsTheSameItemsAsUnity()
        {
            var root = RepositoryRoot();
            var server = File.ReadAllText(Path.Combine(root, "server-data", "tuning", "inventory.json"));

            Assert.That(server, Is.EqualTo(File.ReadAllText(UnityPath(root))));
        }

        [Test]
        public void TheShippedItemsAreReadableAndStartWithTwoBandages()
        {
            var catalog = InventoryJson.Parse(File.ReadAllText(UnityPath(RepositoryRoot())), "inventory.json");

            Assert.That(catalog.Items.Select(i => i.NameKey), Is.EqualTo(_shippedNameKeys));
            Assert.That(catalog.Rule.GridWidth, Is.EqualTo(8));
            Assert.That(catalog.Rule.GridHeight, Is.EqualTo(4));
            Assert.That(catalog.Rule.MaxWeight, Is.EqualTo(30f));
            Assert.That(catalog.Starting.Single().ItemId, Is.EqualTo(catalog.Items[0].Id));
            Assert.That(catalog.Starting.Single().Count, Is.EqualTo(2));
            Assert.That(catalog.Items.Where(i => i.IsUsable).Select(i => i.NameKey), Is.EqualTo(_usableNameKeys));
        }

        [Test]
        public void EveryWeaponItemIsASampleWeaponAndFiresItsAmmoItem()
        {
            var root = RepositoryRoot();
            var catalog = InventoryJson.Parse(File.ReadAllText(UnityPath(root)), "inventory.json");
            var samples = Directory.GetFiles(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Weapons", "Json"), "*.json")
                .Select(path => WeaponSpecJson.Parse(File.ReadAllText(path), path))
                .ToDictionary(spec => spec.Id);
            var expected = new (string NameKey, string AmmoNameKey)[]
            {
                ("weapon.rifle", "item.ammo556"),
                ("weapon.burstRifle", "item.ammo556"),
                ("weapon.handgun", "item.ammo9mm"),
                ("weapon.ricochetPistol", "item.ammo9mm"),
                ("weapon.shotgun", "item.ammo12ga"),
            };

            var weapons = catalog.Items.Where(i => i.IsWeapon).ToList();
            Assert.That(weapons.Select(w => (w.NameKey, catalog.Items.Single(i => i.Id == w.AmmoItemId).NameKey)), Is.EqualTo(expected));
            foreach (var weapon in weapons)
            {
                Assert.That(samples.ContainsKey(weapon.WeaponId), weapon.NameKey + " points at a sample weapon");
                Assert.That(samples[weapon.WeaponId].NameKey, Is.EqualTo(weapon.NameKey));
                Assert.That(samples[weapon.WeaponId].Ammo.Kind, Is.EqualTo(AmmoKind.Magazine), weapon.NameKey + " fires from a magazine");
            }
        }

        [Test]
        public void EveryItemIsNamedInEveryLanguage()
        {
            var root = RepositoryRoot();
            var catalog = InventoryJson.Parse(File.ReadAllText(UnityPath(root)), "inventory.json");
            foreach (var language in _languages)
            {
                var table = File.ReadAllText(Path.Combine(root, "Assets", "game", "Resources", "Localization", language, "items.json"));
                foreach (var item in catalog.Items)
                {
                    Assert.That(table, Does.Contain("\"" + item.NameKey + "\":"), language + " names " + item.NameKey);
                }
            }
        }

        private static string UnityPath(string root)
        {
            return Path.Combine(root, "Assets", "game", "Resources", "Tuning", "inventory.json");
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "server-data")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
