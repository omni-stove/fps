using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace Fps.Core.Tests
{
    public sealed class ReadmeTuningItemsTests
    {
        private static readonly Regex _tableRow = new Regex(@"^\| `([\w.]+)` \| ([-0-9.]+) \|", RegexOptions.Multiline);
        private static readonly Regex _itemWithDefault = new Regex(@"`(\w+)` \(([-0-9.]+|true|false)[ )]");
        private static readonly string[] _changedMotorItems = { "dodgeCooldownSeconds", "vaultReach", "coverMaxEnterDistance", "ladderMinApproachSpeed", "diveSeconds" };

        [TestCase("suppression")]
        [TestCase("squad")]
        [TestCase("stagger")]
        [TestCase("melee")]
        [TestCase("traversal")]
        [TestCase("stealth")]
        [TestCase("bots")]
        public void TheTuningRowNamesEveryItemOfTheJson(string name)
        {
            var row = TuningRow(name);
            var items = new Dictionary<string, JsonElement>();
            using (var json = JsonDocument.Parse(File.ReadAllText(TuningPath(name))))
            {
                CollectItems(json.RootElement, items);
            }

            Assert.That(items, Is.Not.Empty);
            foreach (var item in items.Keys)
            {
                Assert.That(row, Does.Contain("`" + item + "`"), name + ".json item " + item);
            }
        }

        [TestCase("suppression", 9)]
        [TestCase("squad", 6)]
        [TestCase("melee", 17)]
        [TestCase("traversal", 14)]
        [TestCase("stealth", 10)]
        [TestCase("bots", 29)]
        public void TheTuningRowDefaultsMatchTheJson(string name, int expectedDefaults)
        {
            var row = TuningRow(name);
            var items = new Dictionary<string, JsonElement>();
            using var json = JsonDocument.Parse(File.ReadAllText(TuningPath(name)));
            CollectItems(json.RootElement, items);

            var matches = _itemWithDefault.Matches(row);
            Assert.That(matches.Count, Is.EqualTo(expectedDefaults));
            foreach (RegexMatch match in matches)
            {
                var item = match.Groups[1].Value;
                Assert.That(items.ContainsKey(item), Is.True, name + ".json has no item " + item);
                var written = match.Groups[2].Value;
                var value = items[item];
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    Assert.That(written, Is.EqualTo(value.GetBoolean() ? "true" : "false"), name + ".json " + item);
                }
                else
                {
                    Assert.That(Parse(written), Is.EqualTo(value.GetDouble()).Within(1e-6), name + ".json " + item);
                }
            }
        }

        [Test]
        public void TheGrenadeRowNamesTheNewThrowItemsWithTheirDefaults()
        {
            var row = TuningRow("grenade");
            var expected = new Dictionary<string, float>
            {
                ["rollDamping"] = GrenadeSpec.DefaultRollDamping,
                ["bodyRadius"] = GrenadeSpec.DefaultBodyRadius,
                ["mass"] = GrenadeSpec.DefaultMass,
                ["bounciness"] = GrenadeSpec.DefaultBounciness,
                ["friction"] = GrenadeSpec.DefaultFriction,
                ["angularDamping"] = GrenadeSpec.DefaultAngularDamping,
                ["releaseDistance"] = GrenadeSpec.DefaultReleaseDistance,
            };

            var written = Defaults(row);
            foreach (var pair in expected)
            {
                Assert.That(written.ContainsKey(pair.Key), Is.True, "grenade row has no default for " + pair.Key);
                Assert.That(written[pair.Key], Is.EqualTo(pair.Value).Within(1e-6), pair.Key);
            }

            using var json = JsonDocument.Parse(File.ReadAllText(TuningPath("grenade")));
            foreach (var type in json.RootElement.GetProperty("types").EnumerateArray())
            {
                foreach (var property in type.EnumerateObject())
                {
                    Assert.That(row, Does.Contain("`" + property.Name + "`"), "grenade.json item " + property.Name);
                }
            }
        }

        [Test]
        public void TheMotorTableDefaultsMatchMotorSpecAndProneSpec()
        {
            var rows = TableRows(Section("### 移動の値"));

            Assert.That(rows.Count, Is.EqualTo(27));
            foreach (var row in rows)
            {
                var item = row.Key;
                var isProne = item.StartsWith("prone", StringComparison.Ordinal) || item.StartsWith("dive", StringComparison.Ordinal);
                var owner = isProne ? typeof(ProneSpec) : typeof(MotorSpec);
                var constant = "Default" + Pascal(item.StartsWith("prone", StringComparison.Ordinal) ? item.Substring("prone".Length) : item);
                var field = owner.GetField(constant, BindingFlags.Public | BindingFlags.Static);
                Assert.That(field, Is.Not.Null, owner.Name + "." + constant);
                Assert.That(row.Value, Is.EqualTo(Convert.ToDouble(field.GetValue(null), CultureInfo.InvariantCulture)).Within(1e-6), item);
            }

            Assert.That(rows.Keys, Is.SupersetOf(_changedMotorItems));
        }

        [Test]
        public void TheMotorTableNamesTheItemsOfTheRulesFile()
        {
            var rows = TableRows(Section("### 移動の値"));

            foreach (var item in rows.Keys)
            {
                Assert.That(NetMatchFileConstants(), Does.Contain(item), "rules file motor has no item " + item);
            }

            Assert.That(Section("### 移動の値"), Does.Contain("`maxPlayers` (既定 " + NetMatchSettings.DefaultMaxPlayers + "、" + NetMatchSettings.MinMaxPlayers + "〜" + NetMatchSettings.MaxMaxPlayers + "。"));
        }

        [Test]
        public void TheRulesTableDefaultsMatchGameRulesDefault()
        {
            var rows = TableRows(Section("### モードのルールを JSON から取り込む"));

            Assert.That(rows["playerMaxHealth"], Is.EqualTo(GameRules.Default.PlayerMaxHealth));
            Assert.That(rows["actions.reviveRange"], Is.EqualTo(GameRules.Default.Actions.ReviveRange).Within(1e-6));
            var zone = GameRules.Default.ZoneDamage;
            Assert.That(
                Section("### モードのルールを JSON から取り込む"),
                Does.Contain("| `zoneDamage` | `{ \"head\": " + Number(zone.Head) + ", \"body\": " + Number(zone.Body) + ", \"limb\": " + Number(zone.Limb) + " }` |"));
        }

        [TestCase("`Awareness` の欄と既定", typeof(AwarenessSpec), 18)]
        [TestCase("`Melee` の欄と既定", typeof(MeleeSpec), 7)]
        public void TheEnemyDefinitionDefaultsMatchTheSpec(string paragraphStart, Type spec, int expectedDefaults)
        {
            var section = Section("### 敵の定義");
            var start = section.IndexOf(paragraphStart, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), paragraphStart);
            var end = section.IndexOf("\n\n", start, StringComparison.Ordinal);
            var written = Defaults(section.Substring(start, end - start));

            Assert.That(written.Count, Is.EqualTo(expectedDefaults));
            foreach (var pair in written)
            {
                var field = spec.GetField("Default" + pair.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.That(field, Is.Not.Null, spec.Name + ".Default" + pair.Key);
                Assert.That(pair.Value, Is.EqualTo(Convert.ToDouble(field.GetValue(null), CultureInfo.InvariantCulture)).Within(1e-6), pair.Key);
            }
        }

        [Test]
        public void TheEnemyDefinitionTableDefaultsMatchEnemySpec()
        {
            var section = Section("### 敵の定義");

            Assert.That(section, Does.Contain("| `RecoilControl` (射撃) | " + Number(EnemySpec.DefaultRecoilControl) + " |"));
            Assert.That(section, Does.Contain("| `ArrivalDistance` (動き) | " + Number(EnemySpec.DefaultArrivalDistance) + " |"));
            Assert.That(section, Does.Contain("| `SprintRatio` (動き) | " + Number(EnemySpec.DefaultSprintRatio) + " |"));
            Assert.That(section, Does.Contain("| `CrouchRatio` (動き) | " + Number(EnemySpec.DefaultCrouchRatio) + " |"));
        }

        [Test]
        public void TheEntryTableNamesEveryTuningField()
        {
            var entry = Section("### 入口の欄と引く順");

            foreach (var field in new[]
            {
                "GrenadeTuning", "PingTuning", "KnockbackTuning", "InventoryTuning", "SuppressionTuning", "SquadTuning",
                "StaggerTuning", "MeleeTuning", "TraversalTuning", "StealthTuning", "BotsTuning",
                "FreeForAll", "Guardian", "Execution", "BattleRoyale",
            })
            {
                Assert.That(entry, Does.Contain("`" + field + "`"), field);
            }
        }

        private static string TuningRow(string name)
        {
            var readme = Readme();
            var start = readme.IndexOf("| `Tuning/" + name + ".json` |", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "README has no row for Tuning/" + name + ".json");
            var end = readme.IndexOf('\n', start);
            return readme.Substring(start, end - start);
        }

        private static string Section(string heading)
        {
            var readme = Readme();
            var start = readme.IndexOf("\n" + heading, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "README has no section " + heading);
            var end = readme.IndexOf("\n#", start + heading.Length + 1, StringComparison.Ordinal);
            return end < 0 ? readme.Substring(start) : readme.Substring(start, end - start);
        }

        private static Dictionary<string, double> TableRows(string section)
        {
            var rows = new Dictionary<string, double>();
            foreach (RegexMatch match in _tableRow.Matches(section.Replace("\r\n", "\n")))
            {
                rows.Add(match.Groups[1].Value, Parse(match.Groups[2].Value));
            }

            return rows;
        }

        private static Dictionary<string, double> Defaults(string text)
        {
            var written = new Dictionary<string, double>();
            foreach (RegexMatch match in _itemWithDefault.Matches(text))
            {
                written.Add(match.Groups[1].Value, Parse(match.Groups[2].Value));
            }

            return written;
        }

        private static void CollectItems(JsonElement element, Dictionary<string, JsonElement> items)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    items[property.Name] = property.Value;
                    CollectItems(property.Value, items);
                }
                else
                {
                    items[property.Name] = property.Value;
                }
            }
        }

        private static List<string> NetMatchFileConstants()
        {
            return typeof(NetMatchFile)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("Item", StringComparison.Ordinal))
                .Select(f => (string)f.GetValue(null))
                .ToList();
        }

        private static double Parse(string text) => text == "true" ? 1 : text == "false" ? 0 : double.Parse(text, CultureInfo.InvariantCulture);

        private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(float value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Pascal(string item) => char.ToUpperInvariant(item[0]) + item.Substring(1);

        private static string TuningPath(string name) => Path.Combine(RepositoryRoot(), "Assets", "game", "Resources", "Tuning", name + ".json");

        private static string Readme() => File.ReadAllText(Path.Combine(RepositoryRoot(), "Assets", "game", "README.md")).Replace("\r\n", "\n");

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Assets", "game", "README.md")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("Assets/game/README.md is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
