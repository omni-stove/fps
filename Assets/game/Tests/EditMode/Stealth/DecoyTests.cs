using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、Decoy.Tick と敵の知覚 (EnemyAgent.Perceive) を手で進める。
    // 囮は原点に +Z 向きで置き、持ち主は Alpha の陣営と発見ゲージ On のルールを持つ。敵は +Z の先から囮の方を向いて立つ。
    public sealed class DecoyTests
    {
        private const string Kind = "decoy";
        private const string OwnerId = "Owner";
        private const string EnemyId = "Guard";

        private static readonly Vector3 EnemyGround = new Vector3(0f, 0f, 6f);

        private readonly List<Object> _created = new List<Object>();
        private DecoyDeployDefinition _definition;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<DecoyDeployDefinition>();
            _definition.name = "DeployDecoy";
            _definition.Id = Kind;
            _created.Add(_definition);

            _owner = new GameObject(OwnerId);
            _owner.transform.position = new Vector3(0f, 1f, -3f);
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            _owner.AddComponent<StealthTarget>().Rule = Stealth.On;
            _created.Add(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            DecoyPerception.DetachAll();
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
        public void ThePlaceholderIsAPersonShapeThatAvoidsTheNavMeshButIsNotCover()
        {
            var decoy = Place();
            var spec = _definition.ToDeploySpec();

            Assert.That(decoy.transform.Find(DecoyDeployDefinition.BodyName), Is.Not.Null);
            Assert.That(decoy.transform.Find(DecoyDeployDefinition.HeadName), Is.Not.Null);
            Assert.That(decoy.Damageable.TryGetBounds(out var bounds), Is.True, "both parts can be seen and shot");
            Assert.That(bounds.max.y, Is.EqualTo(spec.Height).Within(1e-3f));
            Assert.That(bounds.size.x, Is.LessThanOrEqualTo(spec.Width + 1e-3f));
            Assert.That(decoy.GetComponent<NavMeshObstacle>(), Is.Not.Null);
            Assert.That(decoy.GetComponentInChildren<Shooter.Runtime.CoverPoint>(), Is.Null);
            Assert.That(decoy.Damageable.Health.Current, Is.EqualTo(DecoySpec.DefaultMaxHealth));
            Assert.That(decoy.IsArmed, Is.True);
            Assert.That(Decoy.Armed, Has.Member(decoy));
            Assert.Throws<System.InvalidOperationException>(() => decoy.Arm(DecoySpec.Default, null, 1f));
        }

        [Test]
        public void ThePrefabReplacesThePlaceholder()
        {
            var holder = new GameObject("ArtHolder");
            holder.transform.position = new Vector3(0f, -100f, 0f);
            _created.Add(holder);
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            art.name = "DecoyArt";
            art.transform.SetParent(holder.transform, false);
            _definition.Prefab = art;

            var decoy = Place();

            Assert.That(decoy.transform.Find(DecoyDeployDefinition.BodyName), Is.Null);
            Assert.That(decoy.IsArmed, Is.True);
        }

        [Test]
        public void ItTakesTheOwnersStealthRuleAndStandsUpright()
        {
            var decoy = Place();
            var stealth = decoy.GetComponent<StealthTarget>();

            Assert.That(stealth, Is.Not.Null);
            Assert.That(stealth.Rule, Is.EqualTo(Stealth.On));
            Assert.That(stealth.Posture, Is.EqualTo(AwarenessPosture.Standing));

            Object.DestroyImmediate(_owner.GetComponent<StealthTarget>());
            var other = Place(2);
            Assert.That(other.GetComponent<StealthTarget>().Rule, Is.EqualTo(Stealth.Off), "without the owner's rule, enemies engage at once as before");
        }

        [Test]
        public void ItCallsWhenPlacedAndExpiresAfterItsLifetime()
        {
            _definition.LifetimeSeconds = 1f;
            _definition.CallIntervalSeconds = 0.5f;
            var decoy = Place();
            var deployable = decoy.Deployable;

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DecoyLog.Prefix(OwnerId, Kind, 1) + " call at=")));
            decoy.Tick(0f);
            Assert.That(decoy.Calls, Is.EqualTo(1));

            decoy.Tick(0.6f);
            Assert.That(decoy.Calls, Is.EqualTo(2));
            Assert.That(deployable.IsRemoved, Is.False);

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Expired));
            decoy.Tick(0.6f);

            Assert.That(deployable == null, Is.True);
            Assert.That(Decoy.Armed, Has.No.Member(decoy));
        }

        [Test]
        public void EnemyBulletsBreakItAndTheOwnersPassThrough()
        {
            var decoy = Place();
            var deployable = decoy.Deployable;
            var aim = decoy.Damageable.AimPoint;

            var ownShot = Hitscan.Fire(aim + new Vector3(0f, 0f, -3f), Vector3.forward, 6f, 50, DamageFalloff.None, _owner.transform);
            Assert.That(ownShot.Damage, Is.Zero);
            Assert.That(decoy.Damageable.Health.Current, Is.EqualTo(DecoySpec.DefaultMaxHealth), "the owner's bullets go through");

            var shooter = new GameObject("Shooter");
            shooter.AddComponent<SideMember>().Side = Side.Bravo;
            shooter.transform.position = aim + new Vector3(0f, 0f, 5f);
            _created.Add(shooter);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Destroyed));
            for (var i = 0; i < 20 && deployable != null && !deployable.IsRemoved; i++)
            {
                Hitscan.Fire(shooter.transform.position, Vector3.back, 20f, 30, DamageFalloff.None, shooter.transform);
            }

            Assert.That(deployable == null, Is.True);
            Assert.That(Decoy.Armed, Has.No.Member(decoy));
        }

        [Test]
        public void PlacingADecoyAttachesTheLureToEveryEnemyOnce()
        {
            var agent = CreateEnemy(Side.Bravo);

            Place();
            Place(2);

            Assert.That(agent.PerceptionContributors.OfType<DecoyPerception>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyInitializedAfterAPlacedDecoyGetsTheLureUntilDetached()
        {
            Place();

            var late = CreateEnemy(Side.Bravo);
            Assert.That(late.PerceptionContributors.OfType<DecoyPerception>().Count(), Is.EqualTo(1));

            DecoyPerception.DetachAll();
            var afterDetach = CreateEnemy(Side.Bravo);
            Assert.That(afterDetach.PerceptionContributors.OfType<DecoyPerception>(), Is.Empty);
            Assert.That(late.PerceptionContributors.OfType<DecoyPerception>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyThatSeesTheDecoyTargetsItAndSearchesWhereItWasAfterItBreaks()
        {
            var agent = CreateEnemy(Side.Bravo);
            var decoy = Place();
            var ground = decoy.Damageable.GroundPoint;

            LogAssert.Expect(LogType.Log, DecoyLog.Seen(EnemyId, OwnerId, Kind, 1));
            var perception = agent.Perceive();

            Assert.That(perception.Target.HasValue, Is.True);
            Assert.That(perception.Target.Value.ToUnity(), Is.EqualTo(ground));
            Assert.That(perception.HasLineOfSight, Is.True);
            Assert.That(agent.PerceivedTarget, Is.SameAs(decoy.Damageable), "the enemy shoots at the decoy");
            Assert.That(agent.Target, Is.Null, "the enemy's own target is left alone");

            var decision = agent.Brain.Tick(0.1f, perception);
            Assert.That(agent.Brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.LookAt, Is.EqualTo(ground.ToNumerics()));

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Destroyed));
            decoy.Deployable.Remove(DeployRemoval.Destroyed);
            LogAssert.Expect(LogType.Log, DecoyLog.Lost(EnemyId, OwnerId, Kind, 1, ground.ToNumerics(), true));

            var spec = agent.Brain.Spec;
            for (var elapsed = 0f; elapsed <= spec.LoseSightSeconds + 0.2f; elapsed += 0.1f)
            {
                perception = agent.Perceive();
                Assert.That(perception.Target, Is.Null);
                Assert.That(agent.PerceivedTarget, Is.Null);
                decision = agent.Brain.Tick(0.1f, perception);
            }

            Assert.That(agent.Brain.State, Is.EqualTo(EnemyState.Search));
            Assert.That(decision.MoveTo, Is.EqualTo(ground.ToNumerics()), "it goes to where it last saw the decoy");
        }

        [Test]
        public void ASeenTargetNearerThanTheDecoyKeepsTheEnemysAttention()
        {
            var agent = CreateEnemy(Side.Bravo);
            Place();
            var player = CreateBody("Player", EnemyGround + new Vector3(2f, 0.5f, 0f), 100);
            agent.Target = player;

            var perception = agent.Perceive();

            Assert.That(perception.Target.Value.ToUnity(), Is.EqualTo(player.GroundPoint));
            Assert.That(agent.PerceivedTarget, Is.SameAs(player));
        }

        [Test]
        public void AnEnemyOfTheOwnersSideIsNotLured()
        {
            var agent = CreateEnemy(Side.Alpha);
            var decoy = Place();
            decoy.Tick(0f);

            var perception = agent.Perceive();

            Assert.That(perception.Target, Is.Null);
            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(agent.PerceivedTarget, Is.Null);
        }

        [Test]
        public void AnEnemyBehindAWallHearsEachCallOnce()
        {
            var agent = CreateEnemy(Side.Bravo);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 3f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.3f);
            _created.Add(wall);
            var decoy = Place();
            var ground = decoy.Damageable.GroundPoint;

            decoy.Tick(0f);
            LogAssert.Expect(LogType.Log, DecoyLog.Heard(EnemyId, OwnerId, Kind, 1, ground.ToNumerics()));
            var heard = agent.Perceive();

            Assert.That(heard.Target, Is.Null, "the decoy is out of sight");
            Assert.That(heard.HeardNoise, Is.True);
            Assert.That(heard.NoisePosition, Is.EqualTo(ground.ToNumerics()));
            Assert.That(agent.PerceivedTarget, Is.Null);

            Assert.That(agent.Perceive().HeardNoise, Is.False, "the same call is heard only once");

            decoy.Tick(_definition.CallIntervalSeconds);
            Assert.That(agent.Perceive().HeardNoise, Is.True);
        }

        [Test]
        public void AnEnemyBeyondTheCallRadiusDoesNotHearIt()
        {
            _definition.CallRadius = 3f;
            var agent = CreateEnemy(Side.Bravo);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 3f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.3f);
            _created.Add(wall);
            var decoy = Place();

            decoy.Tick(0f);

            Assert.That(agent.Perceive().HeardNoise, Is.False);
        }

        [Test]
        public void InvalidValuesAreRejectedWhenEquipping()
        {
            _definition.CallVolume = 2f;
            var host = _owner.AddComponent<AbilityHost>();

            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));

            _definition.CallVolume = 1f;
            _definition.LifetimeSeconds = 0f;
            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));
            Assert.That(host.Definition(0), Is.Null);
        }

        [Test]
        public void PlacingWithTheAbilityArmsTheDecoy()
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
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(DecoyLog.Prefix(OwnerId, Kind, 1) + " armed ") + ".* stealth=On$"));
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active.Count, Is.EqualTo(1));
            var decoy = active[0].GetComponent<Decoy>();
            Assert.That(decoy.IsArmed, Is.True);
            Assert.That(decoy.Side, Is.EqualTo(Side.Alpha));
        }

        private Decoy Place(int serial = 1)
        {
            var spec = _definition.ToDeploySpec();
            var deployable = _definition.CreateDeployed(new Pose(Vector3.zero, Quaternion.identity), spec);
            deployable.Initialize(_owner, OwnerId, Kind, serial, Side.Alpha, spec.MaxHealth);
            var decoy = deployable.GetComponent<Decoy>();
            decoy.Arm(_definition.ToDecoySpec(), _definition.CallSound, _definition.CallVolume);
            Physics.SyncTransforms();
            return decoy;
        }

        private EnemyAgent CreateEnemy(Side side)
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            _created.Add(rifle);

            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = EnemyId;
            host.AddComponent<SideMember>().Side = side;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = rifle;
            agent.Id = EnemyId;
            _created.Add(host);

            // 足元を EnemyGround に揃える。NavMeshAgent の baseOffset の分だけ持ち上げる。
            host.transform.SetPositionAndRotation(
                EnemyGround + (Vector3.up * host.GetComponent<NavMeshAgent>().baseOffset), Quaternion.LookRotation(Vector3.back));
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return agent;
        }

        private Damageable CreateBody(string name, Vector3 position, int health)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            body.transform.position = position;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, health);
            _created.Add(body);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
