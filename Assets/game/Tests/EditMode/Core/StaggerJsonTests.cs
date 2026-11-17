using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaggerJsonTests
    {
        private static readonly string[] _expectedItemNames = { "player", "enemy" };

        private static readonly string[] _expectedPlayerItemNames =
        {
            "flinchSeconds", "kneelSeconds", "stumbleSeconds", "blastSeconds",
            "headDamage", "limbDamage", "meleeDamage", "explosionDamage",
            "windowSeconds", "immunitySeconds", "moveMultiplier", "turnMultiplier",
        };

        private static readonly string[] _expectedEnemyItemNames =
        {
            "flinchSeconds", "kneelSeconds", "stumbleSeconds", "blastSeconds",
            "headDamage", "limbDamage", "meleeDamage", "explosionDamage",
            "windowSeconds", "immunitySeconds",
        };

        [Test]
        public void TheItemNamesFollowTheSpecArguments()
        {
            Assert.That(StaggerJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(StaggerJson.PlayerItemNames, Is.EqualTo(_expectedPlayerItemNames));
            Assert.That(StaggerJson.EnemyItemNames, Is.EqualTo(_expectedEnemyItemNames));
        }

        [Test]
        public void ReadsEveryPlayerItem()
        {
            var tuning = StaggerJson.Parse(
                "{\"player\": {\"flinchSeconds\": 0.3, \"kneelSeconds\": 0.7, \"stumbleSeconds\": 0.5, \"blastSeconds\": 0.9,"
                + " \"headDamage\": 30, \"limbDamage\": 12, \"meleeDamage\": 2, \"explosionDamage\": 4,"
                + " \"windowSeconds\": 1.5, \"immunitySeconds\": 2.5, \"moveMultiplier\": 0.5, \"turnMultiplier\": 0.25}}",
                "stagger.json");
            var player = tuning.Player;

            Assert.That(player.FlinchSeconds, Is.EqualTo(0.3f));
            Assert.That(player.KneelSeconds, Is.EqualTo(0.7f));
            Assert.That(player.StumbleSeconds, Is.EqualTo(0.5f));
            Assert.That(player.BlastSeconds, Is.EqualTo(0.9f));
            Assert.That(player.HeadDamage, Is.EqualTo(30f));
            Assert.That(player.LimbDamage, Is.EqualTo(12f));
            Assert.That(player.MeleeDamage, Is.EqualTo(2f));
            Assert.That(player.ExplosionDamage, Is.EqualTo(4f));
            Assert.That(player.WindowSeconds, Is.EqualTo(1.5f));
            Assert.That(player.ImmunitySeconds, Is.EqualTo(2.5f));
            Assert.That(player.MoveMultiplier, Is.EqualTo(0.5f));
            Assert.That(player.TurnMultiplier, Is.EqualTo(0.25f));
            Assert.That(tuning.Enemy, Is.SameAs(StaggerSpec.EnemyDefault), "an omitted enemy object keeps the enemy defaults");
        }

        [Test]
        public void ReadsEveryEnemyItem()
        {
            var tuning = StaggerJson.Parse(
                "{\"enemy\": {\"flinchSeconds\": 0.6, \"kneelSeconds\": 2, \"stumbleSeconds\": 1.2, \"blastSeconds\": 1.4,"
                + " \"headDamage\": 25, \"limbDamage\": 9, \"meleeDamage\": 3, \"explosionDamage\": 5,"
                + " \"windowSeconds\": 0.5, \"immunitySeconds\": 3}}",
                "stagger.json");
            var enemy = tuning.Enemy;

            Assert.That(enemy.FlinchSeconds, Is.EqualTo(0.6f));
            Assert.That(enemy.KneelSeconds, Is.EqualTo(2f));
            Assert.That(enemy.StumbleSeconds, Is.EqualTo(1.2f));
            Assert.That(enemy.BlastSeconds, Is.EqualTo(1.4f));
            Assert.That(enemy.HeadDamage, Is.EqualTo(25f));
            Assert.That(enemy.LimbDamage, Is.EqualTo(9f));
            Assert.That(enemy.MeleeDamage, Is.EqualTo(3f));
            Assert.That(enemy.ExplosionDamage, Is.EqualTo(5f));
            Assert.That(enemy.WindowSeconds, Is.EqualTo(0.5f));
            Assert.That(enemy.ImmunitySeconds, Is.EqualTo(3f));
            Assert.That(enemy.MoveMultiplier, Is.EqualTo(1f));
            Assert.That(enemy.TurnMultiplier, Is.EqualTo(1f));
            Assert.That(tuning.Player, Is.SameAs(StaggerSpec.PlayerDefault), "an omitted player object keeps the player defaults");
        }

        [Test]
        public void OmittedItemsUseTheirSideDefaults()
        {
            var tuning = StaggerJson.Parse("{\"player\": {\"flinchSeconds\": 0.3}, \"enemy\": {\"kneelSeconds\": 2}}", "stagger.json");

            Assert.That(tuning.Player.FlinchSeconds, Is.EqualTo(0.3f));
            Assert.That(tuning.Player.KneelSeconds, Is.EqualTo(0f));
            Assert.That(tuning.Player.StumbleSeconds, Is.EqualTo(0.4f));
            Assert.That(tuning.Player.BlastSeconds, Is.EqualTo(0.6f));
            Assert.That(tuning.Player.ImmunitySeconds, Is.EqualTo(1f));
            Assert.That(tuning.Player.MoveMultiplier, Is.EqualTo(0.35f));
            Assert.That(tuning.Player.TurnMultiplier, Is.EqualTo(0.35f));

            Assert.That(tuning.Enemy.KneelSeconds, Is.EqualTo(2f));
            Assert.That(tuning.Enemy.FlinchSeconds, Is.EqualTo(0.5f));
            Assert.That(tuning.Enemy.HeadDamage, Is.EqualTo(20f));
            Assert.That(tuning.Enemy.LimbDamage, Is.EqualTo(8f));
            Assert.That(tuning.Enemy.ImmunitySeconds, Is.EqualTo(2f));
        }

        [Test]
        public void AnEmptyObjectIsTheDefaults()
        {
            var tuning = StaggerJson.Parse("{}", "stagger.json");

            Assert.That(tuning.Player, Is.SameAs(StaggerSpec.PlayerDefault));
            Assert.That(tuning.Enemy, Is.SameAs(StaggerSpec.EnemyDefault));
            Assert.That(StaggerTuning.Default.Player, Is.SameAs(StaggerSpec.PlayerDefault));
            Assert.That(StaggerTuning.Default.Enemy, Is.SameAs(StaggerSpec.EnemyDefault));
        }

        [Test]
        public void ABadValueNamesTheItem()
        {
            var error = Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"player\": {\"windowSeconds\": 0}}", "stagger.json"));
            Assert.That(error.Item, Is.EqualTo("player.windowSeconds"));
            Assert.That(error.SourceName, Is.EqualTo("stagger.json"));
            Assert.That(error.Message, Does.StartWith("stagger JSON 'stagger.json' item 'player.windowSeconds' is invalid: "));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());

            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"player\": {\"turnMultiplier\": 2}}", "s")).Item,
                Is.EqualTo("player.turnMultiplier"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"enemy\": {\"kneelSeconds\": -1}}", "s")).Item,
                Is.EqualTo("enemy.kneelSeconds"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"enemy\": {\"headDamage\": \"a lot\"}}", "s")).Item,
                Is.EqualTo("enemy.headDamage"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"enemy\": {\"headDamage\": null}}", "s")).Item,
                Is.EqualTo("enemy.headDamage"));
        }

        [Test]
        public void UnknownItemsAreRejected()
        {
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"enemy\": {\"moveMultiplier\": 0.5}}", "s")).Item,
                Is.EqualTo("enemy.moveMultiplier"),
                "enemies stop while staggered, so the walk and turn multipliers would do nothing");
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"boss\": {}}", "s")).Item,
                Is.EqualTo("boss"));
            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"player\": 1}", "s")).Item,
                Is.EqualTo("player"));
        }

        [Test]
        public void BrokenJsonIsRejected()
        {
            var error = Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("{\"player\": ", "stagger.json"));
            Assert.That(error.Item, Is.Null);
            Assert.That(error.Message, Does.StartWith("stagger JSON 'stagger.json' is not valid JSON: "));

            Assert.That(
                Assert.Throws<TuningJsonException>(() => StaggerJson.Parse("[]", null)).Message,
                Does.StartWith("stagger JSON '(unnamed stagger JSON)': the top level must be an object"));
            Assert.Throws<ArgumentNullException>(() => StaggerJson.Parse(null, "s"));
            Assert.Throws<ArgumentNullException>(() => new StaggerTuning(null, StaggerSpec.EnemyDefault));
            Assert.Throws<ArgumentNullException>(() => new StaggerTuning(StaggerSpec.PlayerDefault, null));
        }
    }
}
