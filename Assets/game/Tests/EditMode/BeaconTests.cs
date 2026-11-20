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
    // ビーコンは原点に +Z 向きで置き、持ち主は Alpha の陣営に入れる。
    public sealed class BeaconTests
    {
        private const string Kind = "beacon";
        private const string OwnerId = "Owner";

        private readonly List<Object> _created = new List<Object>();
        private BeaconDeployDefinition _definition;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            _definition.name = "DeployBeacon";
            _definition.Id = Kind;
            _created.Add(_definition);

            _owner = new GameObject(OwnerId);
            _owner.transform.position = new Vector3(0f, 1f, -3f);
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            _created.Add(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var deployable in Object.FindObjectsByType<Deployable>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(deployable.gameObject);
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
        public void ThePlaceholderIsAPoleWithALampThatAvoidsTheNavMeshButIsNotCover()
        {
            var beacon = Place();
            var spec = _definition.ToDeploySpec();

            Assert.That(beacon.transform.Find(BeaconDeployDefinition.PoleName), Is.Not.Null);
            Assert.That(beacon.transform.Find(BeaconDeployDefinition.LampName), Is.Not.Null);
            Assert.That(beacon.Damageable.TryGetBounds(out var bounds), Is.True, "it can be shot");
            Assert.That(bounds.max.y, Is.EqualTo(spec.Height).Within(1e-3f));
            Assert.That(bounds.size.x, Is.LessThanOrEqualTo(spec.Width + 1e-3f));
            Assert.That(beacon.GetComponent<NavMeshObstacle>(), Is.Not.Null);
            Assert.That(beacon.GetComponentInChildren<Shooter.Runtime.CoverPoint>(), Is.Null);
            Assert.That(beacon.Damageable.Health.Current, Is.EqualTo(BeaconSpec.DefaultMaxHealth));
            Assert.That(spec.MaxActive, Is.EqualTo(BeaconSpec.DefaultMaxActive));
            Assert.That(beacon.IsArmed, Is.True);
            Assert.That(beacon.UsesLeft, Is.EqualTo(BeaconSpec.DefaultUses));
            Assert.That(SpawnBeacon.Armed, Has.Member(beacon));
            Assert.Throws<System.InvalidOperationException>(() => beacon.Arm(BeaconSpec.Default));
        }

        [Test]
        public void ThePrefabReplacesThePlaceholder()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            art.name = "BeaconArt";
            art.transform.SetParent(holder.transform, false);
            _definition.Prefab = art;

            var beacon = Place();

            Assert.That(beacon.transform.Find(BeaconDeployDefinition.PoleName), Is.Null);
            Assert.That(beacon.IsArmed, Is.True);
        }

        [Test]
        public void TheNameKeyDefaultsToTheBeaconKeyAndABlankOneIsEmpty()
        {
            Assert.That(_definition.NameKey, Is.EqualTo("deployable.beacon"));
            Assert.That(_definition.DisplayNameKey, Is.EqualTo(BeaconSpec.DefaultNameKey));

            _definition.NameKey = " deployable.squadBeacon ";
            Assert.That(_definition.DisplayNameKey, Is.EqualTo("deployable.squadBeacon"));

            _definition.NameKey = " ";
            Assert.That(_definition.DisplayNameKey, Is.Empty);

            _definition.NameKey = null;
            Assert.That(_definition.DisplayNameKey, Is.Empty);
            Assert.That(_definition.Id, Is.EqualTo(Kind));
        }

        [Test]
        public void ArmingLogsTheUsesAndTheSide()
        {
            LogAssert.Expect(LogType.Log, BeaconLog.Armed(OwnerId, Kind, 1, BeaconSpec.DefaultUses, Side.Alpha));
            Place();
        }

        [Test]
        public void EachUseTakesOneAndSpawnsInFrontOfTheBeacon()
        {
            _definition.Uses = 3;
            _definition.SpawnDistance = 2f;
            var beacon = Place(rotation: Quaternion.Euler(0f, 90f, 0f));

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(BeaconLog.Prefix(OwnerId, Kind, 1) + " used by=" + OwnerId + " at=") + ".* uses=2/3$"));
            Assert.That(beacon.TryUse(_owner, OwnerId, out var spawn), Is.True);

            Assert.That(beacon.UsesLeft, Is.EqualTo(2));
            Assert.That(Vector3.Distance(spawn.position, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Angle(spawn.forward, Vector3.right), Is.LessThan(0.01f), "the spawned body faces the way the beacon faces");

            Assert.That(beacon.TryUse(_owner, OwnerId, out _), Is.True);
            Assert.That(beacon.UsesLeft, Is.EqualTo(1));
            Assert.That(beacon.IsArmed, Is.True);
        }

        [Test]
        public void TheLastUseRemovesIt()
        {
            _definition.Uses = 1;
            var beacon = Place();
            var deployable = beacon.Deployable;

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(BeaconLog.Prefix(OwnerId, Kind, 1) + " used by=" + OwnerId + " ") + ".* uses=0/1$"));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Expended));
            Assert.That(beacon.TryUse(_owner, OwnerId, out _), Is.True);

            Assert.That(deployable == null, Is.True);
            Assert.That(SpawnBeacon.Armed, Has.No.Member(beacon));
            Assert.That(SpawnBeacon.TryFindFor(_owner, out _), Is.False);
        }

        [Test]
        public void TheOwnersSquadCanUseItButTheOtherSideCannot()
        {
            var beacon = Place();
            var mate = CreateMember("Mate", Side.Alpha);
            var foe = CreateMember("Foe", Side.Bravo);
            var loner = new GameObject("Loner");
            _created.Add(loner);

            Assert.That(beacon.CanBeUsedBy(_owner), Is.True);
            Assert.That(beacon.CanBeUsedBy(mate), Is.True);
            Assert.That(beacon.CanBeUsedBy(foe), Is.False);
            Assert.That(beacon.CanBeUsedBy(loner), Is.False);
            Assert.That(beacon.CanBeUsedBy(null), Is.False);

            Assert.That(beacon.TryUse(foe, "Foe", out _), Is.False);
            Assert.That(beacon.UsesLeft, Is.EqualTo(BeaconSpec.DefaultUses), "a refused use takes nothing");

            Assert.That(SpawnBeacon.TryFindFor(mate, out var found), Is.True);
            Assert.That(found, Is.SameAs(beacon));
            Assert.That(SpawnBeacon.TryFindFor(foe, out var none), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void WithoutASideOnlyTheOwnerCanUseIt()
        {
            var beacon = PlaceWith(1, Vector3.zero, Quaternion.identity, null);
            var other = CreateMember("Other", Side.Alpha);

            Assert.That(beacon.CanBeUsedBy(_owner), Is.True);
            Assert.That(beacon.CanBeUsedBy(other), Is.False);
        }

        [Test]
        public void TheNewestUsableBeaconIsFound()
        {
            var older = Place(1);
            var newer = Place(2, new Vector3(5f, 0f, 0f));

            Assert.That(SpawnBeacon.TryFindFor(_owner, out var found), Is.True);
            Assert.That(found, Is.SameAs(newer));

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 2, DeployRemoval.Destroyed));
            newer.Deployable.Remove(DeployRemoval.Destroyed);

            Assert.That(SpawnBeacon.TryFindFor(_owner, out found), Is.True);
            Assert.That(found, Is.SameAs(older));
        }

        [Test]
        public void EnemyBulletsBreakItAndTheOwnersPassThrough()
        {
            var beacon = Place();
            var deployable = beacon.Deployable;
            var aim = beacon.Damageable.AimPoint;

            var ownShot = Hitscan.Fire(aim + new Vector3(0f, 0f, -3f), Vector3.forward, 6f, 50, DamageFalloff.None, _owner.transform);
            Assert.That(ownShot.Damage, Is.Zero);
            Assert.That(beacon.Damageable.Health.Current, Is.EqualTo(BeaconSpec.DefaultMaxHealth), "the owner's bullets go through");

            var shooter = CreateMember("Shooter", Side.Bravo);
            shooter.transform.position = aim + new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Destroyed));
            for (var i = 0; i < 20 && deployable != null && !deployable.IsRemoved; i++)
            {
                Hitscan.Fire(shooter.transform.position, Vector3.back, 20f, 30, DamageFalloff.None, shooter.transform);
            }

            Assert.That(deployable == null, Is.True);
            Assert.That(SpawnBeacon.Armed, Has.No.Member(beacon));
            Assert.That(SpawnBeacon.TryFindFor(_owner, out _), Is.False, "a broken beacon is no longer a place to respawn");
        }

        [Test]
        public void InvalidValuesAreRejectedWhenEquipping()
        {
            _definition.Uses = 0;
            var host = _owner.AddComponent<AbilityHost>();

            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.Uses = 1;
            _definition.SpawnDistance = -1f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));
            Assert.That(host.Definition(0), Is.Null);
        }

        [Test]
        public void PlacingWithTheAbilityArmsTheBeacon()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _created.Add(floor);
            _owner.transform.position = new Vector3(0f, 1f, 0f);
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            var host = _owner.AddComponent<AbilityHost>();
            host.Configure(GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On)));
            host.Equip(0, _definition);
            Physics.SyncTransforms();

            Assert.That(host.TryUse(0), Is.True);
            LogAssert.Expect(LogType.Log, BeaconLog.Armed(OwnerId, Kind, 1, BeaconSpec.DefaultUses, Side.Alpha));
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DeployLog.Prefix(OwnerId, Kind) + " placed serial=1 ") + ".* active=1/1$"));
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active.Count, Is.EqualTo(1));
            var beacon = active[0].GetComponent<SpawnBeacon>();
            Assert.That(beacon.IsArmed, Is.True);
            Assert.That(beacon.Side, Is.EqualTo(Side.Alpha));
            Assert.That(SpawnBeacon.TryFindFor(_owner, out var found), Is.True);
            Assert.That(found, Is.SameAs(beacon));
        }

        private SpawnBeacon Place(int serial = 1, Vector3? position = null, Quaternion? rotation = null)
        {
            return PlaceWith(serial, position ?? Vector3.zero, rotation ?? Quaternion.identity, Side.Alpha);
        }

        private SpawnBeacon PlaceWith(int serial, Vector3 position, Quaternion rotation, Side? side)
        {
            var spec = _definition.ToDeploySpec();
            var deployable = _definition.CreateDeployed(new Pose(position, rotation), spec);
            deployable.Initialize(_owner, OwnerId, Kind, serial, side, spec.MaxHealth);
            var beacon = deployable.GetComponent<SpawnBeacon>();
            beacon.Arm(_definition.ToBeaconSpec());
            Physics.SyncTransforms();
            return beacon;
        }

        private GameObject CreateMember(string name, Side side)
        {
            var member = new GameObject(name);
            member.AddComponent<SideMember>().Side = side;
            _created.Add(member);
            return member;
        }
    }
}
