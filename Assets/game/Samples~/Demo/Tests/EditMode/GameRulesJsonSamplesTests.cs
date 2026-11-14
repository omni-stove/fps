using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// サンプルのルールの JSON (<see cref="SamplesFolder"/>) を一時フォルダに取り込むと、<see cref="GameRulesAssetBuilder"/> のルールと同じ値になることを確かめる。
    /// </summary>
    public sealed class GameRulesJsonSamplesTests
    {
        private static readonly Dictionary<string, Func<GameRules>> _samples = new Dictionary<string, Func<GameRules>>
        {
            { "TeamDeathmatch", GameRulesAssetBuilder.TeamDeathmatchRules },
            { "CampaignEasy", GameRulesAssetBuilder.CampaignEasyRules },
            { "CampaignNormal", GameRulesAssetBuilder.CampaignNormalRules },
            { "CampaignHard", GameRulesAssetBuilder.CampaignHardRules },
            { "Range", GameRulesAssetBuilder.RangeRules },
            { "TpsRange", GameRulesAssetBuilder.TpsRangeRules },
            { "Domination", GameRulesAssetBuilder.DominationRules },
        };

        private string _folder;

        private static string SamplesFolder => DemoSample.Root + "/Rules/Json";

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__GameRulesJsonSamplesTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void ImportingTheSamplesGivesTheBuilderRules()
        {
            var built = GameRulesJsonImporter.ImportFrom(SamplesFolder, _folder);

            var names = new List<string>();
            foreach (var definition in built)
            {
                names.Add(definition.name);
                Assert.That(_samples.ContainsKey(definition.name), Is.True, $"unexpected sample '{definition.name}'");
                Assert.That(AssetDatabase.GetAssetPath(definition), Is.EqualTo(_folder + "/" + definition.name + ".asset"));
                AssertSameRules(definition.ToRules(), _samples[definition.name](), definition.name);
            }

            Assert.That(names, Is.EquivalentTo(_samples.Keys));
        }

        [Test]
        public void ImportingTheSamplesFillsTheAssetLikeTheBuilder()
        {
            var imported = GameRulesJsonImporter.ImportFrom(SamplesFolder + "/TpsRange.json", _folder)[0];
            var built = ScriptableObject.CreateInstance<GameRulesDefinition>();
            built.name = imported.name;
            try
            {
                GameRulesAssetBuilder.Configure(built, GameRulesAssetBuilder.TpsRangeRules());

                Assert.That(EditorJsonUtility.ToJson(imported), Is.EqualTo(EditorJsonUtility.ToJson(built)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(built);
            }
        }

        private static void AssertSameRules(GameRules actual, GameRules expected, string name)
        {
            Assert.That(actual.FriendlyFire, Is.EqualTo(expected.FriendlyFire), name + " friendlyFire");
            Assert.That(actual.RespawnMode, Is.EqualTo(expected.RespawnMode), name + " respawnMode");
            Assert.That(actual.WaveIntervalSeconds, Is.EqualTo(expected.WaveIntervalSeconds), name + " waveIntervalSeconds");
            Assert.That(actual.RegenDelaySeconds, Is.EqualTo(expected.RegenDelaySeconds), name + " regenDelaySeconds");
            Assert.That(actual.RegenPerSecond, Is.EqualTo(expected.RegenPerSecond), name + " regenPerSecond");
            Assert.That(actual.DeathBody, Is.EqualTo(expected.DeathBody), name + " deathBody");
            Assert.That(actual.DeathCamera, Is.EqualTo(expected.DeathCamera), name + " deathCamera");
            Assert.That(actual.InfiniteReserve, Is.EqualTo(expected.InfiniteReserve), name + " infiniteReserve");
            Assert.That(actual.ReserveAmmoMultiplier, Is.EqualTo(expected.ReserveAmmoMultiplier), name + " reserveAmmoMultiplier");
            Assert.That(actual.Destruction, Is.EqualTo(expected.Destruction), name + " destruction");
            Assert.That(actual.SpawnProtectionSeconds, Is.EqualTo(expected.SpawnProtectionSeconds), name + " spawnProtectionSeconds");
            Assert.That(actual.FallDamage, Is.EqualTo(expected.FallDamage), name + " fallDamage");
            Assert.That(actual.HudDensity, Is.EqualTo(expected.HudDensity), name + " hudDensity");
            Assert.That(actual.Objective, Is.EqualTo(expected.Objective), name + " objective");
            Assert.That(actual.MinPlayers, Is.EqualTo(expected.MinPlayers), name + " minPlayers");
            Assert.That(actual.IdleKickSeconds, Is.EqualTo(expected.IdleKickSeconds), name + " idleKickSeconds");

            var a = actual.Actions;
            var b = expected.Actions;
            Assert.That(a.Downed, Is.EqualTo(b.Downed), name + " actions.downed");
            Assert.That(a.DownedBleedoutSeconds, Is.EqualTo(b.DownedBleedoutSeconds), name + " actions.downedBleedoutSeconds");
            Assert.That(a.DownedHealthFraction, Is.EqualTo(b.DownedHealthFraction), name + " actions.downedHealthFraction");
            Assert.That(a.ReviveSeconds, Is.EqualTo(b.ReviveSeconds), name + " actions.reviveSeconds");
            Assert.That(a.Cover, Is.EqualTo(b.Cover), name + " actions.cover");
            Assert.That(a.BlindFire, Is.EqualTo(b.BlindFire), name + " actions.blindFire");
            Assert.That(a.ActiveReload, Is.EqualTo(b.ActiveReload), name + " actions.activeReload");
            Assert.That(a.Dodge, Is.EqualTo(b.Dodge), name + " actions.dodge");
            Assert.That(a.Vault, Is.EqualTo(b.Vault), name + " actions.vault");
            Assert.That(a.Grenade, Is.EqualTo(b.Grenade), name + " actions.grenade");
            Assert.That(a.GrenadeCount, Is.EqualTo(b.GrenadeCount), name + " actions.grenadeCount");
            Assert.That(a.Melee, Is.EqualTo(b.Melee), name + " actions.melee");
            Assert.That(a.Stagger, Is.EqualTo(b.Stagger), name + " actions.stagger");
            Assert.That(a.Slide, Is.EqualTo(b.Slide), name + " actions.slide");
            Assert.That(a.Mantle, Is.EqualTo(b.Mantle), name + " actions.mantle");
            Assert.That(a.Prone, Is.EqualTo(b.Prone), name + " actions.prone");
            Assert.That(a.Ladder, Is.EqualTo(b.Ladder), name + " actions.ladder");
            Assert.That(a.Zipline, Is.EqualTo(b.Zipline), name + " actions.zipline");
            Assert.That(a.Parachute, Is.EqualTo(b.Parachute), name + " actions.parachute");
            Assert.That(a.Stealth, Is.EqualTo(b.Stealth), name + " actions.stealth");
            Assert.That(a.Suppression, Is.EqualTo(b.Suppression), name + " actions.suppression");
            Assert.That(a.Ping, Is.EqualTo(b.Ping), name + " actions.ping");
            Assert.That(a.Abilities, Is.EqualTo(b.Abilities), name + " actions.abilities");
            Assert.That(a.SlowMotion, Is.EqualTo(b.SlowMotion), name + " actions.slowMotion");
        }
    }
}
