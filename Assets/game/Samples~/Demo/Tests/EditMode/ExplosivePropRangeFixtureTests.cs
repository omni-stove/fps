using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 射撃場に樽を並べる <see cref="ExplosivePropRangeFixture"/> と、撃って連鎖を確かめる <see cref="ExplosivePropSmokeStep"/> が
    /// 差し込み口から見つかり、並べた物が smoke の手順どおりに連鎖する間隔になっていることを確かめる。
    /// </summary>
    public sealed class ExplosivePropRangeFixtureTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }

            CombatEvents.ClearSubscribers();
        }

        [Test]
        public void TheFixtureAndTheSmokeStepAreFound()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<ExplosivePropRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<ExplosivePropSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesTwoBarrelsAGasCanAndADummyInsideTheFootprint()
        {
            var fixture = new ExplosivePropRangeFixture();

            var root = fixture.Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(fixture.Name));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var first = Prop(root, ExplosivePropRangeFixture.FirstBarrelName);
            var second = Prop(root, ExplosivePropRangeFixture.SecondBarrelName);
            var gasCan = Prop(root, ExplosivePropRangeFixture.GasCanName);
            Assert.That(first.Spec.MaxHealth, Is.EqualTo(ExplosivePropRangeFixture.BarrelSpec.MaxHealth));
            Assert.That(second.Spec.Burns, Is.False);
            Assert.That(gasCan.Spec.Burns, Is.True);
            Assert.That(gasCan.Spec.BurnSeconds, Is.EqualTo(ExplosivePropRangeFixture.GasCanSpec.BurnSeconds));
            Assert.That(first.Id, Is.EqualTo(ExplosivePropRangeFixture.FirstBarrelName));

            var dummy = root.transform.Find(ExplosivePropRangeFixture.DummyId).GetComponent<Damageable>();
            Assert.That(dummy, Is.Not.Null);
            Assert.That(dummy.Id, Is.EqualTo(ExplosivePropRangeFixture.DummyId));

            var footprint = fixture.Footprint;
            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                var bounds = collider.bounds;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(footprint.min.x - 1e-3f), collider.name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(footprint.max.x + 1e-3f), collider.name);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(footprint.min.z - 1e-3f), collider.name);
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(footprint.max.z + 1e-3f), collider.name);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-1e-3f), collider.name + " stands on the floor");
            }
        }

        [Test]
        public void ShootingTheFirstBarrelKillsTheDummyAndChainsDownTheRow()
        {
            var root = new ExplosivePropRangeFixture().Place(_scene, GameRules.Default);
            Physics.SyncTransforms();
            var first = Prop(root, ExplosivePropRangeFixture.FirstBarrelName);
            var second = Prop(root, ExplosivePropRangeFixture.SecondBarrelName);
            var gasCan = Prop(root, ExplosivePropRangeFixture.GasCanName);
            var dummy = root.transform.Find(ExplosivePropRangeFixture.DummyId).GetComponent<Damageable>();
            var collider = first.GetComponent<Collider>();

            StructureDamage.Apply(collider, collider.bounds.center, WeaponAssetBuilder.RifleSpec().Damage, 0f);
            Assert.That(first.IsTriggered, Is.True, "one rifle bullet sets the barrel off");
            Assert.That(first.Tick(0f), Is.True);

            Assert.That(dummy.IsDead, Is.True, "the dummy next to the barrel dies in the blast");
            Assert.That(second.IsChained, Is.True);
            Assert.That(second.RemainingSeconds, Is.EqualTo(ExplosivePropRangeFixture.BarrelSpec.ChainDelaySeconds));
            Assert.That(gasCan.IsBurning, Is.True, "the gas can catches fire from the first blast");

            Assert.That(second.Tick(second.RemainingSeconds), Is.True);
            Assert.That(gasCan.Tick(ExplosivePropRangeFixture.GasCanSpec.BurnSeconds), Is.True);
        }

        [Test]
        public void TheSmokeStepAllowsForTheBurnAndChainDelays()
        {
            var step = new ExplosivePropSmokeStep();

            Assert.That(step.Name, Is.Not.Empty);
            Assert.That(
                step.TimeoutSeconds,
                Is.GreaterThan(ExplosivePropRangeFixture.BarrelSpec.ChainDelaySeconds + ExplosivePropRangeFixture.GasCanSpec.BurnSeconds));

            step.Reset();
            step.OnLog(null);
            step.Cleanup();
        }

        private static ExplosiveProp Prop(GameObject root, string name)
        {
            var child = root.transform.Find(name);
            Assert.That(child, Is.Not.Null, name);
            var prop = child.GetComponent<ExplosiveProp>();
            Assert.That(prop, Is.Not.Null, name);
            return prop;
        }
    }
}
