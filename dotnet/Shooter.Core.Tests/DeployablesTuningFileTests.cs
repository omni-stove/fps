using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // サーバーの設置物の値は Unity のアセット (Samples~/Demo/Abilities) を書き出したものではないので、食い違いをここで見つける
    public sealed class DeployablesTuningFileTests
    {
        private static readonly string[] _shippedIds =
        {
            "deploy-turret", "deploy-shield", "deploy-decoy", "deploy-beacon", "deploy-mine", "deploy-remote-charge",
        };

        // ビーコンはビルダーが作るので比べず、手で作った地雷と C4 のアセットとは形も比べる
        private static readonly string[] _assetNames = { "DeployTurret", "DeployShield", "DeployDecoy", null, "DeployMine", "DeployRemoteCharge" };
        private static readonly DeployRole[] _shippedRoles =
        {
            DeployRole.Turret, DeployRole.Shield, DeployRole.Decoy, DeployRole.Beacon, DeployRole.Mine, DeployRole.RemoteCharge,
        };

        [Test]
        public void TheShippedFileIsReadableAndHasEveryRoleTheServerPlaces()
        {
            var catalog = Read(RepositoryRoot());

            Assert.That(catalog.All.Select(t => t.Id), Is.EqualTo(_shippedIds));
            Assert.That(catalog.All.Select(t => t.Role), Is.EqualTo(_shippedRoles));
        }

        // Demo のビーコンのアセットはビルダー (AbilityAssetBuilder.ConfigureBeacon) が作り、キットの既定 (BeaconSpec.Default*) のままにするので、既定と比べる
        [Test]
        public void TheBeaconHasTheShapeAndUsesOfTheDemoBeacon()
        {
            var beacon = Read(RepositoryRoot()).All.Single(t => t.Role == DeployRole.Beacon);

            Assert.That(beacon.Id, Is.EqualTo("deploy-beacon"));
            Assert.That(beacon.Deploy.MaxActive, Is.EqualTo(BeaconSpec.DefaultMaxActive));
            Assert.That(beacon.Deploy.Overflow, Is.EqualTo(DeploySpec.DefaultOverflow));
            Assert.That(beacon.Deploy.MaxHealth, Is.EqualTo(BeaconSpec.DefaultMaxHealth));
            Assert.That(beacon.Deploy.MaxRange, Is.EqualTo(DeploySpec.DefaultMaxRange));
            Assert.That(beacon.Deploy.MaxSlopeDegrees, Is.EqualTo(DeploySpec.DefaultMaxSlopeDegrees));
            Assert.That(beacon.Deploy.MaxStepHeight, Is.EqualTo(DeploySpec.DefaultMaxStepHeight));
            Assert.That(beacon.Deploy.Width, Is.EqualTo(BeaconSpec.DefaultWidth));
            Assert.That(beacon.Deploy.Height, Is.EqualTo(BeaconSpec.DefaultHeight));
            Assert.That(beacon.Deploy.Depth, Is.EqualTo(BeaconSpec.DefaultDepth));
            Assert.That(beacon.Beacon.Uses, Is.EqualTo(BeaconSpec.DefaultUses));
            Assert.That(beacon.Beacon.SpawnDistance, Is.EqualTo(BeaconSpec.DefaultSpawnDistance));
        }

        // 回数と間隔はキットの既定ではなく、ビルダーの AbilityAssetBuilder.BeaconAbility* の値。Editor のアセンブリは dotnet から引けないので値で比べる
        [Test]
        public void TheBeaconHasTheChargesOfTheDemoBeacon()
        {
            var beacon = Read(RepositoryRoot()).All.Single(t => t.Role == DeployRole.Beacon);

            Assert.That(beacon.Ability.MaxCharges, Is.EqualTo(1));
            Assert.That(beacon.Ability.RechargeSeconds, Is.EqualTo(45f));
            Assert.That(beacon.Ability.CooldownSeconds, Is.EqualTo(1f));
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
                if (_assetNames[i] == null)
                {
                    continue;
                }

                var asset = File.ReadAllText(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Abilities", _assetNames[i] + ".asset"));
                var type = catalog.All[i];
                Assert.That(asset, Does.Contain("  _overflow: " + Text((int)type.Deploy.Overflow) + "\n"), _assetNames[i]);
                var deploy = type.Deploy;
                Assert.That(asset, Does.Contain("  _id: " + type.Id + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxActive: " + Text(deploy.MaxActive) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxHealth: " + Text(deploy.MaxHealth) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxRange: " + Text(deploy.MaxRange) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _maxCharges: " + Text(type.Ability.MaxCharges) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _rechargeSeconds: " + Text(type.Ability.RechargeSeconds) + "\n"), _assetNames[i]);
                Assert.That(asset, Does.Contain("  _cooldownSeconds: " + Text(type.Ability.CooldownSeconds) + "\n"), _assetNames[i]);
                Assert.That(
                    asset,
                    Does.Contain("  _size: {x: " + Text(deploy.Width) + ", y: " + Text(deploy.Height) + ", z: " + Text(deploy.Depth) + "}"),
                    _assetNames[i]);
            }
        }

        [Test]
        public void TheMineAndTheRemoteChargeExplodeLikeTheirDemoAssets()
        {
            var root = RepositoryRoot();
            var catalog = Read(root);
            var mine = catalog.All.Single(t => t.Role == DeployRole.Mine);
            var charge = catalog.All.Single(t => t.Role == DeployRole.RemoteCharge);
            var mineAsset = File.ReadAllText(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Abilities", "DeployMine.asset"));
            var chargeAsset = File.ReadAllText(Path.Combine(root, "Assets", "game", "Samples~", "Demo", "Abilities", "DeployRemoteCharge.asset"));

            Assert.That(mineAsset, Does.Contain("  _armSeconds: " + Text(mine.Mine.ArmSeconds) + "\n"));
            Assert.That(mineAsset, Does.Contain("  _triggerRadius: " + Text(mine.Mine.TriggerRadius) + "\n"));
            AssertExplosion(mineAsset, mine.Mine.Explosion, "DeployMine");
            AssertExplosion(chargeAsset, charge.RemoteCharge.Explosion, "DeployRemoteCharge");
        }

        private static void AssertExplosion(string asset, ExplosionSpec explosion, string name)
        {
            Assert.That(asset, Does.Contain("  _maxDamage: " + Text(explosion.Damage.MaxDamage) + "\n"), name);
            Assert.That(asset, Does.Contain("  _damageRadius: " + Text(explosion.Damage.Radius) + "\n"), name);
            Assert.That(asset, Does.Contain("  _fullDamageRadius: " + Text(explosion.Damage.FullDamageRadius) + "\n"), name);
            Assert.That(asset, Does.Contain("  _structureDamageAtCenter: " + Text(explosion.StructureDamageAtCenter) + "\n"), name);
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
