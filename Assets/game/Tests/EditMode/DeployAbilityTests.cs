using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // 持ち主は視点も CharacterController も無い GameObject にして、狙いを持ち主の正面、足元を Transform の位置に固定する。
    public sealed class DeployAbilityTests
    {
        private const string Kind = "shield";
        private const string OwnerName = "DeployAbilityTests.Owner";
        private const float Range = 4f;

        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private ShieldDeployDefinition _definition;
        private GameObject _owner;
        private AbilityHost _host;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<ShieldDeployDefinition>();
            _definition.name = "DeployShield";
            _definition.Id = Kind;
            _definition.MaxCharges = 2;
            _definition.RechargeSeconds = 10f;
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            _definition.MaxActive = 2;
            _definition.Overflow = DeployOverflow.ReplaceOldest;
            _definition.MaxRange = Range;
            _created.Add(_definition);

            _owner = new GameObject(OwnerName);
            _owner.transform.position = new Vector3(0f, 1f, 0f);
            _created.Add(_owner);
            _host = _owner.AddComponent<AbilityHost>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var deployable in Object.FindObjectsByType<Deployable>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(deployable.gameObject);
            }

            foreach (var preview in Object.FindObjectsByType<DeployPreview>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(preview.gameObject);
            }

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
        public void ATapPlacesOnFlatGroundInFront()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            Equip();

            Assert.That(_host.TryUse(0), Is.True);
            LogAssert.Expect(LogType.Log, DeployLog.Preview(OwnerName, Kind, DeployVerdict.Ok));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DeployLog.Prefix(OwnerName, Kind) + " placed serial=1 pos=(") + ".* active=1/2$"));
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1), "placing uses the charge");
            var placed = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(placed, Has.Count.EqualTo(1));
            Assert.That(Vector3.Distance(placed[0].transform.position, new Vector3(0f, 0f, Range)), Is.LessThan(0.01f));
            Assert.That(placed[0].Damageable.Health.Current, Is.EqualTo(_definition.MaxHealth));
            Assert.That(Previews(), Is.Empty, "the preview goes away");
        }

        [Test]
        public void HoldingTheButtonKeepsAimingWithAPreview()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            Equip();

            _host.TryUse(0);
            _host.Step(PlayerCommands.None, PlayerCommands.Ability1, 0.1f);
            _host.Step(PlayerCommands.None, PlayerCommands.Ability1, 0.1f);

            Assert.That(_host.IsRunning(0), Is.True);
            var previews = Previews();
            Assert.That(previews, Has.Count.EqualTo(1));
            Assert.That(previews[0].gameObject.activeSelf, Is.True);
            Assert.That(previews[0].IsValid, Is.True, "green over flat ground");
            Assert.That(previews[0].GetComponentsInChildren<Collider>(), Has.All.Matches<Collider>(c => !c.enabled), "the preview does not stop bullets");
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);

            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.EqualTo(1));
        }

        [Test]
        public void SteepGroundIsRefusedAndTheChargeComesBack()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, Range), Quaternion.Euler(45f, 0f, 0f));
            ramp.transform.localScale = new Vector3(4f, 0.2f, 4f);
            _created.Add(ramp);
            Equip();

            AssertRefused(DeployVerdict.TooSteep);
        }

        [Test]
        public void ALedgeIsRefusedAsUneven()
        {
            // 床は x = 0.5 で終わるので、中心は床に乗るが右の隅は宙に浮く。
            CreateFloor(new Vector3(-10f, 0f, 0f), new Vector3(21f, 1f, 40f));
            Equip();

            AssertRefused(DeployVerdict.Uneven);
        }

        [Test]
        public void SomethingInTheWayIsRefusedAsBlocked()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.transform.position = new Vector3(0.8f, 1.5f, Range);
            pole.transform.localScale = new Vector3(0.4f, 1f, 0.2f);
            _created.Add(pole);
            Equip();

            AssertRefused(DeployVerdict.Blocked);
        }

        [Test]
        public void NoGroundIsRefused()
        {
            Equip();

            AssertRefused(DeployVerdict.NoGround);
        }

        [Test]
        public void GoingOverTheLimitRemovesTheOldest()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            Equip();

            var first = PlaceAt(-3f);
            PlaceAt(0f);
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerName, Kind, first.Serial, DeployRemoval.Replaced));
            var third = PlaceAt(3f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(first == null, Is.True, "the oldest is destroyed");
            Assert.That(active, Has.Count.EqualTo(2));
            Assert.That(active[1], Is.SameAs(third));
            Assert.That(third.Serial, Is.EqualTo(3));
        }

        [Test]
        public void RefuseOverflowKeepsTheChargeWhenFull()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            _definition.MaxActive = 1;
            _definition.Overflow = DeployOverflow.Refuse;
            Equip();
            PlaceAt(-3f);
            _host.ResetSlots();

            LogAssert.Expect(LogType.Log, DeployLog.LimitReached(OwnerName, Kind, 1, 1));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.UseBlock(0), Is.EqualTo(AbilityUseBlock.Refused));
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
        }

        [Test]
        public void StoppingWhileAimingGivesTheChargeBack()
        {
            CreateFloor(Vector3.zero, new Vector3(40f, 1f, 40f));
            Equip();
            _host.TryUse(0);
            _host.Step(PlayerCommands.None, PlayerCommands.Ability1, 0.1f);

            LogAssert.Expect(LogType.Log, DeployLog.Cancelled(OwnerName, Kind, 2, 2));
            _host.Configure(GameRules.Default);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
            Assert.That(Previews(), Is.Empty);
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
        }

        [Test]
        public void InvalidDeploySettingsAreRejectedWhenEquipping()
        {
            _definition.MaxRange = 0f;

            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));
            Assert.That(_host.Definition(0), Is.Null);
        }

        private void Equip()
        {
            _host.Configure(_on);
            _host.Equip(0, _definition);
            Physics.SyncTransforms();
        }

        private void AssertRefused(DeployVerdict verdict)
        {
            Assert.That(_host.TryUse(0), Is.True);
            LogAssert.Expect(LogType.Log, DeployLog.Preview(OwnerName, Kind, verdict));
            LogAssert.Expect(LogType.Log, DeployLog.Refused(OwnerName, Kind, verdict, 2, 2));
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2), "the charge comes back");
            Assert.That(_host.Slot(0).CooldownRemaining, Is.Zero);
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
            Assert.That(Object.FindObjectsByType<Deployable>(FindObjectsInactive.Include), Is.Empty);
        }

        private Deployable PlaceAt(float x)
        {
            _owner.transform.position = new Vector3(x, 1f, 0f);
            Physics.SyncTransforms();
            _host.ResetSlots();
            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active, Is.Not.Empty, $"nothing was placed from x={x}");
            Physics.SyncTransforms();
            return active[active.Count - 1];
        }

        private void CreateFloor(Vector3 top, Vector3 size)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = top + new Vector3(0f, -size.y / 2f, 0f);
            floor.transform.localScale = size;
            _created.Add(floor);
        }

        private static List<DeployPreview> Previews()
        {
            return new List<DeployPreview>(Object.FindObjectsByType<DeployPreview>(FindObjectsInactive.Include));
        }
    }
}
