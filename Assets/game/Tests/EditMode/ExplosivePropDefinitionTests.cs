using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class ExplosivePropDefinitionTests
    {
        // ほかのテストやシーンの物と重ならない場所。
        private static readonly Vector3 _origin = new Vector3(-300f, 0f, 300f);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
            CombatEvents.ClearSubscribers();
        }

        [Test]
        public void ANewDefinitionHasTheSpecDefaults()
        {
            var spec = CreateDefinition("Defaults").ToSpec();

            Assert.That(spec.MaxHealth, Is.EqualTo(ExplosivePropSpec.DefaultMaxHealth));
            Assert.That(spec.MaxDamage, Is.EqualTo(ExplosivePropSpec.DefaultMaxDamage));
            Assert.That(spec.DamageRadius, Is.EqualTo(ExplosivePropSpec.DefaultDamageRadius));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(ExplosivePropSpec.DefaultFullDamageRadius));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(ExplosivePropSpec.DefaultStructureDamageAtCenter));
            Assert.That(spec.ChainDelaySeconds, Is.EqualTo(ExplosivePropSpec.DefaultChainDelaySeconds));
            Assert.That(spec.BurnSeconds, Is.EqualTo(ExplosivePropSpec.DefaultBurnSeconds));
        }

        [Test]
        public void EveryValueOfTheDefinitionGoesIntoTheSpec()
        {
            var definition = CreateDefinition("GasCan");
            definition.MaxHealth = 40;
            definition.MaxDamage = 90;
            definition.DamageRadius = 8f;
            definition.FullDamageRadius = 2f;
            definition.StructureDamageAtCenter = 60;
            definition.ChainDelaySeconds = 0.75f;
            definition.BurnSeconds = 2.5f;

            var spec = definition.ToSpec();

            Assert.That(spec.MaxHealth, Is.EqualTo(40));
            Assert.That(spec.MaxDamage, Is.EqualTo(90));
            Assert.That(spec.DamageRadius, Is.EqualTo(8f));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(2f));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(60));
            Assert.That(spec.ChainDelaySeconds, Is.EqualTo(0.75f));
            Assert.That(spec.BurnSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.Burns, Is.True);
            Assert.That(definition.ToSpec(), Is.Not.SameAs(spec), "each call makes a new spec");
        }

        [Test]
        public void AnInvalidDefinitionNamesTheAsset()
        {
            var definition = CreateDefinition("BrokenBarrel");
            definition.FullDamageRadius = definition.DamageRadius;

            var error = Assert.Throws<InvalidOperationException>(() => definition.ToSpec());

            Assert.That(error.Message, Does.Contain("BrokenBarrel"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CopyFromWritesEveryValueBack()
        {
            var definition = CreateDefinition("Copied");
            var source = new ExplosivePropSpec(7, 33, 6f, 0.5f, 12, 1.25f, 3f);

            definition.CopyFrom(source);
            var spec = definition.ToSpec();

            Assert.That(spec.MaxHealth, Is.EqualTo(7));
            Assert.That(spec.MaxDamage, Is.EqualTo(33));
            Assert.That(spec.DamageRadius, Is.EqualTo(6f));
            Assert.That(spec.FullDamageRadius, Is.EqualTo(0.5f));
            Assert.That(spec.StructureDamageAtCenter, Is.EqualTo(12));
            Assert.That(spec.ChainDelaySeconds, Is.EqualTo(1.25f));
            Assert.That(spec.BurnSeconds, Is.EqualTo(3f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ConfigureWithADefinitionUsesItsValuesAndRefillsTheHealth()
        {
            var barrel = CreateBarrel("Barrel", Vector3.zero);
            barrel.ApplyDamage(5, chained: false);
            var definition = CreateDefinition("Tough");
            definition.MaxHealth = 80;
            definition.DamageRadius = 8f;

            barrel.Configure(definition);

            Assert.That(barrel.Definition, Is.SameAs(definition));
            Assert.That(barrel.Spec.MaxHealth, Is.EqualTo(80));
            Assert.That(barrel.Spec.DamageRadius, Is.EqualTo(8f));
            Assert.That(barrel.Health, Is.EqualTo(80));
            Assert.Throws<ArgumentNullException>(() => barrel.Configure((ExplosivePropDefinition)null));
        }

        [Test]
        public void ConfigureWithAnInvalidDefinitionKeepsTheCurrentValues()
        {
            var barrel = CreateBarrel("Barrel", Vector3.zero);
            var definition = CreateDefinition("Broken");
            definition.MaxHealth = 0;

            Assert.Throws<InvalidOperationException>(() => barrel.Configure(definition));

            Assert.That(barrel.Definition, Is.Null);
            Assert.That(barrel.Spec.MaxHealth, Is.EqualTo(ExplosivePropSpec.DefaultMaxHealth));
        }

        [Test]
        public void AWiredDefinitionIsReadInsteadOfTheInspectorValues()
        {
            var definition = CreateDefinition("Wired");
            definition.DamageRadius = 8f;
            definition.MaxHealth = 35;
            var barrel = CreateBarrel("Barrel", Vector3.zero, definition);

            Assert.That(barrel.Definition, Is.SameAs(definition));
            Assert.That(barrel.Spec.DamageRadius, Is.EqualTo(8f));
            Assert.That(barrel.Spec.MaxHealth, Is.EqualTo(35));
        }

        [Test]
        public void WithoutADefinitionTheInspectorValuesAreUsed()
        {
            var barrel = CreateBarrel("Barrel", Vector3.zero);

            Assert.That(barrel.Definition, Is.Null);
            Assert.That(barrel.Spec.DamageRadius, Is.EqualTo(ExplosivePropSpec.DefaultDamageRadius));
            Assert.That(barrel.Spec.MaxHealth, Is.EqualTo(ExplosivePropSpec.DefaultMaxHealth));
        }

        [Test]
        public void AWiredInvalidDefinitionIsReportedOnceAndIgnoresHits()
        {
            var definition = CreateDefinition("BrokenWired");
            definition.DamageRadius = 0f;
            var barrel = CreateBarrel("Barrel", Vector3.zero, definition);

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(ExplosiveProp.LogTag) + ".*Barrel.*BrokenWired"));
            Assert.That(barrel.ApplyDamage(100, chained: false), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(barrel.ApplyDamage(100, chained: false), Is.EqualTo(ExplosiveChargeStep.None));
            Assert.That(barrel.Spec, Is.Null);
        }

        [Test]
        public void AnEightMetreDefinitionHurtsATargetEightMetresAway()
        {
            // 標的の箱は幅 0.6 m なので、中心を 8 m 先に置くと爆発の中心から手前の面までは 7.7 m。
            var definition = CreateDefinition("Wide");
            definition.DamageRadius = 8f;
            var wide = CreateBarrel("Wide", Vector3.zero, definition);
            var target = CreateTarget("Target", new Vector3(8f, 1f, 0f));

            wide.ApplyDamage(wide.Spec.MaxHealth, chained: false);
            wide.Tick(0f);

            // 1.5 m から 8 m まで 150 から減る: round(150 * (1 - (7.7 - 1.5) / 6.5)) = 7。
            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max - 7));
        }

        [Test]
        public void TheDefaultRadiusDoesNotReachATargetEightMetresAway()
        {
            var barrel = CreateBarrel("Default", Vector3.zero);
            var target = CreateTarget("Target", new Vector3(8f, 1f, 0f));

            barrel.ApplyDamage(barrel.Spec.MaxHealth, chained: false);
            barrel.Tick(0f);

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
        }

        private ExplosivePropDefinition CreateDefinition(string definitionName)
        {
            var definition = ScriptableObject.CreateInstance<ExplosivePropDefinition>();
            definition.name = definitionName;
            _created.Add(definition);
            return definition;
        }

        // Configure を使わず SerializedObject で書くのは、Inspector で配線したときと同じく最初に値を読むときに定義が読まれることを確かめるため。
        private ExplosiveProp CreateBarrel(string barrelName, Vector3 offset, ExplosivePropDefinition definition = null)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _created.Add(barrel);
            barrel.name = barrelName;
            barrel.transform.position = _origin + offset + new Vector3(0f, 0.45f, 0f);
            barrel.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            var prop = barrel.AddComponent<ExplosiveProp>();
            if (definition != null)
            {
                var serialized = new SerializedObject(prop);
                serialized.FindProperty("_definition").objectReferenceValue = definition;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Physics.SyncTransforms();
            return prop;
        }

        private Damageable CreateTarget(string targetName, Vector3 offset)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(body);
            body.name = targetName;
            body.transform.position = _origin + offset;
            body.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(targetName, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
