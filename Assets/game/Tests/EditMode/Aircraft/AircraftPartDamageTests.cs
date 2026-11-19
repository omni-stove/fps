using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 胴・左右の翼・尾翼・エンジンを箱で作った機体を、上から撃って部位を削る。物理は進めない。
    public sealed class AircraftPartDamageTests
    {
        private const float Epsilon = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<AircraftPartBrokenInfo> _broken = new List<AircraftPartBrokenInfo>();
        private Vehicle _vehicle;
        private SimpleAircraftMotor _motor;
        private AircraftPartDamage _parts;
        private Transform _leftWing;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Plane"));
            AddBox(root.transform, "Fuselage", Vector3.zero, new Vector3(1f, 1f, 6f));
            _leftWing = AddPart(root.transform, "WingLeft", AircraftDamageSpec.DefaultLeftWingId, new Vector3(-3f, 0f, 0f), new Vector3(4f, 0.2f, 1.5f));
            AddPart(root.transform, "WingRight", AircraftDamageSpec.DefaultRightWingId, new Vector3(3f, 0f, 0f), new Vector3(4f, 0.2f, 1.5f));
            AddPart(root.transform, "Tail", AircraftDamageSpec.DefaultTailId, new Vector3(0f, 0.5f, -3.5f), new Vector3(0.2f, 1f, 1f));
            AddPart(root.transform, "Engine", AircraftDamageSpec.DefaultEngineId, new Vector3(0f, 0f, 3.5f), new Vector3(1f, 1f, 1f));
            new GameObject("Pilot").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);

            _vehicle = root.AddComponent<Vehicle>();
            _motor = root.AddComponent<SimpleAircraftMotor>();
            _motor.Body.useGravity = false;
            _motor.Body.isKinematic = true;
            _vehicle.Configure(new VehicleSpec(
                "plane",
                5000,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) },
                parts: new[]
                {
                    new VehiclePartSpec(AircraftDamageSpec.DefaultLeftWingId, 1f),
                    new VehiclePartSpec(AircraftDamageSpec.DefaultRightWingId, 1f),
                    new VehiclePartSpec(AircraftDamageSpec.DefaultTailId, 1f),
                    new VehiclePartSpec(AircraftDamageSpec.DefaultEngineId, 2f),
                }));

            _parts = root.AddComponent<AircraftPartDamage>();
            _parts.Attach(_vehicle);
            AircraftEvents.PartBroken += OnBroken;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            AircraftEvents.PartBroken -= OnBroken;
            _broken.Clear();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ItIsTheConditionSourceTheMotorReads()
        {
            Assert.That(_vehicle.GetComponent<IAircraftConditionSource>(), Is.SameAs(_parts));
            Assert.That(_parts.Vehicle, Is.SameAs(_vehicle));
            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
            Assert.That(_parts.Spec.Parts.Count, Is.EqualTo(4), "no asset uses the default parts");
        }

        [Test]
        public void ShootingTheLeftWingUntilItBreaksDropsTheLeftLiftAndReportsOnce()
        {
            ShootDown(-3f, 0f, 100);
            ShootDown(-3f, 0f, 100);
            Assert.That(_parts.Damage.Remaining(AircraftDamageSpec.DefaultLeftWingId), Is.EqualTo(100));
            Assert.That(_parts.Condition.LeftLift, Is.EqualTo(0.2f + (0.8f / 3f)).Within(Epsilon), "a third left");
            Assert.That(_broken, Is.Empty);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' part broken part=wing_left effect=LeftLift lift=0.20/1.00 control=1.00 thrust=1.00");
            ShootDown(-3f, 0f, 100);
            ShootDown(-3f, 0f, 100);

            Assert.That(_parts.Condition.LeftLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(Epsilon));
            Assert.That(_parts.Condition.RightLift, Is.EqualTo(1f));
            Assert.That(_broken.Count, Is.EqualTo(1), "a broken wing is reported once");
            Assert.That(_broken[0].Aircraft, Is.SameAs(_vehicle.gameObject));
            Assert.That(_broken[0].PartId, Is.EqualTo(AircraftDamageSpec.DefaultLeftWingId));
            Assert.That(_broken[0].Effect, Is.EqualTo(AircraftDamageEffect.LeftLift));
            Assert.That(_broken[0].Position, Is.EqualTo(_leftWing.position));
            Assert.That(_broken[0].Condition, Is.EqualTo(_parts.Condition));
            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(4600), "the hull still takes every shot");
        }

        [Test]
        public void ThePartTakesTheDamageAfterThePartMultiplier()
        {
            ShootDown(0f, 3.5f, 100);

            Assert.That(_parts.Damage.Remaining(AircraftDamageSpec.DefaultEngineId), Is.EqualTo(AircraftDamageSpec.DefaultEngineDurability - 200));
            Assert.That(_parts.Condition.Thrust, Is.EqualTo(50f / AircraftDamageSpec.DefaultEngineDurability).Within(Epsilon));
        }

        [Test]
        public void ABrokenEngineLeavesTheMotorWithoutThrust()
        {
            _motor.SetInput(new VehicleMotorInput(1f));
            _motor.Step();
            Assert.That(_motor.LastCommand.LocalAcceleration.Z, Is.EqualTo(AircraftSpec.DefaultMaxThrust).Within(Epsilon));

            ShootDown(0f, 3.5f, 100);
            ShootDown(0f, 3.5f, 100);
            _motor.Step();

            Assert.That(_parts.Damage.IsBroken(AircraftDamageSpec.DefaultEngineId), Is.True);
            Assert.That(_motor.LastCommand.LocalAcceleration.Z, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void AHitOnTheFuselageLeavesThePartsAlone()
        {
            ShootDown(0f, 0f, 100);

            Assert.That(_vehicle.Damageable.Health.Current, Is.EqualTo(4900));
            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void ResettingTheVehiclesHealthRepairsTheParts()
        {
            _parts.ApplyPartDamage(AircraftDamageSpec.DefaultTailId, 1000, Vector3.zero);
            Assert.That(_parts.Condition.Control, Is.EqualTo(AircraftDamageSpec.DefaultMinControl).Within(Epsilon));

            _vehicle.Damageable.ResetHealth();

            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void ApplyPartDamageReportsABreakAndIgnoresUnknownParts()
        {
            Assert.That(_parts.ApplyPartDamage("fuel", 1000, Vector3.zero), Is.False);
            Assert.That(_parts.ApplyPartDamage(AircraftDamageSpec.DefaultRightWingId, 1000, Vector3.one), Is.True);

            Assert.That(_broken.Count, Is.EqualTo(1));
            Assert.That(_broken[0].Position, Is.EqualTo(Vector3.one));
            Assert.That(_parts.Condition.RightLift, Is.EqualTo(AircraftDamageSpec.DefaultMinLift).Within(Epsilon));
        }

        [Test]
        public void TheDefinitionsDamageBlockSetsTheParts()
        {
            var definition = NewDefinition("Glider");
            definition.Damage.CopyFrom(new AircraftDamageSpec(
                new[] { new AircraftDamagePartSpec(AircraftDamageSpec.DefaultLeftWingId, AircraftDamageEffect.LeftLift, 50) },
                minLift: 0f));
            _parts.Definition = definition;

            ShootDown(-3f, 0f, 50);
            ShootDown(0f, 3.5f, 100);

            Assert.That(_parts.Condition.LeftLift, Is.Zero);
            Assert.That(_parts.Condition.Thrust, Is.EqualTo(1f), "the glider has no engine part");
        }

        [Test]
        public void ConfigureWinsOverTheDefinitionUntilTheDefinitionIsSetAgain()
        {
            var definition = NewDefinition("Glider");
            definition.Damage.CopyFrom(new AircraftDamageSpec(new AircraftDamagePartSpec[0]));
            var spec = new AircraftDamageSpec(new[] { new AircraftDamagePartSpec("tail", AircraftDamageEffect.Control, 10) });

            _parts.Configure(spec);
            Assert.That(_parts.Spec, Is.SameAs(spec));

            _parts.Definition = definition;
            Assert.That(_parts.Spec.Parts, Is.Empty);
            Assert.Throws<System.ArgumentNullException>(() => _parts.Configure(null));
        }

        [Test]
        public void AnInvalidDefinitionIsLoggedAndThePartsStopMattering()
        {
            var definition = NewDefinition("BrokenPlane");
            definition.Damage.Parts = new[] { new AircraftDamageSettings.Part(string.Empty, AircraftDamageEffect.LeftLift, 10) };
            _parts.Definition = definition;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Vehicle\] 'Plane' aircraft damage settings are invalid; parts take no flight damage: aircraft 'BrokenPlane' is invalid"));
            ShootDown(-3f, 0f, 100);

            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void ClearingTheCombatSubscribersAlsoClearsThePartBrokenSubscribers()
        {
            CombatEvents.ClearSubscribers();

            _parts.ApplyPartDamage(AircraftDamageSpec.DefaultRightWingId, 1000, Vector3.zero);

            Assert.That(_broken, Is.Empty);
        }

        [Test]
        public void ItIsThePartsNetV2HandsTheServerDamageTo()
        {
            Assert.That(_vehicle.GetComponentInChildren<INetVehicleParts>(true), Is.SameAs(_parts));
        }

        [Test]
        public void ApplyServerTakenSetsEachPartToItsDurabilityLessWhatTheServerSaysItTook()
        {
            var taken = new Dictionary<string, int>
            {
                { AircraftDamageSpec.DefaultLeftWingId, 200 },
                { AircraftDamageSpec.DefaultEngineId, 10000 },
            };

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' net condition lift=0.47/1.00 control=1.00 thrust=0.00");
            _parts.ApplyServerTaken(id => taken.TryGetValue(id, out var amount) ? amount : 0);

            Assert.That(_parts.Damage.Remaining(AircraftDamageSpec.DefaultLeftWingId), Is.EqualTo(AircraftDamageSpec.DefaultWingDurability - 200));
            Assert.That(_parts.Damage.Remaining(AircraftDamageSpec.DefaultEngineId), Is.Zero, "more than the durability leaves nothing, not less");
            Assert.That(_parts.Damage.IsBroken(AircraftDamageSpec.DefaultEngineId), Is.True);
            Assert.That(_parts.Condition.LeftLift, Is.EqualTo(0.2f + (0.8f / 3f)).Within(Epsilon));
            Assert.That(_parts.Condition.Thrust, Is.EqualTo(AircraftDamageSpec.DefaultMinThrust).Within(Epsilon));
            Assert.That(_broken, Is.Empty, "the server's totals do not replay the breaks");
        }

        [Test]
        public void ApplyServerTakenRepairsThePartsTheServerNoLongerLists()
        {
            _parts.ApplyPartDamage(AircraftDamageSpec.DefaultTailId, 1000, Vector3.zero);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' net condition lift=1.00/1.00 control=1.00 thrust=1.00");
            _parts.ApplyServerTaken(_ => 0);

            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
        }

        [Test]
        public void ApplyServerTakenLogsNothingWhenTheConditionStaysAndKeepsTheVehicleNameAfterDetach()
        {
            _parts.Detach();
            var lines = new List<string>();
            void Heard(string message, string stackTrace, LogType type) => lines.Add(message);
            Application.logMessageReceived += Heard;
            try
            {
                _parts.ApplyServerTaken(_ => 0);
            }
            finally
            {
                Application.logMessageReceived -= Heard;
            }

            Assert.That(lines, Is.Empty);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Plane' net condition lift=1.00/1.00 control=0.30 thrust=1.00");
            _parts.ApplyServerTaken(id => id == AircraftDamageSpec.DefaultTailId ? AircraftDamageSpec.DefaultTailDurability : 0);

            Assert.Throws<System.ArgumentNullException>(() => _parts.ApplyServerTaken(null));
        }

        [Test]
        public void DetachingStopsTheHitsFromReachingTheParts()
        {
            _parts.Detach();

            ShootDown(-3f, 0f, 400);

            Assert.That(_parts.Condition, Is.EqualTo(AircraftCondition.Intact));
            Assert.That(_parts.Vehicle, Is.Null);
            Assert.Throws<System.ArgumentNullException>(() => _parts.Attach(null));
        }

        private void ShootDown(float x, float z, int damage)
        {
            var result = Hitscan.Fire(new Vector3(x, 5f, z), Vector3.down, 20f, damage, DamageFalloff.None, null);
            Assert.That(result.Hit, Is.True);
        }

        private void OnBroken(AircraftPartBrokenInfo info)
        {
            _broken.Add(info);
        }

        private AircraftDefinition NewDefinition(string assetName)
        {
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = assetName;
            _created.Add(definition);
            return definition;
        }

        private static Transform AddPart(Transform parent, string name, string partId, Vector3 center, Vector3 size)
        {
            var box = AddBox(parent, name, center, size);
            box.gameObject.AddComponent<VehiclePart>().PartId = partId;
            return box.transform;
        }

        private static BoxCollider AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.size = size;
            return box;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}
