using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 撃つ・当てる・倒すで <see cref="CombatEvents"/> の各イベントが 1 回ずつ流れることを確かめる。
    /// Edit Mode でも Physics.Raycast はコライダーに当たるので、生成後に SyncTransforms してから撃つ。
    /// </summary>
    public sealed class CombatEventsTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<Object> _created = new List<Object>();
        private InputAction _attack;
        private InputAction _reload;
        private Recorder _recorder;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _recorder = new Recorder();
        }

        [TearDown]
        public void TearDown()
        {
            _recorder.Dispose();
            CombatEvents.ClearSubscribers();
            _attack.Dispose();
            _reload.Dispose();
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
        public void PlayerShotThatKillsRaisesEachEventOnce()
        {
            var weapon = CreatePlayerWeapon(out var rifle);
            var target = CreateTarget("target", new Vector3(0f, 1f, 10f), maxHealth: 1);

            Assert.That(weapon.Fire(), Is.True);

            Assert.That(_recorder.Shots, Has.Count.EqualTo(1));
            var shot = _recorder.Shots[0];
            Assert.That(shot.Shooter, Is.SameAs(weapon.gameObject));
            Assert.That(shot.Origin, Is.EqualTo(weapon.transform.position));
            Assert.That(shot.Direction, Is.EqualTo(Vector3.forward));
            Assert.That(shot.WeaponId, Is.EqualTo(rifle.Id));

            Assert.That(_recorder.Impacts, Has.Count.EqualTo(1));
            Assert.That(_recorder.Impacts[0].Collider.gameObject, Is.SameAs(target.gameObject));
            Assert.That(_recorder.Impacts[0].Point.z, Is.EqualTo(9.5f).Within(1e-3f), "the near face of the capsule");
            Assert.That(_recorder.Impacts[0].Normal.z, Is.LessThan(0f), "the face looks back at the shooter");

            Assert.That(_recorder.Confirms, Has.Count.EqualTo(1));
            Assert.That(_recorder.Confirms[0].Killed, Is.True);
            Assert.That(_recorder.Confirms[0].Zone, Is.EqualTo(HitZone.Body));
            Assert.That(_recorder.Confirms[0].Damage, Is.GreaterThan(0));

            Assert.That(_recorder.Kills, Has.Count.EqualTo(1));
            var kill = _recorder.Kills[0];
            Assert.That(kill.KillerId, Is.EqualTo(CombatEvents.OfflineId(weapon.gameObject)));
            Assert.That(kill.VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(kill.WeaponId, Is.EqualTo(rifle.Id));
            Assert.That(kill.IsSuicide, Is.False);
            Assert.That(kill.Headshot, Is.False);

            Assert.That(_recorder.DamageTaken, Is.Empty, "the target is not the local player");
            Assert.That(CombatEvents.CurrentAttribution, Is.Null, "the attribution ends with the shot");
        }

        [Test]
        public void PlayerShotThatWoundsConfirmsTheHitWithoutAKill()
        {
            var weapon = CreatePlayerWeapon(out _);
            CreateTarget("target", new Vector3(0f, 1f, 10f), maxHealth: 1000);

            Assert.That(weapon.Fire(), Is.True);

            Assert.That(_recorder.Shots, Has.Count.EqualTo(1));
            Assert.That(_recorder.Impacts, Has.Count.EqualTo(1));
            Assert.That(_recorder.Confirms, Has.Count.EqualTo(1));
            Assert.That(_recorder.Confirms[0].Killed, Is.False);
            Assert.That(_recorder.Kills, Is.Empty);
        }

        [Test]
        public void ShootingACorpseConfirmsNothingAndDoesNotKillAgain()
        {
            var weapon = CreatePlayerWeapon(out var rifle);
            CreateTarget("target", new Vector3(0f, 1f, 10f), maxHealth: 1);
            Assert.That(weapon.Fire(), Is.True);

            weapon.Tick(rifle.FireInterval);
            Assert.That(weapon.Fire(), Is.True);

            Assert.That(_recorder.Shots, Has.Count.EqualTo(2));
            Assert.That(_recorder.Impacts, Has.Count.EqualTo(2));
            Assert.That(_recorder.Confirms, Has.Count.EqualTo(1));
            Assert.That(_recorder.Kills, Has.Count.EqualTo(1));
        }

        [Test]
        public void MissRaisesOnlyTheShot()
        {
            var weapon = CreatePlayerWeapon(out _);

            Assert.That(weapon.Fire(), Is.True);

            Assert.That(_recorder.Shots, Has.Count.EqualTo(1));
            Assert.That(_recorder.Impacts, Is.Empty);
            Assert.That(_recorder.Confirms, Is.Empty);
            Assert.That(_recorder.Kills, Is.Empty);
        }

        [Test]
        public void WallImpactRaisesNoConfirm()
        {
            var weapon = CreatePlayerWeapon(out _);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = new Vector3(0f, 1f, 5f);
            Physics.SyncTransforms();

            Assert.That(weapon.Fire(), Is.True);

            Assert.That(_recorder.Impacts, Has.Count.EqualTo(1));
            Assert.That(_recorder.Impacts[0].Collider.gameObject, Is.SameAs(wall));
            Assert.That(_recorder.Confirms, Is.Empty);
        }

        [Test]
        public void NoEventIsRaisedWhenTheWeaponCannotFire()
        {
            var weapon = CreatePlayerWeapon(out _);
            Assert.That(weapon.Fire(), Is.True);

            Assert.That(weapon.Fire(), Is.False, "within the fire interval");

            Assert.That(_recorder.Shots, Has.Count.EqualTo(1));
        }

        [Test]
        public void EnemyShotOnTheLocalPlayerRaisesDamageTakenButNoConfirm()
        {
            var shooter = new GameObject("enemy");
            _created.Add(shooter);
            shooter.transform.position = new Vector3(0f, 0f, -1f);
            var player = CreateTarget("player", new Vector3(0f, 1f, 10f), maxHealth: 100);
            player.IsLocalPlayer = true;

            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, shooter.transform, weaponId: "enemy-rifle");

            Assert.That(result.Damage, Is.EqualTo(10));
            Assert.That(_recorder.Shots, Has.Count.EqualTo(1));
            Assert.That(_recorder.Shots[0].Shooter, Is.SameAs(shooter));
            Assert.That(_recorder.Shots[0].WeaponId, Is.EqualTo("enemy-rifle"));
            Assert.That(_recorder.Impacts, Has.Count.EqualTo(1));
            Assert.That(_recorder.Confirms, Is.Empty, "only the shooter's own client confirms hits");

            Assert.That(_recorder.DamageTaken, Has.Count.EqualTo(1));
            Assert.That(_recorder.DamageTaken[0].Amount, Is.EqualTo(10));
            Assert.That(_recorder.DamageTaken[0].SourcePosition, Is.EqualTo(shooter.transform.position));
            Assert.That(_recorder.Kills, Is.Empty);
        }

        [Test]
        public void EnemyHeadshotThatKillsIsAttributedToTheEnemy()
        {
            var shooter = new GameObject("enemy");
            _created.Add(shooter);
            var target = CreateTarget("player", new Vector3(0f, 1f, 10f), maxHealth: 5, HitZone.Head);

            Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, shooter.transform, weaponId: "enemy-rifle");

            Assert.That(_recorder.Kills, Has.Count.EqualTo(1));
            Assert.That(_recorder.Kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(shooter)));
            Assert.That(_recorder.Kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(_recorder.Kills[0].WeaponId, Is.EqualTo("enemy-rifle"));
            Assert.That(_recorder.Kills[0].Headshot, Is.True);
        }

        [Test]
        public void DeathWithoutAShooterIsReportedAsASuicide()
        {
            var target = CreateTarget("target", new Vector3(0f, 1f, 10f), maxHealth: 10);

            target.Kill();
            target.TakeDamage(5);

            Assert.That(_recorder.Kills, Has.Count.EqualTo(1));
            Assert.That(_recorder.Kills[0].IsSuicide, Is.True);
            Assert.That(_recorder.Kills[0].WeaponId, Is.Empty);
        }

        [Test]
        public void DamageTakenIsOnlyForTheLocalPlayer()
        {
            var other = CreateTarget("other", new Vector3(0f, 1f, 10f), maxHealth: 100);
            var player = CreateTarget("player", new Vector3(5f, 1f, 10f), maxHealth: 100);
            player.IsLocalPlayer = true;

            other.TakeDamage(10);
            player.TakeDamage(15, new Vector3(1f, 2f, 3f));

            Assert.That(_recorder.DamageTaken, Has.Count.EqualTo(1));
            Assert.That(_recorder.DamageTaken[0].Amount, Is.EqualTo(15));
            Assert.That(_recorder.DamageTaken[0].SourcePosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void AttributionScopesNestAndRestore()
        {
            var outer = new GameObject("outer");
            var inner = new GameObject("inner");
            _created.Add(outer);
            _created.Add(inner);

            using (CombatEvents.Attribute(outer, "a"))
            {
                using (CombatEvents.Attribute(inner, null))
                {
                    Assert.That(CombatEvents.CurrentAttribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(inner)));
                    Assert.That(CombatEvents.CurrentAttribution.Value.WeaponId, Is.Empty);
                }

                Assert.That(CombatEvents.CurrentAttribution.Value.KillerId, Is.EqualTo(CombatEvents.OfflineId(outer)));
                Assert.That(CombatEvents.CurrentAttribution.Value.WeaponId, Is.EqualTo("a"));
            }

            Assert.That(CombatEvents.CurrentAttribution, Is.Null);
        }

        [Test]
        public void AThrowingSubscriberDoesNotStopTheShotOrOtherSubscribers()
        {
            var weapon = CreatePlayerWeapon(out _);
            var target = CreateTarget("target", new Vector3(0f, 1f, 10f), maxHealth: 1000);
            Action<ShotFiredInfo> thrower = _ => throw new InvalidOperationException("boom");
            CombatEvents.ShotFired += thrower;
            var after = 0;
            CombatEvents.ShotFired += _ => after++;

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Assert.That(weapon.Fire(), Is.True);

            Assert.That(after, Is.EqualTo(1));
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Max), "the hit still lands");
        }

        [Test]
        public void ClearSubscribersDropsEveryHandler()
        {
            _recorder.Dispose();
            var count = 0;
            CombatEvents.Killed += _ => count++;
            CombatEvents.ClearSubscribers();

            CombatEvents.RaiseKilled(new KillFeedEntry(1, 2, "w", false, false));

            Assert.That(count, Is.Zero);
        }

        private WeaponController CreatePlayerWeapon(out WeaponDefinition rifle)
        {
            var camera = new GameObject("PlayerCamera", typeof(Camera));
            _created.Add(camera);
            camera.transform.position = _eye;
            rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            _created.Add(rifle);
            WeaponAssetBuilder.Configure(rifle, WeaponAssetBuilder.RifleSpec());

            var weapon = camera.AddComponent<WeaponController>();
            weapon.Initialize(camera.transform, _attack, _reload, rifle);
            return weapon;
        }

        private Damageable CreateTarget(string id, Vector3 position, int maxHealth, HitZone zone = HitZone.Body)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, maxHealth);
            target.AddComponent<Hitbox>().Initialize(zone, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        // 5 つのイベントを購読して届いた値を並べる。Dispose で購読を外す。
        private sealed class Recorder : IDisposable
        {
            public readonly List<ShotFiredInfo> Shots = new List<ShotFiredInfo>();
            public readonly List<ImpactInfo> Impacts = new List<ImpactInfo>();
            public readonly List<HitConfirm> Confirms = new List<HitConfirm>();
            public readonly List<DamageTakenInfo> DamageTaken = new List<DamageTakenInfo>();
            public readonly List<KillFeedEntry> Kills = new List<KillFeedEntry>();

            public Recorder()
            {
                CombatEvents.ShotFired += Shots.Add;
                CombatEvents.Impact += Impacts.Add;
                CombatEvents.HitConfirmed += Confirms.Add;
                CombatEvents.DamageTaken += DamageTaken.Add;
                CombatEvents.Killed += Kills.Add;
            }

            public void Dispose()
            {
                CombatEvents.ShotFired -= Shots.Add;
                CombatEvents.Impact -= Impacts.Add;
                CombatEvents.HitConfirmed -= Confirms.Add;
                CombatEvents.DamageTaken -= DamageTaken.Add;
                CombatEvents.Killed -= Kills.Add;
            }
        }
    }
}
