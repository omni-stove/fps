using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StringsAuditTests
    {
        private static readonly GameLanguage[] _languages = { GameLanguage.Japanese, GameLanguage.English };

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
        public void AKeyMissingFromTheEnglishTableIsListedAndFailsTheAudit()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("Resources/Localization/ja/hud", "{\"hud.ammo\":\"弾\",\"hud.health\":\"体力\"}") },
                english: new[] { Layer("Resources/Localization/en/hud", "{\"hud.health\":\"Health\"}") }), findings);

            StringsAudit.CheckStringKeys(strings, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Strings));
            Assert.That(findings[0].Where, Is.EqualTo("en"));
            Assert.That(findings[0].Problem, Does.Contain("'hud.ammo'"));
            Assert.That(findings[0].Problem, Does.Contain("Resources/Localization/ja/hud"));
            Assert.That(ReplacementAudit.ExitCodeFor(findings), Is.EqualTo(ReplacementAudit.FindingsExitCode));
            Assert.That(ReplacementAudit.Format(findings), Does.Contain("[Strings] en: key 'hud.ammo' is missing"));
        }

        [Test]
        public void TablesWithTheSameKeysAcrossLayersPassTheAudit()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[]
                {
                    Layer("ja.json", "{\"a\":\"あ\"}"),
                    Layer("ja/hud", "{\"b\":\"い\"}"),
                },
                english: new[] { Layer("en/all", "{\"a\":\"A\",\"b\":\"B\"}") }), findings);

            StringsAudit.CheckStringKeys(strings, findings);

            Assert.That(findings, Is.Empty);
            Assert.That(strings.ReadableLanguages, Is.EqualTo(_languages));
            Assert.That(ReplacementAudit.ExitCodeFor(findings), Is.EqualTo(ReplacementAudit.SuccessExitCode));
            Assert.That(ReplacementAudit.Format(findings), Does.Contain("passed"));
        }

        [Test]
        public void AKeyMissingFromEitherSideIsListedForThatLanguage()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{\"a\":\"あ\"}") },
                english: new[] { Layer("en", "{\"a\":\"A\",\"b\":\"B\"}") }), findings);

            StringsAudit.CheckStringKeys(strings, findings);

            Assert.That(findings.Select(f => f.Where), Is.EqualTo(new[] { "ja" }));
            Assert.That(findings[0].Problem, Does.Contain("'b'"));
        }

        [Test]
        public void ALanguageWithABrokenTableIsListedOnceAndItsKeysAreNotCompared()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{\"a\":\"あ\",\"b\":\"い\"}") },
                english: new[]
                {
                    Layer("en/good", "{\"a\":\"A\"}"),
                    Layer("en/broken", "{\"b\":"),
                }), findings);

            StringsAudit.CheckStringKeys(strings, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Where, Is.EqualTo("en"));
            Assert.That(findings[0].Problem, Does.Contain("'en/broken' could not be read"));
            Assert.That(strings.ReadableLanguages, Is.EqualTo(new[] { GameLanguage.Japanese }));
        }

        [Test]
        public void ALanguageWithoutTablesIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{\"a\":\"あ\"}") },
                english: Array.Empty<KeyValuePair<string, string>>()), findings);

            StringsAudit.CheckStringKeys(strings, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Where, Is.EqualTo("en"));
            Assert.That(findings[0].Problem, Does.StartWith("no strings were found"));
        }

        [Test]
        public void AWeaponNameKeyMissingFromALanguageIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{\"weapon.rifle\":\"ライフル\",\"weapon.handgun\":\"拳銃\"}") },
                english: new[] { Layer("en", "{\"weapon.rifle\":\"Rifle\"}") }), findings);
            findings.Clear();
            var rifle = Weapon("Rifle", "weapon.rifle");
            var handgun = Weapon("Handgun", "weapon.handgun");
            var unnamed = Weapon("Unnamed", " ");

            StringsAudit.CheckWeaponNames(new[] { rifle, null, handgun, unnamed }, strings, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Strings));
            Assert.That(findings[0].Where, Is.EqualTo("Handgun"));
            Assert.That(findings[0].Problem, Is.EqualTo("name key 'weapon.handgun' is missing from the en strings"));
        }

        [Test]
        public void TheKitAuditFindsTheStringsAudit()
        {
            Assert.That(ExtensionPoints.CreateAll<IReplacementAuditCheck>(_ => 0).OfType<StringsAudit>().Count(), Is.EqualTo(1));
        }

        private WeaponDefinition Weapon(string name, string nameKey)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(weapon);
            weapon.name = name;
            weapon.Id = name.ToLowerInvariant() + "-id";
            weapon.NameKey = nameKey;
            return weapon;
        }

        private static KeyValuePair<string, string> Layer(string name, string json)
        {
            return new KeyValuePair<string, string>(name, json);
        }

        private static Func<GameLanguage, IReadOnlyList<KeyValuePair<string, string>>> Reader(
            KeyValuePair<string, string>[] japanese,
            KeyValuePair<string, string>[] english)
        {
            return language => language == GameLanguage.Japanese ? japanese : english;
        }
    }
}
