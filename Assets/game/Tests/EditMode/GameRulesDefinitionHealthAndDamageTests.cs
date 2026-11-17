using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GameRulesDefinitionHealthAndDamageTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheDefaultPlayerMaxHealthIsTheDamageableDefault()
        {
            Assert.That(GameRules.DefaultPlayerMaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
        }

        [Test]
        public void ANewDefinitionHasTheDefaults()
        {
            var definition = NewDefinition("Default");

            Assert.That(definition.ReviveRange, Is.EqualTo(ReviveReach.DefaultRange));
            Assert.That(definition.PlayerMaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(definition.HeadDamageMultiplier, Is.EqualTo(ZoneDamageMultipliers.Default.Head));
            Assert.That(definition.BodyDamageMultiplier, Is.EqualTo(ZoneDamageMultipliers.Default.Body));
            Assert.That(definition.LimbDamageMultiplier, Is.EqualTo(ZoneDamageMultipliers.Default.Limb));

            var rules = definition.ToRules();
            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(1.5f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(100));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(2.0));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(1.0));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.75));
            Assert.That(rules.ZoneDamage.WeakPoint, Is.EqualTo(2.0));
        }

        [Test]
        public void ToRulesPassesTheFields()
        {
            var definition = NewDefinition("Custom");
            definition.ReviveRange = 2.5f;
            definition.PlayerMaxHealth = 150;
            definition.HeadDamageMultiplier = 1.4;
            definition.BodyDamageMultiplier = 1.1;
            definition.LimbDamageMultiplier = 0.9;

            var rules = definition.ToRules();

            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(2.5f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(150));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(1.4));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(1.1));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.9));
            Assert.That(rules.ZoneDamage.WeakPoint, Is.EqualTo(ZoneDamageMultipliers.Default.WeakPoint));
        }

        [Test]
        public void CopyFromThenToRulesKeepsTheValues()
        {
            var source = GameRulesJson.Parse(
                "{\"actions\": {\"reviveRange\": 2.5}, \"playerMaxHealth\": 120, \"zoneDamage\": {\"head\": 1.7, \"body\": 0.95, \"limb\": 0.6}}",
                "Rules/Copy.json");
            var definition = NewDefinition("Copied");

            definition.CopyFrom(source);

            Assert.That(definition.ReviveRange, Is.EqualTo(2.5f));
            Assert.That(definition.PlayerMaxHealth, Is.EqualTo(120));
            Assert.That(definition.HeadDamageMultiplier, Is.EqualTo(1.7));
            Assert.That(definition.BodyDamageMultiplier, Is.EqualTo(0.95));
            Assert.That(definition.LimbDamageMultiplier, Is.EqualTo(0.6));

            var rules = definition.ToRules();
            Assert.That(rules.Actions.ReviveRange, Is.EqualTo(2.5f));
            Assert.That(rules.PlayerMaxHealth, Is.EqualTo(120));
            Assert.That(rules.ZoneDamage.Head, Is.EqualTo(1.7));
            Assert.That(rules.ZoneDamage.Body, Is.EqualTo(0.95));
            Assert.That(rules.ZoneDamage.Limb, Is.EqualTo(0.6));
        }

        [TestCase("reviveRange")]
        [TestCase("playerMaxHealth")]
        [TestCase("head")]
        [TestCase("body")]
        [TestCase("limb")]
        public void InvalidFieldIsRejectedWithTheAssetName(string item)
        {
            var definition = NewDefinition("Broken");
            switch (item)
            {
                case "reviveRange":
                    definition.ReviveRange = 0f;
                    break;
                case "playerMaxHealth":
                    definition.PlayerMaxHealth = 0;
                    break;
                case "head":
                    definition.HeadDamageMultiplier = -1.0;
                    break;
                case "body":
                    definition.BodyDamageMultiplier = double.NaN;
                    break;
                default:
                    definition.LimbDamageMultiplier = double.PositiveInfinity;
                    break;
            }

            var e = Assert.Throws<GameRulesDefinitionException>(() => definition.ToRules());

            Assert.That(e.Message, Does.Contain("Broken"));
            Assert.That(e.Message, Does.Contain(item));
        }

        private GameRulesDefinition NewDefinition(string assetName)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = assetName;
            _created.Add(definition);
            return definition;
        }
    }
}
