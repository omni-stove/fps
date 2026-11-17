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
    // Edit Mode では Update が走らないので、Mine.Tick を手で進める。
    // 地雷は原点に置き、持ち主は Alpha の陣営で、地雷から離れた所に立つ。
    public sealed class MineTests
    {
        private const string Kind = "mine";
        private const string OwnerId = "Owner";
        private const float ArmSeconds = 1f;

        private readonly List<Object> _created = new List<Object>();
        private MineDeployDefinition _definition;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _definition = ScriptableObject.CreateInstance<MineDeployDefinition>();
            _definition.name = "DeployMine";
            _definition.Id = Kind;
            _definition.ArmSeconds = ArmSeconds;
            _created.Add(_definition);

            _owner = new GameObject(OwnerId);
            _owner.transform.position = new Vector3(0f, 1f, -20f);
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            _created.Add(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
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
        public void ThePlaceholderIsAFlatDiscThatEnemiesWalkOver()
        {
            var mine = Place();
            var spec = _definition.ToDeploySpec();

            Assert.That(mine.transform.Find(MineDeployDefinition.BodyName), Is.Not.Null);
            Assert.That(mine.Deployable.Damageable.TryGetBounds(out var bounds), Is.True, "it can be shot");
            Assert.That(bounds.max.y, Is.EqualTo(spec.Height).Within(1e-3f));
            Assert.That(mine.GetComponent<NavMeshObstacle>(), Is.Null, "enemies must be able to step on it");
            Assert.That(mine.Deployable.Damageable.Health.Current, Is.EqualTo(MineSpec.DefaultMaxHealth));
            Assert.Throws<System.InvalidOperationException>(() => mine.Arm(MineSpec.Default, null, 1f));
        }

        [Test]
        public void ThePrefabReplacesThePlaceholder()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            art.name = "MineArt";
            art.transform.SetParent(holder.transform, false);
            _definition.Prefab = art;

            var mine = Place();

            Assert.That(mine.transform.Find(MineDeployDefinition.BodyName), Is.Null);
            Assert.That(mine.IsPlaced, Is.True);
        }

        [Test]
        public void AnEnemyInRangeDoesNotSetItOffBeforeItIsArmed()
        {
            var mine = Place();
            var enemy = CreateBody("Enemy", Side.Bravo, Vector3.zero, 100);

            mine.Tick(ArmSeconds * 0.9f);

            Assert.That(mine.IsArmed, Is.False);
            Assert.That(mine.IsPlaced, Is.True);
            Assert.That(enemy.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ItArmsAfterTheArmTime()
        {
            var mine = Place();

            mine.Tick(ArmSeconds * 0.5f);
            Assert.That(mine.IsArmed, Is.False);

            LogAssert.Expect(LogType.Log, MineLog.Armed(OwnerId, Kind, 1));
            mine.Tick(ArmSeconds * 0.5f);

            Assert.That(mine.IsArmed, Is.True);
        }

        [Test]
        public void AnEnemyThatStepsOnItIsBlownUp()
        {
            var mine = Place();
            var deployable = mine.Deployable;
            mine.Tick(ArmSeconds);
            var enemy = CreateBody("Enemy", Side.Bravo, Vector3.zero, 100);

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(MineLog.Prefix(OwnerId, Kind, 1) + " triggered by=Enemy ")));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Detonated));
            mine.Tick(0.1f);

            Assert.That(deployable == null, Is.True, "the mine is used up");
            Assert.That(enemy.IsDead, Is.True);
            Assert.That(DeployOwner.GetOrAdd(_owner).ActiveCount(Kind), Is.Zero);
        }

        [Test]
        public void TheOwnerAndAlliesDoNotSetItOff()
        {
            var mine = Place();
            mine.Tick(ArmSeconds);
            var ownerBody = CreateBody("OwnerBody", null, Vector3.zero, 100);
            ownerBody.transform.SetParent(_owner.transform, true);
            var ally = CreateBody("Ally", Side.Alpha, new Vector3(0.3f, 0f, 0f), 100);
            Physics.SyncTransforms();

            mine.Tick(0.1f);
            mine.Tick(0.1f);

            Assert.That(mine.IsArmed, Is.True);
            Assert.That(ownerBody.Health.Current, Is.EqualTo(100));
            Assert.That(ally.Health.Current, Is.EqualTo(100));
            Assert.That(mine.IsTriggeredBy(ownerBody.gameObject), Is.False);
            Assert.That(mine.IsTriggeredBy(ally.gameObject), Is.False);
        }

        [Test]
        public void AnEnemyOutsideTheTriggerRadiusDoesNotSetItOff()
        {
            _definition.TriggerRadius = 1f;
            var mine = Place();
            mine.Tick(ArmSeconds);
            var enemy = CreateBody("Enemy", Side.Bravo, new Vector3(3f, 0f, 0f), 100);

            mine.Tick(0.1f);

            Assert.That(mine.IsArmed, Is.True);
            Assert.That(enemy.Health.Current, Is.EqualTo(100));
            Assert.That(mine.IsTriggeredBy(enemy.gameObject), Is.True);
        }

        [Test]
        public void TheKillFeedNamesTheMine()
        {
            _owner.AddComponent<AbilityHost>().Equip(0, _definition);
            var kills = new List<KillFeedEntry>();
            CombatEvents.Killed += kills.Add;
            var mine = Place();
            mine.Tick(ArmSeconds);
            CreateBody("Enemy", Side.Bravo, Vector3.zero, 100);

            mine.Tick(0.1f);

            Assert.That(kills.Count, Is.EqualTo(1));
            Assert.That(kills[0].WeaponId, Is.EqualTo(Kind));
            Assert.That(MineDeployDefinition.TryFind(CombatEvents.OfflineObject(kills[0].KillerId), kills[0].WeaponId, out var found), Is.True);
            Assert.That(found, Is.SameAs(_definition));
            Assert.That(found.DisplayNameKey, Is.EqualTo(MineSpec.DefaultNameKey));
            Assert.That(MineDeployDefinition.TryFind(CombatEvents.OfflineObject(kills[0].KillerId), "other", out found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void ABlankNameKeyIsEmpty()
        {
            _definition.NameKey = " ";

            Assert.That(_definition.DisplayNameKey, Is.Empty);
            Assert.That(_definition.Id, Is.EqualTo(Kind));
        }

        [Test]
        public void APlacedMineOffersNoFollowUp()
        {
            var host = _owner.AddComponent<AbilityHost>();
            host.Configure(GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On)));
            host.Equip(0, _definition);
            Place();

            Assert.That(_definition.DisplayNameKey, Is.EqualTo(MineSpec.DefaultNameKey));
            Assert.That(host.TryGetFollowUp(0, out _), Is.False, "a mine is not detonated by hand");
        }

        [Test]
        public void InvalidValuesAreRejectedWhenEquipping()
        {
            var host = _owner.AddComponent<AbilityHost>();

            _definition.ArmedVolume = 2f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.ArmedVolume = 1f;
            _definition.FullDamageRadius = _definition.DamageRadius;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.FullDamageRadius = 0f;
            _definition.TriggerRadius = 0f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));
            Assert.That(host.Definition(0), Is.Null);
        }

        [Test]
        public void PlacingWithTheAbilityStartsArmingTheMine()
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
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(MineLog.Prefix(OwnerId, Kind, 1) + " arming seconds=1.0 ")));
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active.Count, Is.EqualTo(1));
            var mine = active[0].GetComponent<Mine>();
            Assert.That(mine.IsPlaced, Is.True);
            Assert.That(mine.IsArmed, Is.False);
            Assert.That(mine.Side, Is.EqualTo(Side.Alpha));
        }

        private Mine Place(int serial = 1)
        {
            var spec = _definition.ToDeploySpec();
            var owner = DeployOwner.GetOrAdd(_owner);
            var deployable = _definition.CreateDeployed(new Pose(Vector3.zero, Quaternion.identity), spec);
            deployable.Initialize(_owner, OwnerId, Kind, serial, Side.Alpha, spec.MaxHealth);
            owner.Add(Kind, spec, deployable);
            var mine = deployable.GetComponent<Mine>();
            mine.Arm(_definition.ToMineSpec(), _definition.ArmedSound, _definition.ArmedVolume);
            Physics.SyncTransforms();
            return mine;
        }

        private Damageable CreateBody(string name, Side? side, Vector3 ground, int health)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            body.transform.position = ground + new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
            if (side.HasValue)
            {
                body.AddComponent<SideMember>().Side = side.Value;
            }

            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, health);
            _created.Add(body);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
