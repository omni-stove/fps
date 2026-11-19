using System;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class GameRulesMutatorTests
    {
        private const string LowHealthJson = "{ \"playerMaxHealth\": 50 }";
        private const string HeadshotsOnlyJson = "{ \"zoneDamage\": { \"body\": 0, \"limb\": 0 } }";

        // 書いていない欄が元のままかを見分けられるよう、既定と違う値を持たせた土台。
        private static readonly GameRules _base = GameRules.Default
            .WithFriendlyFire(FriendlyFire.On)
            .WithRegenPerSecond(7f)
            .WithViewMode(ViewMode.ThirdPerson)
            .WithActions(ActionRules.Default.WithCover(Cover.On).WithBlindFire(BlindFire.On).WithGrenade(Grenade.On).WithGrenadeCount(2));

        [Test]
        public void ApplyOverwritesOnlyTheWrittenItems()
        {
            var mutator = new GameRulesMutator("low-health", LowHealthJson);

            var rules = mutator.Apply(_base);

            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(7f));
            Assert.That(rules.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(rules.Actions, Is.SameAs(_base.Actions));
            Assert.That(rules.ZoneDamage, Is.SameAs(_base.ZoneDamage));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(_base.RespawnAnchors));
            Assert.That(_base.PlayerMaxHealth, Is.EqualTo(GameRules.DefaultPlayerMaxHealth), "the base rules are not changed");
        }

        [Test]
        public void ANestedItemKeepsItsUnwrittenSiblings()
        {
            var headshots = new GameRulesMutator("headshots-only", HeadshotsOnlyJson).Apply(_base);
            Assert.That(headshots.ZoneDamage.Head, Is.EqualTo(DamageCalculator.HeadMultiplier));
            Assert.That(headshots.ZoneDamage.Body, Is.EqualTo(0d));
            Assert.That(headshots.ZoneDamage.Limb, Is.EqualTo(0d));
            Assert.That(headshots.ZoneDamage.WeakPoint, Is.EqualTo(DamageCalculator.WeakPointMultiplier));

            var noGrenades = new GameRulesMutator("no-grenades", "{ \"actions\": { \"grenade\": \"Off\" } }").Apply(_base);
            Assert.That(noGrenades.Actions.Grenade, Is.EqualTo(Grenade.Off));
            Assert.That(noGrenades.Actions.GrenadeCount, Is.EqualTo(2));
            Assert.That(noGrenades.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(noGrenades.Actions.BlindFire, Is.EqualTo(BlindFire.On));
        }

        [Test]
        public void AnEmptyObjectChangesNothing()
        {
            var rules = new GameRulesMutator("nothing", "{}").Apply(_base);

            Assert.That(GameRulesJson.Write(rules), Is.EqualTo(GameRulesJson.Write(_base)));
        }

        [Test]
        public void ApplyAllStacksInOrderSoTheLaterMutatorWins()
        {
            var fifty = new GameRulesMutator("fifty", "{ \"playerMaxHealth\": 50, \"friendlyFire\": \"Reflect\" }");
            var seventy = new GameRulesMutator("seventy", "{ \"playerMaxHealth\": 70 }");

            var fiftyThenSeventy = GameRulesMutator.ApplyAll(_base, new[] { fifty, seventy });
            var seventyThenFifty = GameRulesMutator.ApplyAll(_base, new[] { seventy, fifty });

            Assert.That(fiftyThenSeventy.PlayerMaxHealth, Is.EqualTo(70));
            Assert.That(fiftyThenSeventy.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect), "an item only the earlier one writes stays");
            Assert.That(seventyThenFifty.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(seventyThenFifty.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
        }

        [Test]
        public void ApplyAllWithNoMutatorsReturnsTheBaseRules()
        {
            Assert.That(GameRulesMutator.ApplyAll(_base, Array.Empty<GameRulesMutator>()), Is.SameAs(_base));
        }

        [Test]
        public void ALaterMutatorSeesTheEarlierOnesResult()
        {
            var coverOn = new GameRulesMutator("cover-on", "{ \"actions\": { \"cover\": \"On\" } }");
            var blindFireOn = new GameRulesMutator("blind-fire-on", "{ \"actions\": { \"blindFire\": \"On\" } }");

            var rules = GameRulesMutator.ApplyAll(GameRules.Default, new[] { coverOn, blindFireOn });

            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.Throws<GameRulesJsonException>(() => GameRulesMutator.ApplyAll(GameRules.Default, new[] { blindFireOn, coverOn }));
        }

        [Test]
        public void BrokenJsonIsRejectedWithTheReasonAndTheMutatorId()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => new GameRulesMutator("broken", "{ \"playerMaxHealth\": ").Apply(_base));

            Assert.That(e.Message, Does.Contain("not valid JSON"));
            Assert.That(e.SourceName, Is.EqualTo("mutator 'broken'"));
            Assert.That(e.Item, Is.Null);
        }

        [Test]
        public void AnUnknownItemIsRejectedWithItsName()
        {
            var e = Assert.Throws<GameRulesJsonException>(() => new GameRulesMutator("typo", "{ \"playerHealth\": 50 }", "mutators.json").Apply(_base));

            Assert.That(e.Item, Is.EqualTo("playerHealth"));
            Assert.That(e.SourceName, Is.EqualTo("mutators.json mutator 'typo'"));
        }

        [Test]
        public void AnOutOfRangeValueIsRejectedWithItsItem()
        {
            var health = Assert.Throws<GameRulesJsonException>(() => new GameRulesMutator("zero", "{ \"playerMaxHealth\": 0 }").Apply(_base));
            Assert.That(health.Item, Is.EqualTo("playerMaxHealth"));
            Assert.That(health.Message, Does.Contain("at least 1"));

            var body = Assert.Throws<GameRulesJsonException>(() => new GameRulesMutator("negative", "{ \"zoneDamage\": { \"body\": -1 } }").Apply(_base));
            Assert.That(body.Item, Is.EqualTo("zoneDamage.body"));

            var coverOff = Assert.Throws<GameRulesJsonException>(() => new GameRulesMutator("no-cover", "{ \"actions\": { \"cover\": \"Off\" } }").Apply(_base));
            Assert.That(coverOff.Item, Is.EqualTo("actions.blindFire"), "the base has blind fire on, which needs cover");
        }

        [Test]
        public void TheConstructorRejectsAMissingIdOrJson()
        {
            Assert.That(Assert.Throws<ArgumentException>(() => new GameRulesMutator(" ", "{}")).ParamName, Is.EqualTo("id"));
            Assert.That(Assert.Throws<ArgumentException>(() => new GameRulesMutator(null, "{}")).ParamName, Is.EqualTo("id"));
            Assert.That(Assert.Throws<ArgumentNullException>(() => new GameRulesMutator("id", null)).ParamName, Is.EqualTo("rulesJson"));
            Assert.Throws<ArgumentNullException>(() => new GameRulesMutator("id", "{}").Apply(null));
            Assert.Throws<ArgumentException>(() => GameRulesMutator.ApplyAll(_base, new GameRulesMutator[] { null }));
        }

        [Test]
        public void FindLooksUpByIdExactly()
        {
            var low = new GameRulesMutator("low-health", LowHealthJson);
            var headshots = new GameRulesMutator("headshots-only", HeadshotsOnlyJson);
            var all = new[] { low, headshots };

            Assert.That(GameRulesMutator.Find(all, "headshots-only"), Is.SameAs(headshots));
            Assert.That(GameRulesMutator.Find(all, "Headshots-Only"), Is.Null);
            Assert.That(GameRulesMutator.Find(all, "missing"), Is.Null);
            Assert.That(GameRulesMutator.Find(all, null), Is.Null);
        }

        [Test]
        public void MutatorJsonReadsTheListInOrder()
        {
            const string text = "{ \"mutators\": [\n"
                + "  { \"id\": \"low-health\", \"rules\": { \"playerMaxHealth\": 50 } },\n"
                + "  { \"id\": \"headshots-only\", \"rules\": { \"zoneDamage\": { \"body\": 0, \"limb\": 0.0 } } },\n"
                + "  { \"id\": \"tags\", \"rules\": { \"respawnAnchor\": [\"Squad\", \"Base\"], \"infiniteReserve\": true } }\n"
                + "] }";

            var mutators = MutatorJson.Parse(text, "mutators.json");

            Assert.That(mutators.Select(m => m.Id), Is.EqualTo(new[] { "low-health", "headshots-only", "tags" }));
            Assert.That(mutators[0].SourceName, Is.EqualTo("mutators.json mutator 'low-health'"));
            Assert.That(mutators[1].RulesJson, Is.EqualTo("{\"zoneDamage\": {\"body\": 0, \"limb\": 0.0}}"));
            Assert.That(mutators[2].RulesJson, Is.EqualTo("{\"respawnAnchor\": [\"Squad\", \"Base\"], \"infiniteReserve\": true}"));

            var rules = GameRulesMutator.ApplyAll(GameRules.Default, mutators);
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(0d));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0d));
            Assert.That(rules.RespawnAnchors, Is.EqualTo(new[] { RespawnAnchor.Squad, RespawnAnchor.Base }));
            Assert.That(rules.InfiniteReserve, Is.True);
        }

        [Test]
        public void MutatorJsonWithoutTheListReadsAsNoMutators()
        {
            Assert.That(MutatorJson.Parse("{}", "mutators.json"), Is.Empty);
            Assert.That(MutatorJson.Parse("{ \"mutators\": [] }", "mutators.json"), Is.Empty);
        }

        [Test]
        public void MutatorJsonAcceptsBlindFireWithoutCoverBecauseTheMatchRulesMayHaveCover()
        {
            var mutators = MutatorJson.Parse("{ \"mutators\": [ { \"id\": \"blind\", \"rules\": { \"actions\": { \"blindFire\": \"On\" } } } ] }", "mutators.json");

            Assert.That(mutators[0].Apply(_base).Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.Throws<GameRulesJsonException>(() => mutators[0].Apply(GameRules.Default));
        }

        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": { \"playerMaxHealth\": 0 } } ] }", "mutators[0].rules.playerMaxHealth")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": { \"zoneDamage\": { \"body\": -1 } } } ] }", "mutators[0].rules.zoneDamage.body")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": { \"actions\": { \"jump\": \"Maybe\" } } } ] }", "mutators[0].rules.actions.jump")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": { \"actions\": { \"cover\": \"Off\", \"blindFire\": \"On\" } } } ] }", "mutators[0].rules.actions.blindFire")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": { \"playerHealth\": 50 } } ] }", "mutators[0].rules.playerHealth")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": [] } ] }", "mutators[0].rules")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\" } ] }", "mutators[0].rules")]
        [TestCase("{ \"mutators\": [ { \"rules\": {} } ] }", "mutators[0].id")]
        [TestCase("{ \"mutators\": [ { \"id\": \" \", \"rules\": {} } ] }", "mutators[0].id")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": {}, \"name\": \"A\" } ] }", "mutators[0].name")]
        [TestCase("{ \"mutators\": [ { \"id\": \"a\", \"rules\": {} }, { \"id\": \"a\", \"rules\": {} } ] }", "mutators[1].id")]
        [TestCase("{ \"mutator\": [] }", "mutator")]
        public void MutatorJsonRejectsABrokenEntryWithItsItem(string text, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => MutatorJson.Parse(text, "mutators.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.SourceName, Is.EqualTo("mutators.json"));
            Assert.That(e.Message, Does.Contain("mutators.json"));
        }

        [Test]
        public void MutatorJsonRejectsTextThatIsNotJson()
        {
            var e = Assert.Throws<TuningJsonException>(() => MutatorJson.Parse("{ \"mutators\": [", "mutators.json"));

            Assert.That(e.Message, Does.Contain("not valid JSON"));
            Assert.That(e.Item, Is.Null);
        }

        [Test]
        public void TheLogListsEveryChangedItemOnce()
        {
            var after = GameRulesMutator.ApplyAll(GameRules.Default, new[]
            {
                new GameRulesMutator("low-health", LowHealthJson),
                new GameRulesMutator("headshots-only", HeadshotsOnlyJson),
                new GameRulesMutator("grenades", "{ \"infiniteReserve\": true, \"actions\": { \"grenade\": \"On\", \"grenadeCount\": 2 } }"),
            });

            var changes = MutatorLog.Changes(GameRules.Default, after);

            Assert.That(changes, Is.EqualTo(new[]
            {
                "InfiniteReserve: Off -> On",
                "Grenade: Off -> On",
                "GrenadeCount: 0 -> 2",
                "playerMaxHealth: 100 -> 50",
                "zoneDamage.body: 1 -> 0",
                "zoneDamage.limb: 0.75 -> 0",
            }));
        }

        [Test]
        public void TheAppliedLogNamesTheMutatorAndSaysWhenNothingChanged()
        {
            var low = new GameRulesMutator("low-health", LowHealthJson);

            Assert.That(
                MutatorLog.Applied(low, GameRules.Default, low.Apply(GameRules.Default)),
                Is.EqualTo("[Mutator] applied id=low-health: playerMaxHealth: 100 -> 50"));
            Assert.That(
                MutatorLog.Applied(low, GameRules.Default, GameRules.Default),
                Is.EqualTo("[Mutator] applied id=low-health: no changes"));
        }

        [Test]
        public void TheLogShowsAChangedAnchorListAndNumbers()
        {
            var after = new GameRulesMutator("anchors", "{ \"respawnAnchor\": [\"Squad\", \"Base\"], \"regenPerSecond\": 2.5 }").Apply(GameRules.Default);

            var changes = MutatorLog.Changes(GameRules.Default, after);

            Assert.That(changes, Is.EqualTo(new[] { "regenPerSecond: 0 -> 2.5", "respawnAnchor: [Base] -> [Squad, Base]" }));
        }

        [Test]
        public void TheSkippedAndUnknownLogsGiveTheReason()
        {
            var broken = new GameRulesMutator("broken", "{ \"playerMaxHealth\": 0 }");
            var reason = Assert.Throws<GameRulesJsonException>(() => broken.Apply(GameRules.Default));

            Assert.That(MutatorLog.Skipped(broken, reason), Is.EqualTo("[Mutator] skipped id=broken: " + reason.Message));
            Assert.That(
                MutatorLog.UnknownId("missing", new[] { broken, new GameRulesMutator("low-health", LowHealthJson) }),
                Is.EqualTo("[Mutator] skipped id=missing: no mutator has this id; known ids are broken, low-health"));
            Assert.That(
                MutatorLog.UnknownId("missing", Array.Empty<GameRulesMutator>()),
                Is.EqualTo("[Mutator] skipped id=missing: no mutator has this id; known ids are (none)"));
        }

        // MutatorLog は Changes が出す欄を、デバッグの欄の名前の頭を小文字にした JSON の項目名で外すので、その対応が崩れていないかを見る。
        [Test]
        public void EveryDebugFieldNamesAGameRulesJsonItem()
        {
            var items = GameRulesJson.ItemNames.Concat(GameRulesJson.ActionItemNames).ToList();
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                var name = field.ToString();
                Assert.That(items, Has.Member(char.ToLowerInvariant(name[0]) + name.Substring(1)), name);
            }
        }
    }
}
