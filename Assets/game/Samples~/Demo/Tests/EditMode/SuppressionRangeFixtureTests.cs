using System;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>射撃場の制圧の的 (<see cref="Fps.Editor.SuppressionRangeFixture"/>) と smoke の手順 (<see cref="Fps.Editor.SuppressionSmokeStep"/>) を確かめる。</summary>
    public sealed class SuppressionRangeFixtureTests
    {
        [Test]
        public void TheRangeFixturePlacesATargetThatFollowsTheRules()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new SuppressionRangeFixture();
                var placed = fixture.Place(scene, GameRulesAssetBuilder.TpsRangeRules());

                Assert.That(placed.name, Is.EqualTo(SuppressionRangeFixture.TargetName));
                Assert.That(placed.scene, Is.EqualTo(scene));
                Assert.That(placed.transform.position, Is.EqualTo(SuppressionRangeFixture.TargetCenter));
                var receiver = placed.GetComponent<SuppressionReceiver>();
                Assert.That(receiver.Rule, Is.EqualTo(Suppression.On));
                Assert.That(receiver.Id, Is.EqualTo(SuppressionRangeFixture.TargetName));
                Assert.That(fixture.Footprint.Contains(SuppressionRangeFixture.TargetCenter), Is.True);

                var off = fixture.Place(scene, GameRules.Default);
                Assert.That(off.GetComponent<SuppressionReceiver>().Rule, Is.EqualTo(Suppression.Off));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheSmokeStepIsFoundAndShootsBesideBothTargets()
        {
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<SuppressionSmokeStep>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<SuppressionRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(SuppressionSmokeStep.TargetMissOffset, Is.GreaterThan(SuppressionRangeFixture.TargetDiameter / 2f));
            Assert.That(SuppressionSmokeStep.TargetMissOffset, Is.LessThan(SuppressionSpec.DefaultNearMissRadius));
            Assert.That(SuppressionSmokeStep.PlayerMissOffset, Is.GreaterThan(GameBootstrap.PlayerRadius));
            Assert.That(SuppressionSmokeStep.PlayerMissOffset, Is.LessThan(SuppressionSpec.DefaultNearMissRadius));
        }
    }
}
