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
        private TutorialPromptView _view;
        private TutorialProgressStore _store;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponAssetBuilder.Configure(_handgun, WeaponAssetBuilder.HandgunSpec());

            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
            _world = bootstrap.Build();
            Assume.That(_world.Hud, Is.Not.Null, "the HUD is not built on a dedicated server");

            _view = _world.Hud.TutorialPrompt;
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
                Object.DestroyImmediate(_world.Hud != null ? _world.Hud.gameObject : null);
                Object.DestroyImmediate(_world.HudPanelSettings);
                Object.DestroyImmediate(_world.TargetRoot);
                Object.DestroyImmediate(_world.Light != null ? _world.Light.gameObject : null);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Floor);
            }

            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
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
            _presenter.Begin(_world.ActionReader, _view);
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
        public void BeginShowsTheFirstStepAtTheBottomOfTheHud()
        {
            Begin();

            Assert.That(_presenter.IsRunning, Is.True);
            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Move));
            Assert.That(_view.IsShown, Is.True);
            Assert.That(_view.Text, Does.StartWith("[1/6] ").And.EndWith(" で移動"));
            Assert.That(_view.Text, Is.EqualTo(_presenter.Prompt));
            Assert.That(_view.IsCompleted, Is.False);
        }

        [Test]
        public void WalkingMovesOnToAiming()
        {
            Begin();

            WalkOff();

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Aim));
            Assert.That(_view.Text, Does.StartWith("[2/6] "));
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
            Assert.That(_view.Text, Is.EqualTo(TutorialSteps.CompletedText));
            Assert.That(_view.IsCompleted, Is.True);

            _presenter.Tick(TutorialPresenter.CompletedDisplaySeconds, State(PlayerActionConditions.None), Vector3.zero, InputDeviceKind.KeyboardMouse);

            Assert.That(_presenter.IsRunning, Is.False);
            Assert.That(_view.IsShown, Is.False, "the completion is hidden after a while");
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
        public void TheKeyFollowsTheDevice()
        {
            var keyboardKey = TutorialPresenter.KeyLabelFor(TutorialStep.Move, InputDeviceKind.KeyboardMouse);
            var gamepadKey = TutorialPresenter.KeyLabelFor(TutorialStep.Move, InputDeviceKind.Gamepad);
            Assume.That(gamepadKey, Is.Not.EqualTo(keyboardKey), "the project-wide actions bind Move differently per device");
            Begin();

            Tick(Vector3.zero, device: InputDeviceKind.KeyboardMouse);
            Assert.That(_view.Text, Does.EndWith(TutorialSteps.Instruction(TutorialStep.Move, keyboardKey)));

            Tick(Vector3.zero, device: InputDeviceKind.Gamepad);
            Assert.That(_view.Text, Does.EndWith(TutorialSteps.Instruction(TutorialStep.Move, gamepadKey)));
        }

        [Test]
        public void StopHidesWithoutSaving()
        {
            Begin();

            _presenter.Stop();

            Assert.That(_presenter.IsRunning, Is.False);
            Assert.That(_view.IsShown, Is.False);
            Assert.That(_store.IsCompleted, Is.False);
            Assert.That(_presenter.Prompt, Is.Null);
        }

        [Test]
        public void BeginAgainRestartsFromTheFirstStep()
        {
            Begin();
            WalkOff();

            Begin();

            Assert.That(_presenter.Steps.Current, Is.EqualTo(TutorialStep.Move));
            Assert.That(_view.Text, Does.StartWith("[1/6] "));
        }

        [Test]
        public void EveryStepHasAnAction()
        {
            foreach (var step in TutorialSteps.Order)
            {
                Assert.That(TutorialPresenter.ActionPathFor(step), Does.StartWith("Player/"), step.ToString());
                Assert.That(TutorialPresenter.KeyLabelFor(step, InputDeviceKind.KeyboardMouse), Is.Not.Empty, step.ToString());
                Assert.That(TutorialPresenter.KeyLabelFor(step, InputDeviceKind.Gamepad), Is.Not.Empty, step.ToString());
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialPresenter.ActionPathFor((TutorialStep)99));
        }

        [Test]
        public void RejectsMissingPartsAndBadTime()
        {
            Assert.Throws<ArgumentNullException>(() => _presenter.Begin(null, _view));
            Assert.Throws<ArgumentNullException>(() => _presenter.Begin(_world.ActionReader, null));
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
