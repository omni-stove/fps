using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>射撃場に置く仮のボス (<see cref="Fps.Editor.RangeBossFixture"/>) が、隠れた弱点を持つボスを置くことを確かめる。</summary>
    public sealed class RangeBossFixtureTests
    {
        [Test]
        public void TheRangeFixturePlacesABossWithAHiddenWeakPoint()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new RangeBossFixture();
                var placed = fixture.Place(scene, GameRulesAssetBuilder.TpsRangeRules());

                Assert.That(placed.name, Is.EqualTo(fixture.Name));
                Assert.That(placed.scene, Is.EqualTo(scene));
                Assert.That(placed.transform.parent, Is.Null);

                var boss = placed.GetComponent<BossController>();
                Assert.That(boss, Is.Not.Null);
                Assert.That(placed.GetComponent<BossStrafeMotion>(), Is.Not.Null);
                Assert.That(placed.GetComponent<TargetDummy>(), Is.Not.Null);
                Assert.That(placed.GetComponent<Damageable>().MaxHealth, Is.EqualTo(RangeBossFixture.MaxHealth));
                Assert.That(boss.CreatePhases().Phases, Has.Count.EqualTo(2));
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
    }
}
