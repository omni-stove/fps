using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    // 押下の頃合いは機器の入力イベントを流さないと作れないので、押した操作 (control) を直に渡して譲るかどうかを試す。
    public sealed class KeySharingTests
    {
        private Keyboard _keyboard;
        private InputActionMap _map;
        private InputAction _melee;
        private InputAction _interact;
        private InputAction _reload;
        private object _source;

        [SetUp]
        public void SetUp()
        {
            KeySharing.Reset();
            KeySharing.Spec = new KeySharingSpec(new[]
            {
                new KeySharingRule(new[] { "Melee", "Interact" }, "Interact"),
                new KeySharingRule(new[] { "Reload", "Interact" }),
            });
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _map = new InputActionMap("Player");
            _melee = _map.AddAction("Melee", InputActionType.Button, "<Keyboard>/v");
            _interact = _map.AddAction("Interact", InputActionType.Button, "<Keyboard>/v");
            _interact.AddBinding("<Keyboard>/e");
            _reload = _map.AddAction("Reload", InputActionType.Button, "<Keyboard>/v");
            _source = new object();
        }

        [TearDown]
        public void TearDown()
        {
            _map.Dispose();
            InputSystem.RemoveDevice(_keyboard);
            KeySharing.Reset();
        }

        [Test]
        public void MeleeYieldsTheSharedKeyOnlyWhileInteractIsClaimed()
        {
            Assert.That(KeySharing.Yields(_melee, _keyboard.vKey), Is.False, "nothing to interact with, so the key swings");

            KeySharing.Claim(InputActionPaths.Interact, _source);
            Assert.That(KeySharing.Yields(_melee, _keyboard.vKey), Is.True);

            KeySharing.Release("Interact", _source);
            Assert.That(KeySharing.Yields(_melee, _keyboard.vKey), Is.False);
        }

        [Test]
        public void AKeyInteractDoesNotUseIsNotYielded()
        {
            _melee.AddBinding("<Keyboard>/b");
            KeySharing.Claim("Interact", _source);

            Assert.That(KeySharing.Yields(_melee, _keyboard.bKey), Is.False);
            Assert.That(KeySharing.Yields(_melee, _keyboard.vKey), Is.True);
        }

        [Test]
        public void ThePreferredActionAndARuleWithoutPreferNeverYield()
        {
            KeySharing.Claim("Interact", _source);

            Assert.That(KeySharing.Yields(_interact, _keyboard.vKey), Is.False);
            Assert.That(KeySharing.Yields(_reload, _keyboard.vKey), Is.False, "Reload and Interact share without prefer, so the game splits them");
        }

        [Test]
        public void ClaimsAreCountedPerSource()
        {
            var other = new object();
            KeySharing.Claim("Player/Interact", _source);
            KeySharing.Claim("interact", other);
            KeySharing.Claim("Interact", _source);

            KeySharing.Release("Interact", _source);
            Assert.That(KeySharing.IsClaimed("Interact"), Is.True, "the other source still claims it");

            KeySharing.Release("Interact", other);
            Assert.That(KeySharing.IsClaimed("Interact"), Is.False);
            Assert.DoesNotThrow(() => KeySharing.Release("Interact", other));
        }

        [Test]
        public void AnIdleOrMissingActionIsNeitherPressedNorYielded()
        {
            KeySharing.Claim("Interact", _source);

            Assert.That(KeySharing.Yields(null), Is.False);
            Assert.That(KeySharing.Yields(_melee), Is.False, "no control is active while nothing is pressed");
            Assert.That(KeySharing.Yields(_melee, null), Is.False);
            Assert.That(KeySharing.WasPressedThisFrame(null), Is.False);
            Assert.That(KeySharing.IsPressed(null), Is.False);
            Assert.That(KeySharing.WasPressedThisFrame(_melee), Is.False);
        }

        [Test]
        public void ActionNameOfDropsTheMapAndRefusesBlanks()
        {
            Assert.That(KeySharing.ActionNameOf("Player/Interact"), Is.EqualTo("Interact"));
            Assert.That(KeySharing.ActionNameOf(" Melee "), Is.EqualTo("Melee"));
            Assert.Throws<ArgumentException>(() => KeySharing.ActionNameOf(" "));
            Assert.Throws<ArgumentNullException>(() => KeySharing.Claim("Interact", null));
            Assert.Throws<ArgumentNullException>(() => KeySharing.Release("Interact", null));
        }

        [Test]
        public void ResetDropsClaimsAndTheSpecSetByCode()
        {
            KeySharing.Claim("Interact", _source);

            KeySharing.Reset();

            Assert.That(KeySharing.IsClaimed("Interact"), Is.False);
            Assert.That(KeySharing.Spec, Is.SameAs(KitTuning.KeySharing));
        }
    }
}
