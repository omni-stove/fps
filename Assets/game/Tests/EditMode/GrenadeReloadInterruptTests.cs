using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GrenadeReloadInterruptTests
    {
        private readonly List<Object> _created = new List<Object>();
        private InputAction _attack;
        private InputAction _reload;
        private WeaponController _weapon;

        [SetUp]
        public void SetUp()
        {
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
        }

        [TearDown]
        public void TearDown()
        {
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
        public void ThrowingAGrenadeInterruptsTheReloadAndBlocksANewOneUntilTheThrowEnds()
        {
            var thrower = CreatePlayer(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));
            Assert.That(thrower.InterruptsReload, Is.True);
            Assert.That(_weapon.Fire(), Is.True);
            Assert.That(_weapon.StartReload(), Is.True);

            Assert.That(thrower.TryThrow(), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] reload-cancelled by=GrenadeThrower");
            _weapon.Tick(0f);

            Assert.That(_weapon.Weapon.IsReloading, Is.False);
            Assert.That(_weapon.StartReload(), Is.False, "no reload while throwing");

            thrower.Tick(GrenadeSpec.Default.ThrowSeconds);
            Assert.That(_weapon.StartReload(), Is.True);
        }

        [Test]
        public void WithoutGrenadeInReloadInterruptedByThrowingLetsTheReloadGoOn()
        {
            var thrower = CreatePlayer(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2)
                .WithReloadInterruptedBy(new[] { ReloadInterrupt.Melee }));
            Assert.That(thrower.InterruptsReload, Is.False);
            Assert.That(_weapon.Fire(), Is.True);
            Assert.That(_weapon.StartReload(), Is.True);

            Assert.That(thrower.TryThrow(), Is.True);
            _weapon.Tick(0f);

            Assert.That(_weapon.Weapon.IsReloading, Is.True);
            Assert.That(_weapon.IsReloadBlockedByModifier, Is.False);
        }

        [Test]
        public void AThrowerThatIsNotThrowingDoesNotBlockTheReload()
        {
            var thrower = CreatePlayer(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));
            Assert.That(thrower.IsThrowing, Is.False);
            Assert.That(_weapon.Fire(), Is.True);

            Assert.That(_weapon.StartReload(), Is.True);
            Assert.That(_weapon.IsReloadBlockedByModifier, Is.False);
        }

        private GrenadeThrower CreatePlayer(ActionRules actions)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(body);
            body.name = "Thrower";
            body.transform.position = new Vector3(0f, 1f, 0f);
            body.AddComponent<Damageable>().Initialize("Thrower", 100);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(body.transform, false);
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(rifle);
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, rifle);

            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.Configure(GameRules.Default.WithActions(actions));
            thrower.Thrown += grenade => _created.Add(grenade.gameObject);
            _weapon.AddModifier(thrower);
            Physics.SyncTransforms();
            return thrower;
        }
    }
}
