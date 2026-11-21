using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BotsArchetypeTuningJsonTests
    {
        [Test]
        public void LeftOutArchetypeValuesAreTheEnemyDefaults()
        {
            var brain = TuningJson.ParseBots("{\"archetype\": \"Charger\"}", "bots.json").Brain;

            Assert.That(brain.Archetype, Is.EqualTo(EnemyArchetype.Charger));
            Assert.That(brain.Charger, Is.SameAs(ChargerSpec.Default));
            Assert.That(brain.Sniper, Is.SameAs(SniperSpec.Default));
            Assert.That(brain.Bomber, Is.SameAs(BomberSpec.Default));
            Assert.That(brain.Shield, Is.SameAs(ShieldSpec.Default));
        }

        [Test]
        public void ReadsTheChargerValues()
        {
            var charger = TuningJson.ParseBots(
                "{\"charger\": {\"triggerDistance\": 6, \"windupSeconds\": 1.2, \"chargeSpeed\": 14, \"chargeDistance\": 12,"
                + " \"hitReach\": 1.5, \"impactDamage\": 50, \"stunSeconds\": 2, \"recoverSeconds\": 0.5}}",
                "bots.json").Brain.Charger;

            Assert.That(charger.TriggerDistance, Is.EqualTo(6f));
            Assert.That(charger.WindupSeconds, Is.EqualTo(1.2f));
            Assert.That(charger.ChargeSpeed, Is.EqualTo(14f));
            Assert.That(charger.ChargeDistance, Is.EqualTo(12f));
            Assert.That(charger.HitReach, Is.EqualTo(1.5f));
            Assert.That(charger.ImpactDamage, Is.EqualTo(50));
            Assert.That(charger.StunSeconds, Is.EqualTo(2f));
            Assert.That(charger.RecoverSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void ReadsTheSniperValues()
        {
            var sniper = TuningJson.ParseBots(
                "{\"sniper\": {\"aimSeconds\": 1.5, \"shotDamageMultiplier\": 4, \"reaimDelaySeconds\": 0.5, \"relocateGiveUpSeconds\": 6}}",
                "bots.json").Brain.Sniper;

            Assert.That(sniper.AimSeconds, Is.EqualTo(1.5f));
            Assert.That(sniper.ShotDamageMultiplier, Is.EqualTo(4f));
            Assert.That(sniper.ReaimDelaySeconds, Is.EqualTo(0.5f));
            Assert.That(sniper.RelocateGiveUpSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void ReadsTheBomberValuesAndItsBlasts()
        {
            var bomber = TuningJson.ParseBots(
                "{\"bomber\": {\"triggerDistance\": 2, \"fuseSeconds\": 1, \"runSpeedMultiplier\": 1.4,"
                + " \"blast\": {\"maxDamage\": 90, \"radius\": 5, \"fullDamageRadius\": 2},"
                + " \"deathBlast\": {\"maxDamage\": 20, \"radius\": 2}}}",
                "bots.json").Brain.Bomber;

            Assert.That(bomber.TriggerDistance, Is.EqualTo(2f));
            Assert.That(bomber.FuseSeconds, Is.EqualTo(1f));
            Assert.That(bomber.RunSpeedMultiplier, Is.EqualTo(1.4f));
            Assert.That(bomber.Blast.MaxDamage, Is.EqualTo(90));
            Assert.That(bomber.Blast.Radius, Is.EqualTo(5f));
            Assert.That(bomber.Blast.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(bomber.DeathBlast.MaxDamage, Is.EqualTo(20));
            Assert.That(bomber.DeathBlast.Radius, Is.EqualTo(2f));
            Assert.That(bomber.DeathBlast.FullDamageRadius, Is.EqualTo(BomberSpec.DefaultDeathBlastFullDamageRadius));
        }

        [Test]
        public void ReadsTheShieldValuesAndItsBash()
        {
            var shield = TuningJson.ParseBots(
                "{\"shield\": {\"approachSpeedMultiplier\": 0.7, \"holdDistance\": 1, \"meleeLowerSeconds\": 2, \"explosionLowerSeconds\": 3,"
                + " \"bash\": {\"damage\": 45, \"reach\": 2}}}",
                "bots.json").Brain.Shield;

            Assert.That(shield.ApproachSpeedMultiplier, Is.EqualTo(0.7f));
            Assert.That(shield.HoldDistance, Is.EqualTo(1f));
            Assert.That(shield.MeleeLowerSeconds, Is.EqualTo(2f));
            Assert.That(shield.ExplosionLowerSeconds, Is.EqualTo(3f));
            Assert.That(shield.Bash.Damage, Is.EqualTo(45));
            Assert.That(shield.Bash.Reach, Is.EqualTo(2f));
            Assert.That(shield.Bash.SwingSeconds, Is.EqualTo(ShieldSpec.DefaultBashSwingSeconds));
        }

        [Test]
        public void TheValuesOfAnotherArchetypeAreKeptForSwitching()
        {
            var brain = TuningJson.ParseBots("{\"archetype\": \"Rifleman\", \"sniper\": {\"aimSeconds\": 1}}", "bots.json").Brain;

            Assert.That(brain.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(brain.Sniper.AimSeconds, Is.EqualTo(1f));
        }

        [TestCase("{\"charger\": {\"triggerDistance\": 20}}", "charger.triggerDistance")]
        [TestCase("{\"charger\": {\"impactDamage\": 0}}", "charger.impactDamage")]
        [TestCase("{\"charger\": {\"speed\": 3}}", "charger.speed")]
        [TestCase("{\"sniper\": {\"aimSeconds\": 0}}", "sniper.aimSeconds")]
        [TestCase("{\"bomber\": {\"triggerDistance\": 9}}", "bomber.triggerDistance")]
        [TestCase("{\"bomber\": {\"deathBlast\": {\"maxDamage\": 500}}}", "bomber.deathBlast")]
        [TestCase("{\"bomber\": {\"blast\": {\"radius\": -1}}}", "bomber.blast.radius")]
        [TestCase("{\"shield\": {\"approachSpeedMultiplier\": 1.5}}", "shield.approachSpeedMultiplier")]
        [TestCase("{\"shield\": {\"holdDistance\": 5}}", "shield.holdDistance")]
        [TestCase("{\"shield\": {\"bash\": {\"damage\": -1}}}", "shield.bash.damage")]
        [TestCase("{\"shield\": 1}", "shield")]
        public void ABadArchetypeItemIsNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseBots(json, "bots.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bots.json").And.Contain("'" + item + "'"));
        }
    }
}
