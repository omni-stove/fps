using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ReplacementAudit"/> が文言のキー・武器の演出と名前・必須の参照の漏れを一覧にし、漏れの有無で終了コードを分けること。
    /// 表とアセットはメモリ上のものを渡し、プロジェクトのものには頼らない。
    /// </summary>
    public sealed class ReplacementAuditTests
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

            ReplacementAudit.CheckStringKeys(strings, findings);

            Assert.That(findings, Has.Count.EqualTo(1));
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

            ReplacementAudit.CheckStringKeys(strings, findings);

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

            ReplacementAudit.CheckStringKeys(strings, findings);

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

            ReplacementAudit.CheckStringKeys(strings, findings);

            Assert.That(findings, Has.Count.EqualTo(1));
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

            ReplacementAudit.CheckStringKeys(strings, findings);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Where, Is.EqualTo("en"));
            Assert.That(findings[0].Problem, Does.StartWith("no strings were found"));
        }

        [Test]
        public void AWeaponWithoutPresentationOrANameInEveryLanguageIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{\"weapon.rifle\":\"ライフル\",\"weapon.handgun\":\"拳銃\"}") },
                english: new[] { Layer("en", "{\"weapon.rifle\":\"Rifle\"}") }), findings);
            findings.Clear();
            var catalog = Catalog("rifle-id");
            var rifle = Weapon("Rifle", "rifle-id", "weapon.rifle");
            var handgun = Weapon("Handgun", "handgun-id", "weapon.handgun");

            ReplacementAudit.CheckWeapons(new[] { rifle, null, handgun }, catalog, strings, findings);

            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings.All(f => f.Where == "Handgun"), Is.True);
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Presentation));
            Assert.That(findings[0].Problem, Does.Contain("'handgun-id'"));
            Assert.That(findings[1].Area, Is.EqualTo(ReplacementAuditArea.Strings));
            Assert.That(findings[1].Problem, Does.Contain("'weapon.handgun' is missing from the en strings"));
        }

        [Test]
        public void AWeaponWithoutAnIdOrANameKeyIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();
            var strings = AuditedStrings.Read(_languages, Reader(
                japanese: new[] { Layer("ja", "{}") },
                english: new[] { Layer("en", "{}") }), findings);

            ReplacementAudit.CheckWeapons(new[] { Weapon("Blank", " ", string.Empty) }, Catalog("rifle-id"), strings, findings);

            Assert.That(findings.Select(f => f.Problem), Is.EqualTo(new[] { "the weapon has no id", "the weapon has no name key" }));
        }

        [Test]
        public void AMissingPresentationCatalogIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckPresentationCatalog(null, findings);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Presentation));
            Assert.That(findings[0].Where, Is.EqualTo("Resources/" + PresentationCatalog.ResourcePath));
        }

        [Test]
        public void APresentationCatalogWithEmptySlotsIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckPresentationCatalog(Catalog("rifle-id"), findings);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Problem, Does.Contain("fire sound is missing"));
        }

        [Test]
        public void AnEmptyRequiredReferenceIsListedWithItsPlaceUntilItIsFilled()
        {
            var root = Track(new GameObject("Mission"));
            var child = new GameObject("Runner");
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            var runner = child.AddComponent<MissionRunner>();
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckGameObject(root, "Assets/Test.unity", findings);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Reference));
            Assert.That(findings[0].Where, Is.EqualTo("Assets/Test.unity Mission/Runner"));
            Assert.That(findings[0].Problem, Does.StartWith("MissionRunner 'Definition' is empty"));

            runner.Definition = Track(ScriptableObject.CreateInstance<MissionDefinition>());
            findings.Clear();

            ReplacementAudit.CheckGameObject(root, "Assets/Test.unity", findings);

            Assert.That(findings, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnlyMarkedFieldsAreRequired()
        {
            var names = ReplacementAudit.RequiredFieldsOf(typeof(HordeRunner)).Select(f => f.Name).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "_spawner" }));
            Assert.That(ReplacementAudit.RequiredFieldsOf(typeof(Transform)), Is.Empty);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private WeaponDefinition Weapon(string name, string id, string nameKey)
        {
            var weapon = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            weapon.name = name;
            weapon.Id = id;
            weapon.NameKey = nameKey;
            return weapon;
        }

        private PresentationCatalog Catalog(params string[] weaponIds)
        {
            var catalog = Track(ScriptableObject.CreateInstance<PresentationCatalog>());
            catalog.name = "TestCatalog";
            catalog.SetWeapons(weaponIds.Select(id => new WeaponPresentationAssets(id, null, null, null)));
            return catalog;
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
