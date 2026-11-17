using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class FlashTests
    {
        private const string _weaponId = GrenadeThrower.WeaponId;
        private const string _enemyId = "Guard";

        private static readonly BlindSpec _blind = new BlindSpec(15f, 4f, 0.25f);
        private static readonly Vector3 _center = new Vector3(0f, 1.5f, 0f);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
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
        public void LookingAtTheFlashBlindsLongerThanLookingAway()
        {
            var toward = CreateTarget("Toward", new Vector3(0f, 1f, 3f), Vector3.back);
            var away = CreateTarget("Away", new Vector3(0f, 1f, -3f), Vector3.back);

            var blinded = Flash.Detonate(_center, _blind, null, _weaponId);

            Assert.That(blinded, Is.EqualTo(2));
            var towardSeconds = toward.StatusEffects.RemainingSeconds(StatusEffectKinds.Blind);
            var awaySeconds = away.StatusEffects.RemainingSeconds(StatusEffectKinds.Blind);
            Assert.That(awaySeconds, Is.GreaterThan(0f));
            Assert.That(towardSeconds, Is.GreaterThan(awaySeconds * 3f), "turning away makes it much lighter");
            Assert.That(toward.Health.Current, Is.EqualTo(toward.Health.Max), "the flash does not hurt");
        }

        [Test]
        public void AFartherTargetIsBlindedForLessAndOneOutsideTheRadiusNotAtAll()
        {
            var near = CreateTarget("Near", new Vector3(0f, 1f, 3f), Vector3.back);
            var far = CreateTarget("Far", new Vector3(3f, 1f, 9f), Vector3.back);
            var outside = CreateTarget("Outside", new Vector3(-3f, 1f, _blind.Radius + 2f), Vector3.back);

            Flash.Detonate(_center, _blind, null, _weaponId);

            Assert.That(
                far.StatusEffects.RemainingSeconds(StatusEffectKinds.Blind),
                Is.GreaterThan(0f).And.LessThan(near.StatusEffects.RemainingSeconds(StatusEffectKinds.Blind)));
            Assert.That(outside.StatusEffects, Is.Null);
        }

        [Test]
        public void AWallBetweenTheFlashAndTheEyesKeepsThemSeeing()
        {
            var hidden = CreateTarget("Hidden", new Vector3(0f, 1f, 3f), Vector3.back);
            CreateBox("Wall", new Vector3(0f, 1.5f, 1.2f), new Vector3(4f, 4f, 0.3f));

            Assert.That(Flash.Detonate(_center, _blind, null, _weaponId), Is.Zero);
            Assert.That(hidden.StatusEffects, Is.Null);
        }

        [Test]
        public void AThrownFlashGrenadeBlindsWithoutExploding()
        {
            var target = CreateTarget("Target", new Vector3(0f, 1f, 3f), Vector3.back);
            var spec = new GrenadeSpec(
                "bcc273d5-24a8-42f4-bb3c-b054d001434d", "grenade.flash", new ImpactSpec(damage: 0, blind: _blind), fuseSeconds: 1f);
            var grenade = ThrownGrenade.Spawn(null, _center, Vector3.zero, spec, null);
            _created.Add(grenade.gameObject);

            var result = grenade.Explode();

            Assert.That(result.Damaged, Is.Zero);
            Assert.That(target.StatusEffects.Has(StatusEffectKinds.Blind), Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
        }

        [Test]
        public void TheBlindnessWearsOff()
        {
            var target = CreateTarget("Target", new Vector3(0f, 1f, 3f), Vector3.back);
            Flash.Detonate(_center, _blind, null, _weaponId);
            var receiver = target.StatusEffects;

            receiver.Advance(receiver.RemainingSeconds(StatusEffectKinds.Blind) + 0.1f);

            Assert.That(receiver.Has(StatusEffectKinds.Blind), Is.False);
        }

        [Test]
        public void BlindingTheLocalPlayerTellsTheHud()
        {
            var player = CreateTarget("Player", new Vector3(0f, 1f, 3f), Vector3.back);
            player.IsLocalPlayer = true;
            var told = new List<StatusEffectReceiver>();
            System.Action<StatusEffectReceiver> listener = told.Add;
            StatusEffectReceiver.LocalPlayerBlinded += listener;
            try
            {
                var dummy = CreateTarget("Dummy", new Vector3(2f, 1f, 3f), Vector3.back);
                Flash.Detonate(_center, _blind, null, _weaponId);

                Assert.That(dummy.StatusEffects.Has(StatusEffectKinds.Blind), Is.True);
                Assert.That(told, Is.EqualTo(new[] { player.StatusEffects }), "only the local player's blindness reaches the HUD");
            }
            finally
            {
                StatusEffectReceiver.LocalPlayerBlinded -= listener;
            }
        }

        [Test]
        public void ABlindedEnemyStopsSeeingItsTargetUntilItWearsOff()
        {
            var agent = CreateEnemy();
            var player = CreateTarget("Player", new Vector3(0f, 1f, 3f), Vector3.back);
            agent.Target = player;
            Assert.That(agent.Perceive().HasLineOfSight, Is.True);

            var receiver = agent.GetComponent<Damageable>().EnsureStatusEffects();
            receiver.Afflict(BlindEffect.StatusEffect(2f), player.gameObject, _weaponId);

            var blinded = agent.Perceive();
            Assert.That(blinded.HasLineOfSight, Is.False);
            Assert.That(blinded.Target, Is.Null);
            Assert.That(blinded.HeardNoise, Is.False);

            receiver.Advance(2.1f);

            Assert.That(agent.Perceive().HasLineOfSight, Is.True, "it sees again once the blindness wears off");
        }

        private EnemyAgent CreateEnemy()
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            _created.Add(rifle);

            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = _enemyId;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = rifle;
            agent.Id = _enemyId;
            _created.Add(host);
            host.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, 8f) + (Vector3.up * host.GetComponent<UnityEngine.AI.NavMeshAgent>().baseOffset), Quaternion.LookRotation(Vector3.back));
            Assert.That(agent.TryInitialize(), Is.True);
            Physics.SyncTransforms();
            return agent;
        }

        private Damageable CreateTarget(string name, Vector3 position, Vector3 facing)
        {
            var body = CreateBox(name, position, new Vector3(0.6f, 2f, 0.6f));
            body.transform.rotation = Quaternion.LookRotation(facing);
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(box);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }
    }
}
