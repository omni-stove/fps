using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 持ち主は視点も CharacterController も無い GameObject にして、狙いを持ち主の正面、体の中心を Transform の位置に固定する。
    // 正面 (+Z) に壁、右 (+X) に標的を置き、向きを変えてそれぞれに貼る。Edit Mode では Update が走らないので、能力は AbilityHost.Step で進める。
    public sealed class RemoteChargeTests
    {
        private const string Kind = "remote-charge";
        private const string OwnerId = "Owner";
        private const float TapSeconds = 0.25f;

        // 先に貼った C4 に狙いが当たらないよう、壁の別の所を狙う向き。
        private static readonly Vector3 _asideOnTheWall = new Vector3(0.5f, 0f, 1f);
        private static readonly Vector3 _otherSideOnTheWall = new Vector3(-0.5f, 0f, 1f);

        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private RemoteChargeDeployDefinition _definition;
        private GameObject _owner;
        private AbilityHost _host;
        private GameObject _wall;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _definition = ScriptableObject.CreateInstance<RemoteChargeDeployDefinition>();
            _definition.name = "DeployRemoteCharge";
            _definition.Id = Kind;
            _definition.MaxCharges = 3;
            _definition.RechargeSeconds = 0f;
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            _definition.TapSeconds = TapSeconds;
            _created.Add(_definition);

            _owner = new GameObject(OwnerId);
            _owner.transform.position = new Vector3(0f, 1f, 0f);
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            _created.Add(_owner);
            _host = _owner.AddComponent<AbilityHost>();
            _host.Configure(_on);

            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Wall";
            _wall.transform.position = new Vector3(0f, 1f, 1.5f);
            _wall.transform.localScale = new Vector3(4f, 3f, 1f);
            _created.Add(_wall);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
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
        public void ATapSticksItFlatOnTheWallItIsAimedAt()
        {
            _host.Equip(0, _definition);

            LogAssert.Expect(LogType.Log, RemoteChargeLog.Attached(OwnerId, Kind, 1, "Wall"));
            var charge = Tap();

            Assert.That(charge, Is.Not.Null);
            Assert.That(charge.transform.position.z, Is.EqualTo(1f).Within(1e-3f), "on the wall's face");
            Assert.That(Vector3.Angle(charge.transform.up, Vector3.back), Is.LessThan(0.1f), "lying flat against the wall");
            Assert.That(charge.Anchor, Is.SameAs(_wall.transform));
            Assert.That(charge.transform.Find(RemoteChargeDeployDefinition.BodyName), Is.Not.Null);
            Assert.That(charge.GetComponent<NavMeshObstacle>(), Is.Null);
            Assert.That(charge.Deployable.Damageable.Health.Current, Is.EqualTo(RemoteChargeSpec.DefaultMaxHealth));
            Assert.That(charge.Deployable.Side, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void ThePrefabReplacesThePlaceholder()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            art.name = "C4Art";
            art.transform.SetParent(holder.transform, false);
            _definition.Prefab = art;
            _host.Equip(0, _definition);

            var charge = Tap();

            Assert.That(charge.transform.Find(RemoteChargeDeployDefinition.BodyName), Is.Null);
            Assert.That(charge.IsPlaced, Is.True);
        }

        [Test]
        public void ItFollowsTheTargetItIsStuckTo()
        {
            var target = CreateTarget(new Vector3(2f, 1f, 0f));
            _host.Equip(0, _definition);
            Face(Vector3.right);

            var charge = Tap();
            var offset = charge.transform.position - target.transform.position;
            target.transform.position += new Vector3(0f, 0f, -3f);
            charge.Follow();

            Assert.That(charge.Anchor, Is.SameAs(target.transform));
            Assert.That(Vector3.Distance(charge.transform.position - target.transform.position, offset), Is.LessThan(1e-3f));
        }

        [Test]
        public void TappingAgainBlowsUpEveryChargeAtOnceWithoutUsingACharge()
        {
            var target = CreateTarget(new Vector3(2f, 1f, 0f));
            _host.Equip(0, _definition);
            var onWall = Tap().Deployable;
            Face(Vector3.right);
            var onTarget = HoldAndRelease().Deployable;
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.EqualTo(2));
            var charges = _host.Slot(0).Charges;

            LogAssert.Expect(LogType.Log, RemoteChargeLog.Detonating(OwnerId, Kind, 2));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Detonated));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 2, DeployRemoval.Detonated));
            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(onWall == null, Is.True);
            Assert.That(onTarget == null, Is.True);
            Assert.That(target.IsDead, Is.True);
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(charges), "detonating does not use a charge");
            Assert.That(_host.IsRunning(0), Is.False);
        }

        [Test]
        public void HoldingPlacesAnotherInsteadOfDetonating()
        {
            _host.Equip(0, _definition);
            var first = Tap();
            Face(_asideOnTheWall);

            var second = HoldAndRelease();

            Assert.That(first.IsPlaced, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.EqualTo(2));
        }

        [Test]
        public void PlacingPastTheLimitIsRefusedButATapStillDetonates()
        {
            _definition.MaxActive = 2;
            _host.Equip(0, _definition);
            Tap();
            Face(_asideOnTheWall);
            HoldAndRelease();
            Face(_otherSideOnTheWall);
            var charges = _host.Slot(0).Charges;

            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, AbilityHost.CommandFor(0), TapSeconds + 0.05f);
            LogAssert.Expect(LogType.Log, DeployLog.LimitReached(OwnerId, Kind, 2, 2));
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.EqualTo(2));
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(charges), "the refused charge is given back");

            LogAssert.Expect(LogType.Log, RemoteChargeLog.Detonating(OwnerId, Kind, 2));
            _host.TryUse(0);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
        }

        [Test]
        public void WithNoChargesLeftAPressDetonatesAtOnce()
        {
            _definition.MaxCharges = 1;
            _host.Equip(0, _definition);
            Tap();
            Assert.That(_host.Slot(0).Charges, Is.Zero);

            LogAssert.Expect(LogType.Log, RemoteChargeLog.Detonating(OwnerId, Kind, 1));
            LogAssert.Expect(LogType.Log, AbilityHost.FormatFollowUpLog(0, _definition));
            Assert.That(_host.TryUse(0), Is.True);

            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.Zero);
        }

        [Test]
        public void WithNothingPlacedAndNoChargesThePressIsRejected()
        {
            _definition.MaxCharges = 1;
            _host.Equip(0, _definition);
            Tap();
            _host.TryUse(0);

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape("[Ability] slot=1 id=" + Kind + " rejected reason=NoCharges")));
            Assert.That(_host.TryUse(0), Is.False);
        }

        [Test]
        public void TooFarAwayItCannotBePlaced()
        {
            _wall.transform.position = new Vector3(0f, 1f, 10f);
            Physics.SyncTransforms();
            _host.Equip(0, _definition);

            LogAssert.Expect(LogType.Log, DeployLog.Preview(OwnerId, Kind, DeployVerdict.NoGround));
            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(3));
        }

        [Test]
        public void DetonatingIsOfferedOnlyWhileChargesArePlaced()
        {
            _host.Equip(0, _definition);
            Assert.That(_host.TryGetFollowUp(0, out _), Is.False);

            Tap();
            Face(_asideOnTheWall);
            HoldAndRelease();

            Assert.That(_host.TryGetFollowUp(0, out var followUp), Is.True);
            Assert.That(followUp, Is.EqualTo(AbilityFollowUp.Detonate(Kind, RemoteChargeSpec.DefaultNameKey, 2)));
            Assert.That(followUp.Kind, Is.EqualTo(AbilityFollowUpKind.Detonate));
            Assert.That(followUp.TargetNameKey, Is.EqualTo(RemoteChargeSpec.DefaultNameKey));

            _host.Configure(GameRules.Default);
            Assert.That(_host.TryGetFollowUp(0, out _), Is.False, "nothing is offered while abilities are off");
        }

        [Test]
        public void TheHostIsHeardWhenThePlacedChargesChange()
        {
            _host.Equip(0, _definition);
            var heard = 0;
            _host.Changed += _ => heard++;

            var placed = Tap();
            Assert.That(heard, Is.GreaterThan(0));

            heard = 0;
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            Assert.That(heard, Is.Zero, "nothing moved");

            placed.Deployable.Remove(DeployRemoval.Discarded);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            Assert.That(heard, Is.EqualTo(1), "a charge removed outside the host changes what a press does");
        }

        [Test]
        public void TheDetonateFollowUpCarriesTheIdWithoutANameKey()
        {
            _definition.NameKey = " ";

            Assert.That(_definition.DisplayNameKey, Is.Empty);
            var followUp = AbilityFollowUp.Detonate(Kind, _definition.DisplayNameKey, 1);
            Assert.That(followUp.TargetId, Is.EqualTo(Kind));
            Assert.That(followUp.TargetNameKey, Is.Empty);
        }

        [Test]
        public void TheKillFeedNamesTheCharge()
        {
            var target = CreateTarget(new Vector3(2f, 1f, 0f));
            _host.Equip(0, _definition);
            var kills = new List<KillFeedEntry>();
            CombatEvents.Killed += kills.Add;
            Face(Vector3.right);
            Tap();

            _host.TryUse(0);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            Assert.That(target.IsDead, Is.True);
            Assert.That(kills.Count, Is.EqualTo(1));
            Assert.That(kills[0].WeaponId, Is.EqualTo(Kind));
            Assert.That(RemoteChargeDeployDefinition.TryFind(CombatEvents.OfflineObject(kills[0].KillerId), kills[0].WeaponId, out var found), Is.True);
            Assert.That(found, Is.SameAs(_definition));
            Assert.That(found.DisplayNameKey, Is.EqualTo(RemoteChargeSpec.DefaultNameKey));
        }

        [Test]
        public void ABlankNameKeyIsEmpty()
        {
            _definition.NameKey = " ";

            Assert.That(_definition.DisplayNameKey, Is.Empty);
            Assert.That(_definition.Id, Is.EqualTo(Kind));
        }

        [Test]
        public void InvalidValuesAreRejectedWhenEquipping()
        {
            _definition.AttachedVolume = 2f;
            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));

            _definition.AttachedVolume = 1f;
            _definition.FullDamageRadius = _definition.DamageRadius;
            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));

            _definition.FullDamageRadius = 0f;
            _definition.TapSeconds = -1f;
            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));
            Assert.That(_host.Definition(0), Is.Null);
        }

        [Test]
        public void ItDoesNotStickToAnotherPlacedThing()
        {
            _host.Equip(0, _definition);
            var first = Tap();
            first.transform.position = new Vector3(0f, 1f, 0.9f);
            first.transform.localScale = new Vector3(10f, 10f, 10f);
            Physics.SyncTransforms();

            Assert.That(
                _definition.Survey(_owner.transform, _owner.transform.position, Vector3.forward, _definition.ToDeploySpec(), out _, out var surface),
                Is.EqualTo(DeployVerdict.Blocked));
            Assert.That(surface, Is.Null);
        }

        // 置いた物が無いので、押してすぐ離すと狙った所に置く。
        private RemoteCharge Tap()
        {
            var before = new HashSet<Deployable>(DeployOwner.GetOrAdd(_owner).Active(Kind));
            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            return Newest(before);
        }

        // 置いた物があるので、短く押すと起爆になる。起爆の時間を過ぎるまで押してから離して置く。
        private RemoteCharge HoldAndRelease()
        {
            var before = new HashSet<Deployable>(DeployOwner.GetOrAdd(_owner).Active(Kind));
            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, AbilityHost.CommandFor(0), TapSeconds + 0.05f);
            _host.Step(PlayerCommands.None, PlayerCommands.None, 0f);
            return Newest(before);
        }

        private RemoteCharge Newest(HashSet<Deployable> before)
        {
            foreach (var deployable in DeployOwner.GetOrAdd(_owner).Active(Kind))
            {
                if (!before.Contains(deployable))
                {
                    Physics.SyncTransforms();
                    return deployable.GetComponent<RemoteCharge>();
                }
            }

            return null;
        }

        private void Face(Vector3 direction)
        {
            _owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private Damageable CreateTarget(Vector3 center)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Target";
            body.transform.position = center;
            body.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("Target", 100);
            _created.Add(body);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
