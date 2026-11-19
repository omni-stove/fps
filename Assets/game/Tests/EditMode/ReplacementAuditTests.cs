using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ReplacementAuditTests
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
        public void AWeaponWithoutPresentationIsListedAndFailsTheAudit()
        {
            var findings = new List<ReplacementAuditFinding>();
            var catalog = Catalog("rifle-id");
            var rifle = Weapon("Rifle", "rifle-id", "weapon.rifle");
            var handgun = Weapon("Handgun", "handgun-id", "weapon.handgun");

            ReplacementAudit.CheckWeapons(new[] { rifle, null, handgun }, catalog, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Where, Is.EqualTo("Handgun"));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Presentation));
            Assert.That(findings[0].Problem, Does.Contain("'handgun-id'"));
            Assert.That(ReplacementAudit.ExitCodeFor(findings), Is.EqualTo(ReplacementAudit.FindingsExitCode));
            Assert.That(ReplacementAudit.Format(findings), Does.Contain("[Presentation] Handgun: weapon id 'handgun-id' has no sounds or effects"));
        }

        [Test]
        public void AWeaponWithAnIdPresentationAndANameKeyPassesTheAudit()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckWeapons(new[] { Weapon("Rifle", "rifle-id", "weapon.rifle") }, Catalog("rifle-id"), findings);

            Assert.That(findings, Is.Empty);
            Assert.That(ReplacementAudit.ExitCodeFor(findings), Is.EqualTo(ReplacementAudit.SuccessExitCode));
            Assert.That(ReplacementAudit.Format(findings), Does.Contain("passed"));
        }

        [Test]
        public void AWeaponWithoutAnIdOrANameKeyIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckWeapons(new[] { Weapon("Blank", " ", string.Empty) }, Catalog("rifle-id"), findings);

            Assert.That(findings.Select(f => f.Problem), Is.EqualTo(new[] { "the weapon has no id", "the weapon has no name key" }));
            Assert.That(findings[1].Area, Is.EqualTo(ReplacementAuditArea.Strings));
        }

        [Test]
        public void AMissingPresentationCatalogIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckPresentationCatalog(null, findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Presentation));
            Assert.That(findings[0].Where, Is.EqualTo("Resources/" + PresentationCatalog.ResourcePath));
        }

        [Test]
        public void APresentationCatalogWithEmptySlotsIsListed()
        {
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckPresentationCatalog(Catalog("rifle-id"), findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Problem, Does.Contain("fire sound is missing"));
        }

        [Test]
        public void AnEmptyRequiredReferenceIsListedWithItsPlaceUntilItIsFilled()
        {
            var root = Track(new GameObject("Horde"));
            var child = new GameObject("Runner");
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            var runner = child.AddComponent<HordeRunner>();
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckGameObject(root, "Assets/Test.unity", findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Reference));
            Assert.That(findings[0].Where, Is.EqualTo("Assets/Test.unity Horde/Runner"));
            Assert.That(findings[0].Problem, Does.StartWith("HordeRunner 'Spawner' is empty"));

            var spawnerHost = Track(new GameObject("Spawner"));
            spawnerHost.SetActive(false);
            runner.Spawner = spawnerHost.AddComponent<WaveSpawner>();
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
    }
}
