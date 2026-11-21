using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BotsMountedGunTuningJsonTests
    {
        [Test]
        public void WithoutTheItemBotsDoNotManGuns()
        {
            Assert.That(TuningJson.ParseBots("{}", "bots.json").MountedGun, Is.Null);
            Assert.That(NetBotSpec.Default.MountedGun, Is.Null);
        }

        [Test]
        public void AnEmptyItemUsesTheOfflineDefaults()
        {
            var gun = TuningJson.ParseBots("{\"mountedGun\": {}}", "bots.json").MountedGun;

            Assert.That(gun, Is.Not.Null);
            Assert.That(gun.SeekRadius, Is.EqualTo(MountedGunUseSpec.DefaultSeekRadius));
            Assert.That(gun.GiveUpSeconds, Is.EqualTo(MountedGunUseSpec.DefaultGiveUpSeconds));
            Assert.That(gun.RemountDelaySeconds, Is.EqualTo(MountedGunUseSpec.DefaultRemountDelaySeconds));
        }

        [Test]
        public void ReadsTheMountedGunValues()
        {
            var gun = TuningJson.ParseBots(
                "{\"archetype\": \"Rifleman\", \"mountedGun\": {\"seekRadius\": 12, \"giveUpSeconds\": 4.5, \"remountDelaySeconds\": 0}}",
                "bots.json").MountedGun;

            Assert.That(gun.SeekRadius, Is.EqualTo(12f));
            Assert.That(gun.GiveUpSeconds, Is.EqualTo(4.5f));
            Assert.That(gun.RemountDelaySeconds, Is.Zero);
        }

        [TestCase("{\"mountedGun\": {\"seekRadius\": -1}}", "mountedGun.seekRadius")]
        [TestCase("{\"mountedGun\": {\"giveUpSeconds\": 0}}", "mountedGun.giveUpSeconds")]
        [TestCase("{\"mountedGun\": {\"remountDelaySeconds\": -2}}", "mountedGun.remountDelaySeconds")]
        [TestCase("{\"mountedGun\": {\"radius\": 3}}", "mountedGun.radius")]
        [TestCase("{\"mountedGun\": true}", "mountedGun")]
        public void ABadMountedGunItemIsNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseBots(json, "bots.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bots.json").And.Contain("'" + item + "'"));
        }
    }
}
