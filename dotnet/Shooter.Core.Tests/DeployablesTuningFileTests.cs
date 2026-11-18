using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // サーバーの設置物の値は Unity のアセット (Samples~/Demo/Abilities) を書き出したものではないので、食い違いをここで見つける
    public sealed class DeployablesTuningFileTests
    {
        private static readonly string[] _shippedIds = { "deploy-turret", "deploy-shield", "deploy-decoy" };
        private static readonly string[] _assetNames = { "DeployTurret", "DeployShield", "DeployDecoy" };
        private static readonly DeployRole[] _shippedRoles = { DeployRole.Turret, DeployRole.Shield, DeployRole.Decoy };

        [Test]
        public void TheShippedFileIsReadableAndHasATurretAShieldAndADecoy()
        {
            var catalog = Read(RepositoryRoot());

            Assert.That(catalog.All.Select(t => t.Id), Is.EqualTo(_shippedIds));
            Assert.That(catalog.All.Select(t => t.Role), Is.EqualTo(_shippedRoles));
        }

        [Test]
        public void TheTurretFiresASampleWeapon()
        {
            var root = RepositoryRoot();
            var turret = Read(root).All.Single(t => t.Role == DeployRole.Turret);
            var samples = Directory.GetFiles(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Weapons", "Json"), "*.json")
                .Select(path => WeaponSpecJson.Parse(File.ReadAllText(path), path))
                .ToDictionary(spec => spec.Id);

            Assert.That(samples.ContainsKey(turret.WeaponId), Is.True);
            Assert.That(samples[turret.WeaponId].NameKey, Is.EqualTo("weapon.rifle"));
            Assert.That(samples[turret.WeaponId].Round.Delivery, Is.EqualTo(RoundDelivery.Hitscan), "the server turret fires only hitscan rounds");
        }

        [Test]
        public void EveryTypeHasTheIdAndShapeOfItsDemoAsset()
        {
            var root = RepositoryRoot();
            var catalog = Read(root);
            for (var i = 0; i < _assetNames.Length; i++)
            {
                var asset = File.ReadAllText(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Abilities", _assetNames[i] + ".asset"));
                var type = catalog.All[i];
                var deploy = type.Deploy;
                Assert.That(asset, Does.Contain("  _id: " + type.Id + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxActive: " + Text(deploy.MaxActive) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxHealth: " + Text(deploy.MaxHealth) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxRange: " + Text(deploy.MaxRange) + "\n"), _assetNames[i]);
                Assert.That(
                    asset,
                    Does.Contain("  _size: {x: " + Text(deploy.Width) + ", y: " + Text(deploy.Height) + ", z: " + Text(deploy.Depth) + "}"),
                    _assetNames[i]);
            }
        }

        private static DeployCatalog Read(string root)
        {
            var path = Path.Combine(root, "server-data", "tuning", "deployables.json");
            return DeployCatalogJson.Parse(File.ReadAllText(path), path);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

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
