using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DeployCatalogJsonTests
    {
        private const string _source = "deployables.json";
        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";

        private static readonly string[] _expectedTypeItemNames =
        {
            "id", "role", "maxActive", "overflow", "maxHealth", "maxRange", "maxSlopeDegrees", "maxStepHeight", "width", "height", "depth", "turret", "decoy", "beacon",
        };

        private static readonly string[] _expectedTurretItemNames =
        {
            "weaponId", "rounds", "lifetimeSeconds", "sightRange", "turnDegreesPerSecond", "aimToleranceDegrees",
        };

        private static readonly string[] _expectedDecoyItemNames = { "lifetimeSeconds", "callIntervalSeconds", "callRadius" };

        private static readonly string[] _expectedBeaconItemNames = { "uses", "spawnDistance" };

        [Test]
        public void ReadsEveryItem()
        {
            var catalog = DeployCatalogJson.Parse(
                "{\"types\": [{\"id\": \"deploy-turret\", \"role\": \"Turret\", \"maxActive\": 2, \"overflow\": \"Refuse\", \"maxHealth\": 90,"
                + " \"maxRange\": 5, \"maxSlopeDegrees\": 20, \"maxStepHeight\": 0.3, \"width\": 1, \"height\": 1.5, \"depth\": 0.9,"
                + " \"turret\": {\"weaponId\": \"" + _rifleId + "\", \"rounds\": 40, \"lifetimeSeconds\": 25, \"sightRange\": 15,"
                + " \"turnDegreesPerSecond\": 180, \"aimToleranceDegrees\": 3}},"
                + " {\"id\": \"deploy-decoy\", \"role\": \"Decoy\", \"decoy\": {\"lifetimeSeconds\": 12, \"callIntervalSeconds\": 1.5, \"callRadius\": 8}}]}",
                _source);

            Assert.That(catalog.Count, Is.EqualTo(2));
            var turret = catalog.All[0];
            Assert.That(turret.Id, Is.EqualTo("deploy-turret"));
            Assert.That(turret.Role, Is.EqualTo(DeployRole.Turret));
            Assert.That(turret.Deploy.MaxActive, Is.EqualTo(2));
            Assert.That(turret.Deploy.Overflow, Is.EqualTo(DeployOverflow.Refuse));
            Assert.That(turret.Deploy.MaxHealth, Is.EqualTo(90));
            Assert.That(turret.Deploy.MaxRange, Is.EqualTo(5f));
            Assert.That(turret.Deploy.MaxSlopeDegrees, Is.EqualTo(20f));
            Assert.That(turret.Deploy.MaxStepHeight, Is.EqualTo(0.3f));
            Assert.That(turret.Deploy.Width, Is.EqualTo(1f));
            Assert.That(turret.Deploy.Height, Is.EqualTo(1.5f));
            Assert.That(turret.Deploy.Depth, Is.EqualTo(0.9f));
            Assert.That(turret.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(turret.Turret.Rounds, Is.EqualTo(40));
            Assert.That(turret.Turret.LifetimeSeconds, Is.EqualTo(25f));
            Assert.That(turret.Turret.SightRange, Is.EqualTo(15f));
            Assert.That(turret.Turret.TurnDegreesPerSecond, Is.EqualTo(180f));
            Assert.That(turret.Turret.AimToleranceDegrees, Is.EqualTo(3f));
            Assert.That(turret.Decoy, Is.Null);

            var decoy = catalog.All[1];
            Assert.That(decoy.Role, Is.EqualTo(DeployRole.Decoy));
            Assert.That(decoy.Decoy.LifetimeSeconds, Is.EqualTo(12f));
            Assert.That(decoy.Decoy.CallIntervalSeconds, Is.EqualTo(1.5f));
            Assert.That(decoy.Decoy.CallRadius, Is.EqualTo(8f));
            Assert.That(decoy.Turret, Is.Null);
            Assert.That(decoy.WeaponId, Is.Null);

            Assert.That(catalog.TryGet("deploy-decoy", out var found), Is.True);
            Assert.That(found, Is.SameAs(decoy));
            Assert.That(catalog.TryGet("missing", out _), Is.False);
            Assert.That(catalog.TryGet(null, out _), Is.False);
        }

        [Test]
        public void ReadsABeacon()
        {
            var catalog = DeployCatalogJson.Parse(
                "{\"types\": [{\"id\": \"deploy-beacon\", \"role\": \"Beacon\", \"maxActive\": 2, \"maxHealth\": 80,"
                + " \"beacon\": {\"uses\": 5, \"spawnDistance\": 1.5}}]}",
                _source);

            var beacon = catalog.All[0];
            Assert.That(beacon.Role, Is.EqualTo(DeployRole.Beacon));
            Assert.That(beacon.Deploy.MaxActive, Is.EqualTo(2));
            Assert.That(beacon.Deploy.MaxHealth, Is.EqualTo(80));
            Assert.That(beacon.Deploy.Width, Is.EqualTo(0.4f), "the shape is left out and keeps the beacon's default");
            Assert.That(beacon.Beacon.Uses, Is.EqualTo(5));
            Assert.That(beacon.Beacon.SpawnDistance, Is.EqualTo(1.5f));
            Assert.That(beacon.Turret, Is.Null);
            Assert.That(beacon.Decoy, Is.Null);
            Assert.That(beacon.WeaponId, Is.Null);
        }

        [Test]
        public void ABeaconWithoutABeaconObjectUsesTheDefaults()
        {
            var beacon = DeployCatalogJson.Parse("{\"types\": [{\"id\": \"b\", \"role\": \"Beacon\"}]}", _source).All[0];

            Assert.That(beacon.Deploy.MaxActive, Is.EqualTo(1));
            Assert.That(beacon.Deploy.MaxHealth, Is.EqualTo(150));
            Assert.That(beacon.Deploy.Width, Is.EqualTo(0.4f));
            Assert.That(beacon.Deploy.Height, Is.EqualTo(1.2f));
            Assert.That(beacon.Deploy.Depth, Is.EqualTo(0.4f));
            Assert.That(beacon.Beacon.Uses, Is.EqualTo(3));
            Assert.That(beacon.Beacon.SpawnDistance, Is.EqualTo(1f));
        }

        [Test]
        public void ABadBeaconIsRejectedAtItsItem()
        {
            var uses = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"b\", \"role\": \"Beacon\", \"beacon\": {\"uses\": 0}}]}", _source));
            var distance = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"b\", \"role\": \"Beacon\", \"beacon\": {\"spawnDistance\": -1}}]}", _source));
            var unknown = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"b\", \"role\": \"Beacon\", \"beacon\": {\"charges\": 2}}]}", _source));
            var misplaced = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"s\", \"role\": \"Shield\", \"beacon\": {\"uses\": 2}}]}", _source));

            Assert.That(uses.Item, Is.EqualTo("types[0].beacon.uses"));
            Assert.That(uses.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(distance.Item, Is.EqualTo("types[0].beacon.spawnDistance"));
            Assert.That(unknown.Item, Is.EqualTo("types[0].beacon.charges"));
            Assert.That(misplaced.Item, Is.EqualTo("types[0].beacon"));
        }

        [Test]
        public void LeftOutItemsUseTheDefaultsOfTheRole()
        {
            var catalog = DeployCatalogJson.Parse(
                "{\"types\": [{\"id\": \"t\", \"role\": \"Turret\", \"turret\": {\"weaponId\": \"" + _rifleId + "\"}},"
                + " {\"id\": \"s\", \"role\": \"Shield\"}, {\"id\": \"d\", \"role\": \"Decoy\"}]}",
                _source);

            var turret = catalog.All[0];
            Assert.That(turret.Deploy.MaxActive, Is.EqualTo(1));
            Assert.That(turret.Deploy.MaxHealth, Is.EqualTo(150));
            Assert.That(turret.Deploy.Width, Is.EqualTo(0.8f));
            Assert.That(turret.Deploy.Height, Is.EqualTo(1.2f));
            Assert.That(turret.Deploy.Depth, Is.EqualTo(0.8f));
            Assert.That(turret.Deploy.Overflow, Is.EqualTo(DeployOverflow.ReplaceOldest));
            Assert.That(turret.Deploy.MaxRange, Is.EqualTo(4f));
            Assert.That(turret.Turret.Rounds, Is.EqualTo(60));
            Assert.That(turret.Turret.LifetimeSeconds, Is.EqualTo(30f));
            Assert.That(turret.Turret.SightRange, Is.EqualTo(12f));
            Assert.That(turret.Turret.TurnDegreesPerSecond, Is.EqualTo(270f));
            Assert.That(turret.Turret.AimToleranceDegrees, Is.EqualTo(5f));

            var shield = catalog.All[1];
            Assert.That(shield.Deploy.MaxActive, Is.EqualTo(2));
            Assert.That(shield.Deploy.MaxHealth, Is.EqualTo(300));
            Assert.That(shield.Deploy.Width, Is.EqualTo(2f));
            Assert.That(shield.Deploy.Height, Is.EqualTo(1.4f));
            Assert.That(shield.Deploy.Depth, Is.EqualTo(0.2f));

            var decoy = catalog.All[2];
            Assert.That(decoy.Deploy.MaxActive, Is.EqualTo(1));
            Assert.That(decoy.Deploy.MaxHealth, Is.EqualTo(100));
            Assert.That(decoy.Deploy.Width, Is.EqualTo(0.6f));
            Assert.That(decoy.Deploy.Height, Is.EqualTo(1.8f));
            Assert.That(decoy.Decoy.LifetimeSeconds, Is.EqualTo(20f));
            Assert.That(decoy.Decoy.CallIntervalSeconds, Is.EqualTo(2f));
            Assert.That(decoy.Decoy.CallRadius, Is.EqualTo(10f));
        }

        [Test]
        public void AnEmptyDocumentHasNoTypes()
        {
            Assert.That(DeployCatalogJson.Parse("{}", _source).Count, Is.EqualTo(0));
            Assert.That(DeployCatalog.Empty.Count, Is.EqualTo(0));
        }

        [Test]
        public void ATurretWithoutATurretObjectIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(() => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"t\", \"role\": \"Turret\"}]}", _source));

            Assert.That(e.SourceName, Is.EqualTo(_source));
            Assert.That(e.Item, Is.EqualTo("types[0].turret"));
        }

        [Test]
        public void ATurretWithoutAWeaponIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"t\", \"role\": \"Turret\", \"turret\": {\"rounds\": 5}}]}", _source));

            Assert.That(e.Item, Is.EqualTo("types[0].turret.weaponId"));
        }

        [Test]
        public void AShieldWithATurretObjectIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"s\", \"role\": \"Shield\", \"turret\": {\"weaponId\": \"w\"}}]}", _source));

            Assert.That(e.Item, Is.EqualTo("types[0].turret"));
        }

        [Test]
        public void AnOutOfRangeValueIsRejectedAtItsItem()
        {
            var shape = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"s\", \"role\": \"Shield\", \"maxHealth\": 0}]}", _source));
            var turret = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"t\", \"role\": \"Turret\", \"turret\": {\"weaponId\": \"w\", \"rounds\": 0}}]}", _source));
            var decoy = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"d\", \"role\": \"Decoy\", \"decoy\": {\"callRadius\": -1}}]}", _source));

            Assert.That(shape.Item, Is.EqualTo("types[0].maxHealth"));
            Assert.That(shape.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(turret.Item, Is.EqualTo("types[0].turret.rounds"));
            Assert.That(decoy.Item, Is.EqualTo("types[0].decoy.callRadius"));
        }

        [Test]
        public void AnIdListedTwiceIsRejected()
        {
            var e = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"s\", \"role\": \"Shield\"}, {\"id\": \"s\", \"role\": \"Decoy\"}]}", _source));

            Assert.That(e.Item, Is.EqualTo("types[1].id"));
        }

        [Test]
        public void UnknownItemsAndRolesAreRejected()
        {
            var item = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"s\", \"role\": \"Shield\", \"armor\": 3}]}", _source));
            var role = Assert.Throws<TuningJsonException>(
                () => DeployCatalogJson.Parse("{\"types\": [{\"id\": \"m\", \"role\": \"Mine\"}]}", _source));
            var broken = Assert.Throws<TuningJsonException>(() => DeployCatalogJson.Parse("{\"types\": [", _source));

            Assert.That(item.Item, Is.EqualTo("types[0].armor"));
            Assert.That(role.Item, Is.EqualTo("types[0].role"));
            Assert.That(broken.SourceName, Is.EqualTo(_source));
        }

        [Test]
        public void ItemNamesMatchTheDocumentedNames()
        {
            Assert.That(DeployCatalogJson.TypeItemNames, Is.EqualTo(_expectedTypeItemNames));
            Assert.That(DeployCatalogJson.TurretItemNames, Is.EqualTo(_expectedTurretItemNames));
            Assert.That(DeployCatalogJson.DecoyItemNames, Is.EqualTo(_expectedDecoyItemNames));
            Assert.That(DeployCatalogJson.BeaconItemNames, Is.EqualTo(_expectedBeaconItemNames));
        }

        [Test]
        public void ATypeChecksItsRoleParts()
        {
            Assert.Throws<ArgumentException>(() => new DeployType("", DeployRole.Shield, DeploySpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("t", DeployRole.Turret, DeploySpec.Default, TurretSpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("d", DeployRole.Decoy, DeploySpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("s", DeployRole.Shield, DeploySpec.Default, decoy: DecoySpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("b", DeployRole.Beacon, DeploySpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("d", DeployRole.Decoy, DeploySpec.Default, decoy: DecoySpec.Default, beacon: BeaconSpec.Default));
            Assert.That(new DeployType("b", DeployRole.Beacon, DeploySpec.Default, beacon: BeaconSpec.Default).Beacon, Is.SameAs(BeaconSpec.Default));
            Assert.Throws<ArgumentNullException>(() => new DeployType("s", DeployRole.Shield, null));
            Assert.Throws<ArgumentException>(
                () => new DeployCatalog(new[] { new DeployType("s", DeployRole.Shield, DeploySpec.Default), new DeployType("s", DeployRole.Shield, DeploySpec.Default) }));
        }
    }
}
