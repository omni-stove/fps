using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では Update が走らないので、Turret.Tick を手で進める。Physics の問い合わせは効くので、動かした後は SyncTransforms する。
    // タレットは原点に +Z 向きで置き、持ち主は Alpha、敵は Bravo の陣営を持たせる。
    public sealed class TurretTests
    {
        private const string Kind = "turret";
        private const string OwnerId = "Owner";
        private const float Step = 0.05f;
        private const int MaxSteps = 400;

        private readonly List<Object> _created = new List<Object>();
        private TurretDeployDefinition _definition;
        private WeaponDefinition _rifle;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _created.Add(_rifle);

            _definition = ScriptableObject.CreateInstance<TurretDeployDefinition>();
            _definition.name = "DeployTurret";
            _definition.Id = Kind;
            _definition.Weapon = _rifle;
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
        public void ThePlaceholderHasAHeadAndAvoidsTheNavMeshButIsNotCover()
        {
            var turret = Place(FriendlyFire.Off);

            Assert.That(turret.Head, Is.Not.SameAs(turret.transform));
            Assert.That(turret.Head.name, Is.EqualTo(Turret.HeadName));
            Assert.That(turret.Head.position.y, Is.LessThan(TurretSpec.DefaultHeight));
            Assert.That(turret.GetComponent<NavMeshObstacle>(), Is.Not.Null);
            Assert.That(turret.GetComponentInChildren<CoverPoint>(), Is.Null);
            Assert.That(turret.Damageable.Health.Current, Is.EqualTo(TurretSpec.DefaultMaxHealth));
            Assert.That(turret.IsArmed, Is.True);
            Assert.That(Turret.Armed, Has.Member(turret));
            Assert.Throws<System.InvalidOperationException>(() => turret.Arm(TurretSpec.Default, _rifle.ToSpec(), FriendlyFire.Off));
        }

        [Test]
        public void ItShootsAndKillsAHostileInSight()
        {
            var turret = Place(FriendlyFire.Off);
            var foe = CreateBody("Foe", new Vector3(0f, 0.5f, 5f), Side.Bravo, 40);

            LogAssert.Expect(LogType.Log, TurretLog.Target(OwnerId, Kind, 1, "Foe"));
            LogAssert.Expect(LogType.Log, TurretLog.Killed(OwnerId, Kind, 1, "Foe"));
            Run(turret, () => foe.IsDead);

            Assert.That(foe.IsDead, Is.True);
            Assert.That(turret.State.RoundsLeft, Is.LessThan(TurretSpec.DefaultRounds));
        }

        [Test]
        public void ItLeavesAlliesAndSidelessDummiesAlone()
        {
            var turret = Place(FriendlyFire.On);
            var ally = CreateBody("Ally", new Vector3(-2f, 0.5f, 5f), Side.Alpha, 40);
            var dummy = CreateBody("Dummy", new Vector3(2f, 0.5f, 5f), null, 40);

            for (var i = 0; i < 40; i++)
            {
                turret.Tick(Step);
            }

            Assert.That(turret.Target, Is.Null);
            Assert.That(turret.State.RoundsLeft, Is.EqualTo(TurretSpec.DefaultRounds));
            Assert.That(ally.Health.Current, Is.EqualTo(40));
            Assert.That(dummy.Health.Current, Is.EqualTo(40));
        }

        [Test]
        public void ItShootsABotWithoutASide()
        {
            Object.DestroyImmediate(_owner.GetComponent<SideMember>());
            var turret = Place(FriendlyFire.Off, null);
            var bot = CreateBody("Bot", new Vector3(0f, 0.5f, 5f), null, 40);
            bot.gameObject.AddComponent<EnemyAgent>().enabled = false;

            Run(turret, () => bot.IsDead);

            Assert.That(bot.IsDead, Is.True);
        }

        [Test]
        public void AnAllyInTheLineOfFireHoldsTheTurretWhileFriendlyFireIsOff()
        {
            var turret = Place(FriendlyFire.Off);
            var foe = CreateBody("Foe", new Vector3(0f, 0.5f, 8f), Side.Bravo, 1000);
            var ally = CreateBody("Ally", new Vector3(3f, 0.5f, 4f), Side.Alpha, 40);
            turret.Tick(Step);
            Assert.That(turret.Target, Is.SameAs(foe));
            var rounds = turret.State.RoundsLeft;

            ally.transform.position = new Vector3(0f, 0.5f, 4f);
            Physics.SyncTransforms();
            LogAssert.Expect(LogType.Log, TurretLog.Held(OwnerId, Kind, 1, "Ally", FriendlyFire.Off));
            turret.Tick(_rifle.FireInterval + 0.01f);

            Assert.That(turret.State.RoundsLeft, Is.EqualTo(rounds), "no round is spent while an ally is in the way");
            Assert.That(ally.Health.Current, Is.EqualTo(40));
        }

        [Test]
        public void FriendlyFireOnShootsThroughTheAllyInTheWay()
        {
            var turret = Place(FriendlyFire.On);
            var foe = CreateBody("Foe", new Vector3(0f, 0.5f, 8f), Side.Bravo, 1000);
            var ally = CreateBody("Ally", new Vector3(3f, 0.5f, 4f), Side.Alpha, 1000);
            turret.Tick(Step);
            Assert.That(turret.Target, Is.SameAs(foe));

            ally.transform.position = new Vector3(0f, 0.5f, 4f);
            Physics.SyncTransforms();
            turret.Tick(_rifle.FireInterval + 0.01f);

            Assert.That(ally.Health.Current, Is.LessThan(1000), "the rule lets the bullet hit the ally");
        }

        [Test]
        public void ItRunsOutOfRoundsAndIsRemoved()
        {
            _definition.Rounds = 3;
            var turret = Place(FriendlyFire.Off);
            var deployable = turret.Deployable;
            var foe = CreateBody("Foe", new Vector3(0f, 0.5f, 5f), Side.Bravo, 1000);
            DeployRemoval? removedWith = null;
            deployable.Removed += (_, reason) => removedWith = reason;

            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(TurretLog.Prefix(OwnerId, Kind, 1) + " fire target=Foe ") + ".* rounds=0$"));
            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Expended));
            Run(turret, () => removedWith.HasValue);

            Assert.That(removedWith, Is.EqualTo(DeployRemoval.Expended));
            Assert.That(deployable == null, Is.True, "the empty turret is destroyed");
            Assert.That(foe.Health.Current, Is.LessThan(1000));
            Assert.That(Turret.Armed, Has.No.Member(turret));
        }

        [Test]
        public void ItExpiresAfterItsLifetime()
        {
            _definition.LifetimeSeconds = 1f;
            var turret = Place(FriendlyFire.Off);
            var deployable = turret.Deployable;

            turret.Tick(0.6f);
            Assert.That(deployable.IsRemoved, Is.False);

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Expired));
            turret.Tick(0.6f);

            Assert.That(deployable == null, Is.True);
        }

        [Test]
        public void EnemyBulletsBreakTheTurretWithoutAKill()
        {
            var turret = Place(FriendlyFire.Off);
            var deployable = turret.Deployable;
            var shooter = new GameObject("Shooter");
            shooter.AddComponent<SideMember>().Side = Side.Bravo;
            shooter.transform.position = new Vector3(0f, 0.6f, 5f);
            _created.Add(shooter);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, DeployLog.Removed(OwnerId, Kind, 1, DeployRemoval.Destroyed));
            for (var i = 0; i < 20 && deployable != null && !deployable.IsRemoved; i++)
            {
                Hitscan.Fire(shooter.transform.position, Vector3.back, 20f, 50, DamageFalloff.None, shooter.transform);
            }

            Assert.That(deployable == null, Is.True);
        }

        [Test]
        public void TheTurretsBulletsGoThroughItsOwnersShield()
        {
            var turret = Place(FriendlyFire.Off);
            var shieldDefinition = ScriptableObject.CreateInstance<ShieldDeployDefinition>();
            shieldDefinition.Id = "shield";
            _created.Add(shieldDefinition);
            var shieldSpec = shieldDefinition.ToDeploySpec();
            var shield = shieldDefinition.CreateDeployed(new Pose(new Vector3(0f, 0f, 2.5f), Quaternion.identity), shieldSpec);
            shield.Initialize(_owner, OwnerId, "shield", 2, Side.Alpha, shieldSpec.MaxHealth);
            var foe = CreateBody("Foe", new Vector3(0f, 0.5f, 5f), Side.Bravo, 1000);

            Assert.That(shield.LetsThrough(turret.gameObject), Is.True);
            Run(turret, () => foe.Health.Current < 1000);

            Assert.That(foe.Health.Current, Is.LessThan(1000));
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(shieldSpec.MaxHealth));
        }

        [Test]
        public void EnemiesPreferANearerHostileTurret()
        {
            var turret = Place(FriendlyFire.Off);
            var player = CreateBody("Player", new Vector3(0f, 0.5f, -6f), Side.Alpha, 100);
            var enemy = new GameObject("Enemy");
            enemy.transform.position = new Vector3(0f, 0f, 4f);
            _created.Add(enemy);

            var chosen = EnemySenses.PreferNearerTurret(enemy, Side.Bravo, enemy.transform.position, player, player.GroundPoint, Turret.Armed);
            Assert.That(chosen, Is.SameAs(turret.Damageable));

            var tagged = EnemySenses.PreferNearerTurret(enemy, null, enemy.transform.position, player, player.GroundPoint, Turret.Armed);
            Assert.That(tagged, Is.SameAs(turret.Damageable), "bots without sides also go for the turret");

            var sameSide = EnemySenses.PreferNearerTurret(enemy, Side.Alpha, enemy.transform.position, player, player.GroundPoint, Turret.Armed);
            Assert.That(sameSide, Is.SameAs(player), "a turret of the enemy's own side is not a target");

            enemy.transform.position = new Vector3(0f, 0f, -8f);
            var farther = EnemySenses.PreferNearerTurret(enemy, Side.Bravo, enemy.transform.position, player, player.GroundPoint, Turret.Armed);
            Assert.That(farther, Is.SameAs(player), "the nearer one wins");
        }

        [Test]
        public void PlacingWithTheAbilityArmsTheTurretWithTheRulesFriendlyFire()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            _created.Add(floor);
            _owner.transform.position = new Vector3(0f, 1f, 0f);
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            var host = _owner.AddComponent<AbilityHost>();
            host.Configure(GameRules.Default
                .WithActions(ActionRules.Default.WithAbilities(Abilities.On))
                .WithFriendlyFire(FriendlyFire.On));
            host.Equip(0, _definition);
            Physics.SyncTransforms();

            Assert.That(host.TryUse(0), Is.True);
            LogAssert.Expect(LogType.Log, new Regex("^" + Regex.Escape(TurretLog.Prefix(OwnerId, Kind, 1) + " armed weapon=") + ".* friendlyFire=On$"));
            host.Step(PlayerCommands.None, PlayerCommands.None, 0f);

            var active = DeployOwner.GetOrAdd(_owner).Active(Kind);
            Assert.That(active, Has.Count.EqualTo(1));
            var turret = active[0].GetComponent<Turret>();
            Assert.That(turret.IsArmed, Is.True);
            Assert.That(turret.FriendlyFire, Is.EqualTo(FriendlyFire.On));
            Assert.That(turret.Side, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void ATurretWithoutAWeaponIsRejectedWhenEquipping()
        {
            _definition.Weapon = null;
            var host = _owner.AddComponent<AbilityHost>();

            Assert.Throws<AbilityDefinitionException>(() => host.Equip(0, _definition));
            Assert.That(host.Definition(0), Is.Null);
        }

        private Turret Place(FriendlyFire friendlyFire)
        {
            return Place(friendlyFire, Side.Alpha);
        }

        private Turret Place(FriendlyFire friendlyFire, Side? side)
        {
            var spec = _definition.ToDeploySpec();
            var deployable = _definition.CreateDeployed(new Pose(Vector3.zero, Quaternion.identity), spec);
            deployable.Initialize(_owner, OwnerId, Kind, 1, side, spec.MaxHealth);
            var turret = deployable.GetComponent<Turret>();
            turret.Arm(_definition.ToTurretSpec(), _definition.ToWeaponSpec(), friendlyFire);
            Physics.SyncTransforms();
            return turret;
        }

        private Damageable CreateBody(string name, Vector3 position, Side? side, int health)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            body.transform.position = position;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, health);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            if (side.HasValue)
            {
                body.AddComponent<SideMember>().Side = side.Value;
            }

            _created.Add(body);
            Physics.SyncTransforms();
            return damageable;
        }

        private static void Run(Turret turret, System.Func<bool> done)
        {
            for (var i = 0; i < MaxSteps && !done(); i++)
            {
                turret.Tick(Step);
                Physics.SyncTransforms();
            }
        }
    }
}
