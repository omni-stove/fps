using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class GameRulesDefinitionTests
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
        public void DefaultsMatchGameRulesDefault()
        {
            var rules = NewDefinition("Default").ToRules();
            var expected = GameRules.Default;

            AssertSameRules(rules, expected);
        }

        [Test]
        public void ToRulesCopiesEveryField()
        {
            var definition = NewDefinition("Custom");
            definition.FriendlyFire = FriendlyFire.Reflect;
            definition.RespawnMode = RespawnMode.Wave;
            definition.WaveIntervalSeconds = 15f;
            definition.RegenDelaySeconds = 4f;
            definition.RegenPerSecond = 20f;
            definition.DeathBody = DeathBody.Collapse;
            definition.DeathCamera = DeathCamera.Killer;
            definition.InfiniteReserve = true;
            definition.ReserveAmmoMultiplier = 0.6f;
            definition.Destruction = Destruction.On;
            definition.Downed = Downed.Revive;
            definition.DownedBleedoutSeconds = 20f;
            definition.DownedHealthFraction = 0.5f;
            definition.ReviveSeconds = 4f;
            definition.Cover = Cover.On;
            definition.BlindFire = BlindFire.On;
            definition.ActiveReload = ActiveReload.On;
            definition.Dodge = Dodge.On;
            definition.Vault = Vault.On;
            definition.Grenade = Grenade.On;
            definition.GrenadeCount = 2;
            definition.Melee = Melee.On;
            definition.Stagger = Stagger.On;
            definition.Slide = Slide.On;
            definition.Mantle = Mantle.On;
            definition.Prone = Prone.On;
            definition.Ladder = Ladder.On;
            definition.Zipline = Zipline.On;
            definition.Parachute = Parachute.On;
            definition.Stealth = Stealth.On;
            definition.Suppression = Suppression.On;
            definition.Ping = Fps.Core.Ping.On;
            definition.Abilities = Abilities.On;
            definition.SlowMotion = SlowMotion.On;
            definition.SpawnProtectionSeconds = 1.5f;
            definition.FallDamage = FallDamage.On;
            definition.HudDensity = HudDensity.Minimal;
            definition.Objective = MatchObjective.Domination;
            definition.MinPlayers = 6;
            definition.IdleKickSeconds = 45f;

            var rules = definition.ToRules();

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(15f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(0.6f));
            Assert.That(rules.Destruction, Is.EqualTo(Destruction.On));
            Assert.That(rules.Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(rules.Actions.DownedBleedoutSeconds, Is.EqualTo(20f));
            Assert.That(rules.Actions.DownedHealthFraction, Is.EqualTo(0.5f));
            Assert.That(rules.Actions.ReviveSeconds, Is.EqualTo(4f));
            Assert.That(rules.Actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(rules.Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(rules.Actions.ActiveReload, Is.EqualTo(ActiveReload.On));
            Assert.That(rules.Actions.Dodge, Is.EqualTo(Dodge.On));
            Assert.That(rules.Actions.Vault, Is.EqualTo(Vault.On));
            Assert.That(rules.Actions.Grenade, Is.EqualTo(Grenade.On));
            Assert.That(rules.Actions.GrenadeCount, Is.EqualTo(2));
            Assert.That(rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(rules.Actions.Stagger, Is.EqualTo(Stagger.On));
            Assert.That(rules.Actions.Slide, Is.EqualTo(Slide.On));
            Assert.That(rules.Actions.Mantle, Is.EqualTo(Mantle.On));
            Assert.That(rules.Actions.Prone, Is.EqualTo(Prone.On));
            Assert.That(rules.Actions.Ladder, Is.EqualTo(Ladder.On));
            Assert.That(rules.Actions.Zipline, Is.EqualTo(Zipline.On));
            Assert.That(rules.Actions.Parachute, Is.EqualTo(Parachute.On));
            Assert.That(rules.Actions.Stealth, Is.EqualTo(Stealth.On));
            Assert.That(rules.Actions.Suppression, Is.EqualTo(Suppression.On));
            Assert.That(rules.Actions.Ping, Is.EqualTo(Fps.Core.Ping.On));
            Assert.That(rules.Actions.Abilities, Is.EqualTo(Abilities.On));
            Assert.That(rules.Actions.SlowMotion, Is.EqualTo(SlowMotion.On));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(1.5f));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.On));
            Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Minimal));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(rules.MinPlayers, Is.EqualTo(6));
            Assert.That(rules.IdleKickSeconds, Is.EqualTo(45f));
            Assert.That(definition.ToRules(), Is.Not.SameAs(rules));
        }

        [Test]
        public void CopyFromRoundTripsThroughToRules()
        {
            var source = new GameRules(
                FriendlyFire.On,
                RespawnMode.None,
                7f,
                2f,
                30f,
                DeathBody.Collapse,
                DeathCamera.Killer,
                true,
                2f,
                Destruction.On,
                new ActionRules(
                    Downed.Revive,
                    12f,
                    0.25f,
                    2.5f,
                    Cover.On,
                    BlindFire.On,
                    ActiveReload.On,
                    Dodge.On,
                    Vault.On,
                    Grenade.On,
                    3,
                    Melee.On,
                    Stagger.On,
                    Slide.On,
                    Mantle.On,
                    Prone.On,
                    Ladder.On,
                    Zipline.On,
                    Parachute.On,
                    Stealth.On,
                    Suppression.On,
                    Fps.Core.Ping.On,
                    Abilities.On,
                    SlowMotion.On),
                2f,
                FallDamage.On,
                HudDensity.Hardcore,
                MatchObjective.Domination,
                8,
                120f);
            var definition = NewDefinition("Copied");

            definition.CopyFrom(source);

            AssertSameRules(definition.ToRules(), source);
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [TestCase("FriendlyFire")]
        [TestCase("RespawnMode")]
        [TestCase("WaveIntervalSeconds")]
        [TestCase("RegenDelaySeconds")]
        [TestCase("RegenPerSecond")]
        [TestCase("DeathBody")]
        [TestCase("DeathCamera")]
        [TestCase("ReserveAmmoMultiplier")]
        [TestCase("Destruction")]
        [TestCase("Downed")]
        [TestCase("DownedBleedoutSeconds")]
        [TestCase("DownedHealthFraction")]
        [TestCase("ReviveSeconds")]
        [TestCase("Cover")]
        [TestCase("BlindFire")]
        [TestCase("BlindFireWithoutCover")]
        [TestCase("ActiveReload")]
        [TestCase("Dodge")]
        [TestCase("Vault")]
        [TestCase("Grenade")]
        [TestCase("GrenadeCount")]
        [TestCase("Melee")]
        [TestCase("Stagger")]
        [TestCase("Slide")]
        [TestCase("Mantle")]
        [TestCase("Prone")]
        [TestCase("Ladder")]
        [TestCase("Zipline")]
        [TestCase("Parachute")]
        [TestCase("Stealth")]
        [TestCase("Suppression")]
        [TestCase("Ping")]
        [TestCase("Abilities")]
        [TestCase("SlowMotion")]
        [TestCase("SpawnProtectionSeconds")]
        [TestCase("FallDamage")]
        [TestCase("HudDensity")]
        [TestCase("Objective")]
        [TestCase("MinPlayers")]
        [TestCase("IdleKickSeconds")]
        public void ToRulesRejectsInvalidValuesWithTheAssetName(string field)
        {
            var definition = NewBroken("BrokenRules", field);

            var e = Assert.Throws<GameRulesDefinitionException>(() => definition.ToRules());
            Assert.That(e.Message, Does.Contain("BrokenRules"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [TestCase("FriendlyFire")]
        [TestCase("WaveIntervalSeconds")]
        [TestCase("RegenPerSecond")]
        [TestCase("ReserveAmmoMultiplier")]
        public void TryToRulesReportsInvalidValuesWithoutThrowing(string field)
        {
            var definition = NewBroken("BrokenRules", field);

            Assert.That(definition.TryToRules(out var rules, out var error), Is.False);
            Assert.That(rules, Is.Null);
            Assert.That(error, Does.Contain("BrokenRules"));
        }

        [Test]
        public void TryToRulesReturnsTheRulesWhenValid()
        {
            var definition = NewDefinition("Valid");
            definition.RegenPerSecond = 10f;

            Assert.That(definition.TryToRules(out var rules, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.RegenPerSecond, Is.EqualTo(10f));
        }

        [Test]
        public void AnUnnamedDefinitionStillNamesItselfInTheError()
        {
            var definition = NewDefinition(string.Empty);
            definition.ReserveAmmoMultiplier = -1f;

            var e = Assert.Throws<GameRulesDefinitionException>(() => definition.ToRules());
            Assert.That(e.Message, Does.Contain("(unnamed game rules)"));
        }

        private static void AssertSameRules(GameRules actual, GameRules expected)
        {
            Assert.That(actual.FriendlyFire, Is.EqualTo(expected.FriendlyFire));
            Assert.That(actual.RespawnMode, Is.EqualTo(expected.RespawnMode));
            Assert.That(actual.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds));
            Assert.That(actual.RegenDelaySeconds, Is.EqualTo(expected.RegenDelaySeconds));
            Assert.That(actual.RegenPerSecond, Is.EqualTo(expected.RegenPerSecond));
            Assert.That(actual.DeathBody, Is.EqualTo(expected.DeathBody));
            Assert.That(actual.DeathCamera, Is.EqualTo(expected.DeathCamera));
            Assert.That(actual.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve));
            Assert.That(actual.ReserveAmmoMultiplier, Is.EqualTo(expected.ReserveAmmoMultiplier));
            Assert.That(actual.Destruction, Is.EqualTo(expected.Destruction));
            Assert.That(actual.Actions.Downed, Is.EqualTo(expected.Actions.Downed));
            Assert.That(actual.Actions.DownedBleedoutSeconds, Is.EqualTo(expected.Actions.DownedBleedoutSeconds));
            Assert.That(actual.Actions.DownedHealthFraction, Is.EqualTo(expected.Actions.DownedHealthFraction));
            Assert.That(actual.Actions.ReviveSeconds, Is.EqualTo(expected.Actions.ReviveSeconds));
            Assert.That(actual.Actions.Cover, Is.EqualTo(expected.Actions.Cover));
            Assert.That(actual.Actions.BlindFire, Is.EqualTo(expected.Actions.BlindFire));
            Assert.That(actual.Actions.ActiveReload, Is.EqualTo(expected.Actions.ActiveReload));
            Assert.That(actual.Actions.Dodge, Is.EqualTo(expected.Actions.Dodge));
            Assert.That(actual.Actions.Vault, Is.EqualTo(expected.Actions.Vault));
            Assert.That(actual.Actions.Grenade, Is.EqualTo(expected.Actions.Grenade));
            Assert.That(actual.Actions.GrenadeCount, Is.EqualTo(expected.Actions.GrenadeCount));
            Assert.That(actual.Actions.Melee, Is.EqualTo(expected.Actions.Melee));
            Assert.That(actual.Actions.Stagger, Is.EqualTo(expected.Actions.Stagger));
            Assert.That(actual.Actions.Slide, Is.EqualTo(expected.Actions.Slide));
            Assert.That(actual.Actions.Mantle, Is.EqualTo(expected.Actions.Mantle));
            Assert.That(actual.Actions.Prone, Is.EqualTo(expected.Actions.Prone));
            Assert.That(actual.Actions.Ladder, Is.EqualTo(expected.Actions.Ladder));
            Assert.That(actual.Actions.Zipline, Is.EqualTo(expected.Actions.Zipline));
            Assert.That(actual.Actions.Parachute, Is.EqualTo(expected.Actions.Parachute));
            Assert.That(actual.Actions.Stealth, Is.EqualTo(expected.Actions.Stealth));
            Assert.That(actual.Actions.Suppression, Is.EqualTo(expected.Actions.Suppression));
            Assert.That(actual.Actions.Ping, Is.EqualTo(expected.Actions.Ping));
            Assert.That(actual.Actions.Abilities, Is.EqualTo(expected.Actions.Abilities));
            Assert.That(actual.Actions.SlowMotion, Is.EqualTo(expected.Actions.SlowMotion));
            Assert.That(actual.SpawnProtectionSeconds, Is.EqualTo(expected.SpawnProtectionSeconds));
            Assert.That(actual.FallDamage, Is.EqualTo(expected.FallDamage));
            Assert.That(actual.HudDensity, Is.EqualTo(expected.HudDensity));
            Assert.That(actual.Objective, Is.EqualTo(expected.Objective));
            Assert.That(actual.MinPlayers, Is.EqualTo(expected.MinPlayers));
            Assert.That(actual.IdleKickSeconds, Is.EqualTo(expected.IdleKickSeconds));
        }

        private GameRulesDefinition NewBroken(string assetName, string field)
        {
            var definition = NewDefinition(assetName);
            switch (field)
            {
                case "FriendlyFire":
                    definition.FriendlyFire = (FriendlyFire)99;
                    break;
                case "RespawnMode":
                    definition.RespawnMode = (RespawnMode)99;
                    break;
                case "WaveIntervalSeconds":
                    definition.WaveIntervalSeconds = 0f;
                    break;
                case "RegenDelaySeconds":
                    definition.RegenDelaySeconds = float.NaN;
                    break;
                case "RegenPerSecond":
                    definition.RegenPerSecond = float.PositiveInfinity;
                    break;
                case "DeathBody":
                    definition.DeathBody = (DeathBody)99;
                    break;
                case "DeathCamera":
                    definition.DeathCamera = (DeathCamera)99;
                    break;
                case "ReserveAmmoMultiplier":
                    definition.ReserveAmmoMultiplier = -0.1f;
                    break;
                case "Destruction":
                    definition.Destruction = (Destruction)99;
                    break;
                case "Downed":
                    definition.Downed = (Downed)99;
                    break;
                case "DownedBleedoutSeconds":
                    definition.DownedBleedoutSeconds = 0f;
                    break;
                case "DownedHealthFraction":
                    definition.DownedHealthFraction = 1.5f;
                    break;
                case "ReviveSeconds":
                    definition.ReviveSeconds = float.PositiveInfinity;
                    break;
                case "Cover":
                    definition.Cover = (Cover)99;
                    break;
                case "BlindFire":
                    definition.Cover = Cover.On;
                    definition.BlindFire = (BlindFire)99;
                    break;
                case "BlindFireWithoutCover":
                    definition.Cover = Cover.Off;
                    definition.BlindFire = BlindFire.On;
                    break;
                case "ActiveReload":
                    definition.ActiveReload = (ActiveReload)99;
                    break;
                case "Dodge":
                    definition.Dodge = (Dodge)99;
                    break;
                case "Vault":
                    definition.Vault = (Vault)99;
                    break;
                case "Grenade":
                    definition.Grenade = (Grenade)99;
                    break;
                case "GrenadeCount":
                    definition.GrenadeCount = -1;
                    break;
                case "Melee":
                    definition.Melee = (Melee)99;
                    break;
                case "Stagger":
                    definition.Stagger = (Stagger)99;
                    break;
                case "Slide":
                    definition.Slide = (Slide)99;
                    break;
                case "Mantle":
                    definition.Mantle = (Mantle)99;
                    break;
                case "Prone":
                    definition.Prone = (Prone)99;
                    break;
                case "Ladder":
                    definition.Ladder = (Ladder)99;
                    break;
                case "Zipline":
                    definition.Zipline = (Zipline)99;
                    break;
                case "Parachute":
                    definition.Parachute = (Parachute)99;
                    break;
                case "Stealth":
                    definition.Stealth = (Stealth)99;
                    break;
                case "Suppression":
                    definition.Suppression = (Suppression)99;
                    break;
                case "Ping":
                    definition.Ping = (Fps.Core.Ping)99;
                    break;
                case "Abilities":
                    definition.Abilities = (Abilities)99;
                    break;
                case "SlowMotion":
                    definition.SlowMotion = (SlowMotion)99;
                    break;
                case "SpawnProtectionSeconds":
                    definition.SpawnProtectionSeconds = float.NaN;
                    break;
                case "FallDamage":
                    definition.FallDamage = (FallDamage)99;
                    break;
                case "HudDensity":
                    definition.HudDensity = (HudDensity)99;
                    break;
                case "Objective":
                    definition.Objective = (MatchObjective)99;
                    break;
                case "MinPlayers":
                    definition.MinPlayers = -1;
                    break;
                case "IdleKickSeconds":
                    definition.IdleKickSeconds = float.NegativeInfinity;
                    break;
                default:
                    Assert.Fail("unknown field " + field);
                    break;
            }

            return definition;
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
