using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class TutorialPresenterTests
    {
        private const float _frame = 1f / 60f;

        private GameObject _host;
        private BootstrapWorld _world;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;
        private TutorialPresenter _presenter;
        private TutorialProgressStore _store;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponAssets.Configure(_handgun, TestWeapons.HandgunSpec());

            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
            _world = bootstrap.Build();
            _store = new TutorialProgressStore("Fps.Tests.TutorialCompleted." + Guid.NewGuid().ToString("N"));
            _presenter = _host.AddComponent<TutorialPresenter>();
            _presenter.Store = _store;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            _store.Delete();
            if (_world != null)
            {
                Object.DestroyImmediate(_world.TargetRoot);
                Object.DestroyImmediate(_world.Light != null ? _world.Light.gameObject : null);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Floor);
            }

            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            GameSignals.ResetAll();
        }

        private static TutorialStatus Published
        {
            get
            {
                Assert.That(GameSignals.Tutorial.Value.HasValue, Is.True, "the tutorial publishes its status");
                return GameSignals.Tutorial.Value.Value;
            }
        }

        private static PlayerActionState State(PlayerActionConditions flags)
        {
            var reloading = (flags & PlayerActionConditions.Reloading) != 0;
            return new PlayerActionState(flags | PlayerActionConditions.Grounded, reloading ? 0.5f : 0f, null, ShoulderSide.Right, ViewMode.FirstPerson, "Rifle", 0f);
        }

        private void Tick(Vector3 position, PlayerActionConditions flags = PlayerActionConditions.None, InputDeviceKind device = InputDeviceKind.KeyboardMouse)
        {
            _presenter.Tick(_frame, State(flags), position, device);
        }

        private void Begin()
        {
            _presenter.Begin(_world.ActionReader);
        }

        private void WalkOff()
        {
            var x = 0f;
            Tick(Vector3.zero);
            while (_presenter.Steps.Current == TutorialStep.Move)
            {
                x += 0.5f;
                Tick(new Vector3(x, 0f, 0f));
                Assert.That(x, Is.LessThan(100f), "walking never finished the move step");
            }
        }

        [Test]
        public void BeginPublishesTheFirstStepAndItsAction()
        {
            Begin();

            Assert.That(_presenter.IsRunning, Is.True);
            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Move));
            var status = Published;
            Assert.That(status.Step, Is.EqualTo(TutorialStep.Move));
            Assert.That(status.StepNumber, Is.EqualTo(1));
            Assert.That(status.StepCount, Is.EqualTo(6));
            Assert.That(status.ActionPath, Is.EqualTo(InputActionPaths.Move));
            Assert.That(status.Device, Is.EqualTo(_presenter.Device));
            Assert.That(status.IsComplete, Is.False);
            Assert.That(_presenter.Status, Is.EqualTo(status));
        }

        [Test]
        public void TheSameStatusIsNotPublishedAgain()
        {
            Begin();
            var heard = 0;
            GameSignals.Tutorial.Changed += _ => heard++;

            Tick(Vector3.zero);
            Tick(Vector3.zero);

            Assert.That(heard, Is.Zero, "standing still changes nothing");
        }

        [Test]
        public void EachHitIsPublishedDuringTheShootStep()
        {
            Begin();
            WalkOff();
            for (var i = 0; i < 60 && _presenter.Steps.Current == TutorialStep.Aim; i++)
            {
                Tick(Vector3.zero, PlayerActionConditions.Aiming);
            }

            Assert.That(Published.Step, Is.EqualTo(TutorialStep.Shoot));
            Assert.That(Published.ActionPath, Is.EqualTo(InputActionPaths.Attack));
            CombatEvents.RaiseHitConfirmed(new HitConfirm(10, HitZone.Body, false, false));
            Tick(Vector3.zero);

            Assert.That(Published.Hits, Is.EqualTo(1));
            Assert.That(Published.HitsRequired, Is.EqualTo(TutorialSteps.DefaultHitsRequired));
        }

        [Test]
        public void WalkingMovesOnToAiming()
        {
            Begin();

            WalkOff();

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Aim));
            Assert.That(Published.Step, Is.EqualTo(TutorialStep.Aim));
            Assert.That(Published.StepNumber, Is.EqualTo(2));
            Assert.That(Published.ActionPath, Is.EqualTo(InputActionPaths.Aim));
        }

        [Test]
        public void JumpingUpAndDownIsNotWalking()
        {
            Begin();

            for (var i = 0; i < 100; i++)
            {
                Tick(new Vector3(0f, i % 2, 0f));
            }

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Move));
        }

        [Test]
        public void TheWholeTutorialCompletesAndIsSaved()
        {
            Begin();
            WalkOff();

            for (var i = 0; i < 60 && _presenter.Steps.Current == TutorialStep.Aim; i++)
            {
                Tick(Vector3.zero, PlayerActionConditions.Aiming);
            }

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Shoot));
            for (var i = 0; i < TutorialSteps.DefaultHitsRequired; i++)
            {
                CombatEvents.RaiseHitConfirmed(new HitConfirm(10, HitZone.Body, false, false));
                Tick(Vector3.zero);
            }

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Reload));
            Tick(Vector3.zero, PlayerActionConditions.Reloading);
            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Crouch));
            Tick(Vector3.zero, PlayerActionConditions.Crouching);
            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Cover));
            Assert.That(_store.IsCompleted, Is.False, "nothing is saved until the last step");

            Tick(Vector3.zero, PlayerActionConditions.InCover);

            Assert.That(_presenter.Steps.IsComplete, Is.True);
            Assert.That(_store.IsCompleted, Is.True);
            Assert.That(Published.IsComplete, Is.True);
            Assert.That(Published.ActionPath, Is.Null);

            _presenter.Tick(TutorialPresenter.CompletedDisplaySeconds, State(PlayerActionConditions.None), Vector3.zero, InputDeviceKind.KeyboardMouse);

            Assert.That(_presenter.IsRunning, Is.False);
            Assert.That(GameSignals.Tutorial.HasValue, Is.False, "the completion is cleared after a while");
            Assert.That(_presenter.Status, Is.Null);
        }

        [Test]
        public void HitsBeforeTheShootStepDoNotCount()
        {
            Begin();
            CombatEvents.RaiseHitConfirmed(new HitConfirm(10, HitZone.Body, false, false));
            WalkOff();

            for (var i = 0; i < 60 && _presenter.Steps.Current == TutorialStep.Aim; i++)
            {
                Tick(Vector3.zero, PlayerActionConditions.Aiming);
            }

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Shoot));
            Assert.That(_presenter.Steps.Hits, Is.Zero);
        }

        [Test]
        public void TheDeviceIsPublishedWithTheStep()
        {
            Begin();

            Tick(Vector3.zero, device: InputDeviceKind.KeyboardMouse);
            Assert.That(Published.Device, Is.EqualTo(InputDeviceKind.KeyboardMouse));

            Tick(Vector3.zero, device: InputDeviceKind.Gamepad);
            Assert.That(Published.Device, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(Published.Step, Is.EqualTo(TutorialStep.Move));
        }

        [Test]
        public void StopHidesWithoutSaving()
        {
            Begin();

            _presenter.Stop();

            Assert.That(_presenter.IsRunning, Is.False);
            Assert.That(GameSignals.Tutorial.HasValue, Is.False);
            Assert.That(_store.IsCompleted, Is.False);
            Assert.That(_presenter.Status, Is.Null);
        }

        [Test]
        public void StopLeavesAStatusPublishedByAnotherSourceAlone()
        {
            Begin();
            var other = new TutorialStatus(new TutorialSteps(), InputActionPaths.Move, InputDeviceKind.Gamepad);
            GameSignals.Tutorial.Publish(other);

            _presenter.Stop();

            Assert.That(GameSignals.Tutorial.Value, Is.EqualTo(other));
        }


        [Test]
        public void BeginAgainRestartsFromTheFirstStep()
        {
            Begin();
            WalkOff();

            Begin();

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Move));
            Assert.That(Published.StepNumber, Is.EqualTo(1));
        }

        [Test]
        public void EveryStepHasAnAction()
        {
            foreach (var step in TutorialSteps.Order)
            {
                Assert.That(TutorialPresenter.ActionPathFor(step), Does.StartWith("Player/"), step.ToString());
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialPresenter.ActionPathFor((TutorialStep)99));
        }

        [Test]
        public void RejectsMissingPartsAndBadTime()
        {
            Assert.Throws<ArgumentNullException>(() => _presenter.Begin(null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _presenter.Tick(-1f, State(PlayerActionConditions.None), Vector3.zero, InputDeviceKind.KeyboardMouse));
            Assert.Throws<ArgumentNullException>(() => _presenter.Store = null);
        }

        [Test]
        public void DetectDeviceKeepsTheLastDeviceWhenNothingIsPressed()
        {
            Assert.That(TutorialPresenter.DetectDevice(null, InputDeviceKind.Gamepad), Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(
                TutorialPresenter.DetectDevice(new UnityEngine.InputSystem.InputAction[] { null }, InputDeviceKind.KeyboardMouse),
                Is.EqualTo(InputDeviceKind.KeyboardMouse));
        }
    }
}
