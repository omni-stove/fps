using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class RangeBossFixtureTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__RangeBossFixtureTests_" + Guid.NewGuid().ToString("N");
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
        public void TheRangeFixturePlacesABossWithAHiddenWeakPoint()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new RangeBossFixture(_folder + "/RangeBoss.asset");
                var placed = fixture.Place(scene, GameRulesAssetBuilder.TpsRangeRules());

                Assert.That(placed.name, Is.EqualTo(fixture.Name));
                Assert.That(placed.scene, Is.EqualTo(scene));
                Assert.That(placed.transform.parent, Is.Null);

                var boss = placed.GetComponent<BossController>();
                Assert.That(boss, Is.Not.Null);
                Assert.That(placed.GetComponent<BossStrafeMotion>(), Is.Not.Null);
                Assert.That(placed.GetComponent<TargetDummy>(), Is.Not.Null);
                Assert.That(placed.GetComponent<Damageable>().MaxHealth, Is.EqualTo(RangeBossFixture.MaxHealth));
                Assert.That(boss.CreatePhases().Phases.Count(), Is.EqualTo(2));
                Assert.That(boss.CreatePhases().Phases[1].Trigger.HealthFraction, Is.EqualTo(RangeBossFixture.ExposeHealthFraction));
                Assert.That(boss.CreateZoneMultipliers().Body, Is.LessThan(1.0));

                var weakPoint = placed.GetComponentInChildren<BossWeakPoint>(true);
                Assert.That(weakPoint, Is.Not.Null);
                Assert.That(weakPoint.Id, Is.EqualTo(RangeBossFixture.WeakPointId));
                Assert.That(weakPoint.IsExposed, Is.False);
                Assert.That(weakPoint.GetComponent<Hitbox>().Zone, Is.EqualTo(HitZone.WeakPoint));
                Assert.That(boss.PhaseEntries[1].ExposedWeakPoints, Does.Contain(RangeBossFixture.WeakPointId));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheBossIsWiredToADefinitionWithTheFixtureValues()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var path = _folder + "/RangeBoss.asset";
                var placed = new RangeBossFixture(path).Place(scene, GameRules.Default);

                var definition = placed.GetComponent<BossController>().Definition;
                Assert.That(definition, Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<BossDefinition>(path), Is.SameAs(definition));
                Assert.That(definition.Phases.Select(p => p.Id), Is.EqualTo(new[] { RangeBossFixture.GuardedPhaseId, RangeBossFixture.ExposedPhaseId }));
                Assert.That(definition.Phases[1].HealthFraction, Is.EqualTo(RangeBossFixture.ExposeHealthFraction));
                Assert.That(definition.Phases[1].MoveSpeedMultiplier, Is.EqualTo(RangeBossFixture.ExposedSpeedMultiplier));
                Assert.That(definition.Phases[1].ExposedWeakPoints, Is.EqualTo(new[] { RangeBossFixture.WeakPointId }));
                Assert.That(definition.CreateZoneMultipliers().Body, Is.EqualTo(RangeBossFixture.BodyMultiplier).Within(1e-6));
                Assert.That(definition.CreateZoneMultipliers().WeakPoint, Is.EqualTo(RangeBossFixture.WeakPointMultiplier).Within(1e-6));
                Assert.That(definition.StrafeSpeed, Is.EqualTo(RangeBossFixture.StrafeSpeed));
                Assert.That(definition.StrafeHalfWidth, Is.EqualTo(RangeBossFixture.StrafeHalfWidth));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AnEditedDefinitionKeepsItsValuesAndDrivesThePhase()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var path = _folder + "/RangeBoss.asset";
                var definition = RangeBossFixture.LoadOrCreateDefinition(path);
                var phases = definition.Phases.ToArray();
                phases[1] = new BossPhaseEntry(phases[1].Id, 0.9f, phases[1].MoveSpeedMultiplier, phases[1].ExposedWeakPoints);
                definition.Configure(phases, definition.CreateZoneMultipliers(), definition.StrafeSpeed, definition.StrafeHalfWidth);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();

                var placed = new RangeBossFixture(path).Place(scene, GameRules.Default);
                var boss = placed.GetComponent<BossController>();
                var damageable = placed.GetComponent<Damageable>();

                Assert.That(boss.Definition, Is.SameAs(definition));
                Assert.That(boss.TryInitialize(), Is.True);
                Assert.That(boss.CurrentPhase.Id, Is.EqualTo(RangeBossFixture.GuardedPhaseId));

                // 体力 300 の 1 割。
                damageable.TakeDamage(RangeBossFixture.MaxHealth / 10);

                Assert.That(boss.CurrentPhase.Id, Is.EqualTo(RangeBossFixture.ExposedPhaseId));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheDefaultDefinitionSitsUnderTheBossesFolder()
        {
            Assert.That(RangeBossFixture.DefaultDefinitionPath, Is.EqualTo(ShooterKitRoot.OutputPath("Bosses") + "/RangeBoss.asset"));
            Assert.That(new RangeBossFixture().DefinitionPath, Is.EqualTo(RangeBossFixture.DefaultDefinitionPath));
            Assert.Throws<ArgumentException>(() => new RangeBossFixture("Packages/RangeBoss.asset"));
        }
    }
}
