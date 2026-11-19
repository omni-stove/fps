using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GameRulesOverrideTests
    {
        private static readonly GameRulesMutator _lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
        private static readonly GameRulesMutator _seventy = new GameRulesMutator("seventy", "{ \"playerMaxHealth\": 70 }");
        private static readonly GameRulesMutator _headshots =
            new GameRulesMutator("headshots-only", "{ \"zoneDamage\": { \"body\": 0, \"limb\": 0 } }");

        [SetUp]
        public void SetUp()
        {
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
        }

        [TearDown]
        public void TearDown()
        {
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
        }

        [Test]
        public void NothingIsStackedAtFirst()
        {
            Assert.That(GameRulesOverride.Current, Is.Null);
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
        }

        [Test]
        public void PushedMutatorsAreKeptInOrder()
        {
            GameRulesOverride.PushMutator(_lowHealth);
            GameRulesOverride.PushMutator(_headshots);

            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth, _headshots }));
        }

        [Test]
        public void TheExposedListCannotBeChangedFromOutside()
        {
            GameRulesOverride.PushMutator(_lowHealth);

            var list = (System.Collections.Generic.IList<GameRulesMutator>)GameRulesOverride.Mutators;

            Assert.Throws<NotSupportedException>(() => list.Add(_headshots));
            Assert.That(GameRulesOverride.Mutators.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearKeepsTheMutatorsAndClearMutatorsKeepsTheRules()
        {
            GameRulesOverride.Set(GameRules.Default);
            GameRulesOverride.PushMutator(_lowHealth);

            GameRulesOverride.Clear();
            Assert.That(GameRulesOverride.Current, Is.Null);
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _lowHealth }), "the debug panel's reset only drops its own rules");

            GameRulesOverride.Set(GameRules.Default);
            GameRulesOverride.ClearMutators();
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
            Assert.That(GameRulesOverride.Current, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void PushRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesOverride.PushMutator((GameRulesMutator)null));
            Assert.Throws<ArgumentNullException>(() => GameRulesOverride.PushMutator((MutatorDefinition)null));
            Assert.Throws<ArgumentNullException>(() => GameRulesOverride.PushMutator("low-health", null));
            Assert.That(GameRulesOverride.Mutators, Is.Empty);
        }

        [Test]
        public void PushByIdFindsTheMutatorInTheList()
        {
            var available = new[] { _lowHealth, _headshots };

            var pushed = GameRulesOverride.PushMutator("headshots-only", available);

            Assert.That(pushed, Is.SameAs(_headshots));
            Assert.That(GameRulesOverride.Mutators, Is.EqualTo(new[] { _headshots }));
        }

        [Test]
        public void PushByAnUnknownIdThrowsAndListsTheKnownIds()
        {
            var e = Assert.Throws<ArgumentException>(() => GameRulesOverride.PushMutator("missing", new[] { _lowHealth, _headshots }));

            Assert.That(e.ParamName, Is.EqualTo("id"));
            Assert.That(e.Message, Does.Contain("'missing'").And.Contain("low-health, headshots-only"));
            Assert.That(GameRulesOverride.Mutators, Is.Empty);

            var none = Assert.Throws<ArgumentException>(() => GameRulesOverride.PushMutator("missing", Array.Empty<GameRulesMutator>()));
            Assert.That(none.Message, Does.Contain("(none)"));
        }

        [Test]
        public void PushADefinitionStacksItsRules()
        {
            var definition = ScriptableObject.CreateInstance<MutatorDefinition>();
            try
            {
                definition.name = "LowHealth";
                definition.Id = "low-health";
                definition.RulesJson = "{ \"playerMaxHealth\": 50 }";

                GameRulesOverride.PushMutator(definition);

                var pushed = GameRulesOverride.Mutators.Single();
                Assert.That(pushed.Id, Is.EqualTo("low-health"));
                Assert.That(pushed.SourceName, Is.EqualTo("MutatorDefinition 'LowHealth' mutator 'low-health'"));
                Assert.That(pushed.Apply(GameRules.Default).PlayerMaxHealth, Is.EqualTo(50));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ADefinitionWithoutAnIdCannotBePushed()
        {
            var definition = ScriptableObject.CreateInstance<MutatorDefinition>();
            try
            {
                Assert.That(definition.RulesJson, Is.EqualTo("{}"));
                Assert.Throws<ArgumentException>(() => GameRulesOverride.PushMutator(definition));
                Assert.That(GameRulesOverride.Mutators, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ApplyWithNothingStackedReturnsTheBaseRulesWithoutLogging()
        {
            var baseRules = GameRules.Default.WithRegenPerSecond(5f);

            Assert.That(GameRulesOverride.ApplyMutators(baseRules), Is.SameAs(baseRules));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ApplyStacksInOrderAndLogsEachChange()
        {
            var baseRules = GameRules.Default.WithRegenPerSecond(5f);
            GameRulesOverride.PushMutator(_lowHealth);
            GameRulesOverride.PushMutator(_seventy);

            var afterLow = _lowHealth.Apply(baseRules);
            LogAssert.Expect(LogType.Log, "[Mutator] applied id=low-health: playerMaxHealth: 100 -> 50");
            LogAssert.Expect(LogType.Log, "[Mutator] applied id=seventy: playerMaxHealth: 50 -> 70");
            var rules = GameRulesOverride.ApplyMutators(baseRules);

            Assert.That(afterLow.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(70), "the later mutator wins");
            Assert.That(rules.RegenPerSecond, Is.EqualTo(5f), "an item no mutator writes keeps the base value");
            Assert.That(GameRulesOverride.Mutators.Count, Is.EqualTo(2), "applying does not use the stack up");
        }

        [Test]
        public void ApplySkipsAMutatorThatDoesNotFitAndKeepsGoing()
        {
            var blindFire = new GameRulesMutator("blind-fire", "{ \"actions\": { \"blindFire\": \"On\" } }");
            var reason = Assert.Throws<GameRulesJsonException>(() => blindFire.Apply(GameRules.Default));

            LogAssert.Expect(LogType.Error, MutatorLog.Skipped(blindFire, reason));
            LogAssert.Expect(LogType.Log, "[Mutator] applied id=low-health: playerMaxHealth: 100 -> 50");
            var rules = GameRulesOverride.ApplyMutators(GameRules.Default, new[] { blindFire, _lowHealth });

            Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.Off));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(50));
        }

        [Test]
        public void ApplyRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesOverride.ApplyMutators(null));
            Assert.Throws<ArgumentNullException>(() => GameRulesOverride.ApplyMutators(GameRules.Default, null));
            Assert.Throws<ArgumentException>(() => GameRulesOverride.ApplyMutators(GameRules.Default, new GameRulesMutator[] { null }));
        }

        [Test]
        public void TheBundledMutatorsFileReadsAndHasTheExamples()
        {
            var asset = Resources.Load<TextAsset>(KitTuning.MutatorsResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/" + KitTuning.MutatorsResourcePath + ".json is bundled");

            var mutators = MutatorJson.Parse(asset.text, "Resources/" + KitTuning.MutatorsResourcePath + ".json");
            Resources.UnloadAsset(asset);

            Assert.That(mutators.Select(m => m.Id), Is.EqualTo(new[] { "low-health", "headshots-only", "no-regen", "infinite-ammo" }));
            var rules = GameRulesMutator.ApplyAll(GameRules.Default, mutators);
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(50));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(0d));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0d));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(GameRules.Default.ZoneDamage.Head));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f));
            Assert.That(rules.InfiniteReserve, Is.True);
        }

        [Test]
        public void TheResourcePathsAgree()
        {
            Assert.That(MutatorJson.ResourcePath, Is.EqualTo(KitTuning.MutatorsResourcePath));
        }
    }
}
