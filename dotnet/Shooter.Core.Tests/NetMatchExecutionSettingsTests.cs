using System;
using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchExecutionSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings, MatchObjective objective = MatchObjective.None)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                objective.ToString(),
                GameRules.Default.WithObjective(objective),
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match)
        {
            return "{\"rules\": {\"objective\": \"Execution\"}, \"match\": " + match
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultMatchesTheOfflineDefaults()
        {
            Assert.That(NetMatchSettings.Default.Execution, Is.SameAs(ExecutionSpec.Default));
            Assert.That(new NetMatchSettings(execution: null).Execution, Is.SameAs(ExecutionSpec.Default));
            Assert.That(ExecutionSpec.Default.ExecuteSeconds, Is.EqualTo(ExecutionSession.DefaultExecuteSeconds));
            Assert.That(ExecutionSpec.Default.ExecuteRange, Is.EqualTo(2f));
        }

        [TestCase(0f, 2f, "executeSeconds")]
        [TestCase(float.PositiveInfinity, 2f, "executeSeconds")]
        [TestCase(float.NaN, 2f, "executeSeconds")]
        [TestCase(1.5f, 0f, "executeRange")]
        [TestCase(1.5f, -1f, "executeRange")]
        [TestCase(1.5f, float.PositiveInfinity, "executeRange")]
        public void ABadSpecNamesItsArgument(float executeSeconds, float executeRange, string paramName)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSpec(executeSeconds, executeRange));

            Assert.That(e.ParamName, Is.EqualTo(paramName));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(execution: new ExecutionSpec(2.5f, 3f));
            var text = NetMatchFile.Write(File(original, MatchObjective.Execution));

            var read = NetMatchFile.Parse(text, "Execution.json").Settings.Execution;

            Assert.That(read.ExecuteSeconds, Is.EqualTo(2.5f));
            Assert.That(read.ExecuteRange, Is.EqualTo(3f));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "Execution.json")), Is.EqualTo(text));
        }

        [Test]
        public void AnExecutionFileListsTheSectionEvenAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default, MatchObjective.Execution));

            Assert.That(text, Does.Contain("\"execution\": {\n      \"executeSeconds\": 1.5,\n      \"executeRange\": 2\n    }\n  },"));
            Assert.That(text, Does.Not.Contain("\"infected\""), "the infected section stays out of an execution file at its defaults");
        }

        // 節を足す前に書き出したルールのファイルを、書き出し直さずにそのまま使えるようにする
        [Test]
        public void AnotherObjectiveLeavesTheSectionOutAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("\"execution\""));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Execution, Is.SameAs(ExecutionSpec.Default));
        }

        // プレイリストは最初の項目のルールの match.* を使い続けるので、Execution でないルールにも変えた値は書く
        [Test]
        public void AnotherObjectiveWritesTheSectionWhenItWasChanged()
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(execution: new ExecutionSpec(executeRange: 3f))));

            Assert.That(text, Does.Contain("\"execution\": {"));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Execution.ExecuteRange, Is.EqualTo(3f));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void BothSectionsAreWrittenInOrderWhenBothWereChanged()
        {
            var settings = new NetMatchSettings(infected: new InfectedSpec(initialInfected: 2), execution: new ExecutionSpec(executeSeconds: 2f));
            var text = NetMatchFile.Write(File(settings));

            Assert.That(text.IndexOf("\"infected\": {", StringComparison.Ordinal), Is.LessThan(text.IndexOf("\"execution\": {", StringComparison.Ordinal)));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void MissingItemsReadAsTheirDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"execution\": {\"executeSeconds\": 3}}"), "Execution.json").Settings.Execution;

            Assert.That(read.ExecuteSeconds, Is.EqualTo(3f));
            Assert.That(read.ExecuteRange, Is.EqualTo(ExecutionSpec.DefaultExecuteRange));
            Assert.That(NetMatchFile.Parse(Text("{}"), "Execution.json").Settings.Execution, Is.SameAs(ExecutionSpec.Default));
        }

        [TestCase("{\"execution\": {\"executeSeconds\": 0}}", "match.execution.executeSeconds")]
        [TestCase("{\"execution\": {\"executeSeconds\": -1}}", "match.execution.executeSeconds")]
        [TestCase("{\"execution\": {\"executeRange\": 0}}", "match.execution.executeRange")]
        [TestCase("{\"execution\": {\"executeRange\": \"far\"}}", "match.execution.executeRange")]
        [TestCase("{\"execution\": {\"winsToWin\": 3}}", "match.execution.winsToWin")]
        [TestCase("{\"execution\": 1}", "match.execution")]
        public void ABadValueNamesItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "Execution.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("Execution.json"));
        }

        [Test]
        public void WithPrivacyKeepsTheExecutionSettings()
        {
            var original = new NetMatchSettings(execution: new ExecutionSpec(executeSeconds: 2f));

            Assert.That(original.WithPrivacy(true, "ABC234").Execution, Is.SameAs(original.Execution));
        }

        [Test]
        public void TheCommittedExecutionRulesReadTheirObjectiveRoundsAndTuning()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Execution.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Execution"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Execution));
            Assert.That(file.Rules.Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(file.Rules.Actions.Melee, Is.EqualTo(Melee.On), "executions are melee moves");
            Assert.That(file.Rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(file.Settings.RoundWinsToWin, Is.EqualTo(ExecutionSession.DefaultWinsToWin));
            Assert.That(file.Settings.RoundPreparationSeconds, Is.EqualTo(ExecutionSession.DefaultPreparationSeconds));
            Assert.That(file.Settings.RoundTimeLimitSeconds, Is.EqualTo(ExecutionSession.DefaultRoundTimeLimitSeconds));
            Assert.That(file.Settings.Execution.ExecuteSeconds, Is.EqualTo(ExecutionSpec.DefaultExecuteSeconds));
            Assert.That(file.Settings.Execution.ExecuteRange, Is.EqualTo(ExecutionSpec.DefaultExecuteRange));
        }

        [Test]
        public void TheOtherCommittedRulesLeaveTheSectionOut()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
                if (file.Rules.Objective == MatchObjective.Execution)
                {
                    continue;
                }

                Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("\"" + NetMatchFile.ExecutionItem + "\""), path);
                Assert.That(file.Settings.Execution, Is.SameAs(ExecutionSpec.Default), path);
            }
        }
    }
}
