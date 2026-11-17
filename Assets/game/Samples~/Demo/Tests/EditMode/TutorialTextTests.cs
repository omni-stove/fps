using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class TutorialTextTests
    {
        private static TutorialObservation Frame(float delta = 1f / 60f, float moved = 0f, bool aiming = false, int hits = 0)
        {
            return new TutorialObservation(delta, moved, aiming, hits, false, false, false);
        }

        private static TutorialSteps AtShoot()
        {
            var steps = new TutorialSteps();
            for (var i = 0; i < 10 && steps.Current == TutorialStep.Move; i++)
            {
                steps.Observe(Frame(moved: 1f));
            }

            steps.Observe(Frame(delta: TutorialSteps.DefaultAimSeconds, aiming: true));
            Assume.That(steps.Current, Is.EqualTo(TutorialStep.Shoot));
            return steps;
        }

        private static TutorialStatus Status(TutorialSteps steps, string actionPath = InputActionPaths.Move)
        {
            return new TutorialStatus(steps, actionPath, InputDeviceKind.KeyboardMouse);
        }

        [Test]
        public void ThePromptNumbersTheStepAndNamesTheKey()
        {
            var status = Status(new TutorialSteps());

            Assert.That(Localizer.Resolve(TutorialText.Prompt(status, "WASD")), Is.EqualTo("[1/6] WASD で移動"));
            Assert.That(Localizer.Resolve(TutorialText.Prompt(status, " Left Stick ")), Is.EqualTo("[1/6] Left Stick で移動"));
            Assert.Throws<ArgumentException>(() => TutorialText.Prompt(status, " "));
        }

        [Test]
        public void TheShootPromptCountsTheHits()
        {
            var steps = AtShoot();
            steps.Observe(Frame(hits: 1));

            Assert.That(
                Localizer.Resolve(TutorialText.Prompt(Status(steps, InputActionPaths.Attack), "LMB")),
                Is.EqualTo("[3/6] LMB で撃って標的に 3 回当てる (1/3)"));
        }

        [Test]
        public void TheShootPromptUsesTheConfiguredHits()
        {
            var steps = new TutorialSteps(hitsRequired: 5);
            for (var i = 0; i < 10 && steps.Current == TutorialStep.Move; i++)
            {
                steps.Observe(Frame(moved: 1f));
            }

            steps.Observe(Frame(delta: TutorialSteps.DefaultAimSeconds, aiming: true));

            Assert.That(
                Localizer.Resolve(TutorialText.Prompt(Status(steps, InputActionPaths.Attack), "RT")),
                Is.EqualTo("[3/6] RT で撃って標的に 5 回当てる (0/5)"));
        }

        [Test]
        public void ACompletedStatusIsTheCompletedText()
        {
            var steps = AtShoot();
            steps.Observe(Frame(hits: TutorialSteps.DefaultHitsRequired));
            steps.Observe(new TutorialObservation(1f / 60f, 0f, false, 0, true, false, false));
            steps.Observe(new TutorialObservation(1f / 60f, 0f, false, 0, false, true, false));
            steps.Observe(new TutorialObservation(1f / 60f, 0f, false, 0, false, false, true));
            var status = new TutorialStatus(steps, null, InputDeviceKind.KeyboardMouse);

            Assert.That(TutorialText.Prompt(status, null), Is.EqualTo(TutorialText.CompletedText));
            Assert.That(TutorialText.For(status), Is.EqualTo(TutorialText.CompletedText));
            Assert.That(Localizer.Resolve(TutorialText.CompletedText), Is.EqualTo("チュートリアル完了"));
        }

        [Test]
        public void EveryStepHasAnInstructionWithTheKey()
        {
            foreach (var step in TutorialSteps.Order)
            {
                Assert.That(Localizer.Resolve(TutorialText.Instruction(step, "KEY")), Does.Contain("KEY"), step.ToString());
            }

            Assert.That(Localizer.Resolve(TutorialText.Instruction(TutorialStep.Shoot, "RT", 5)), Does.Contain("5 回"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialText.Instruction((TutorialStep)99, "KEY"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialText.Instruction(TutorialStep.Shoot, "RT", 0));
        }

        [Test]
        public void ForNamesTheKeyOfTheStatusAction()
        {
            var status = Status(new TutorialSteps());
            var key = TutorialText.KeyLabelFor(InputActionPaths.Move, InputDeviceKind.KeyboardMouse);

            Assert.That(TutorialText.For(status), Is.EqualTo(TutorialText.Prompt(status, key)));
        }

        [Test]
        public void EveryStepActionHasAKeyOnEachDevice()
        {
            foreach (var step in TutorialSteps.Order)
            {
                var path = TutorialPresenter.ActionPathFor(step);
                Assert.That(TutorialText.KeyLabelFor(path, InputDeviceKind.KeyboardMouse), Is.Not.Empty, step.ToString());
                Assert.That(TutorialText.KeyLabelFor(path, InputDeviceKind.Gamepad), Is.Not.Empty, step.ToString());
            }
        }

        [Test]
        public void TheKeyFollowsTheDevice()
        {
            var keyboardKey = TutorialText.KeyLabelFor(InputActionPaths.Move, InputDeviceKind.KeyboardMouse);
            var gamepadKey = TutorialText.KeyLabelFor(InputActionPaths.Move, InputDeviceKind.Gamepad);
            Assume.That(gamepadKey, Is.Not.EqualTo(keyboardKey), "the project-wide actions bind Move differently per device");
            var steps = new TutorialSteps();

            Assert.That(
                Localizer.Resolve(TutorialText.For(new TutorialStatus(steps, InputActionPaths.Move, InputDeviceKind.Gamepad))),
                Does.EndWith(Localizer.Resolve(TutorialText.Instruction(TutorialStep.Move, gamepadKey))));
            Assert.That(
                Localizer.Resolve(TutorialText.For(new TutorialStatus(steps, InputActionPaths.Move, InputDeviceKind.KeyboardMouse))),
                Does.EndWith(Localizer.Resolve(TutorialText.Instruction(TutorialStep.Move, keyboardKey))));
        }

        [Test]
        public void AnUnknownActionIsNamedByItsLastPart()
        {
            Assert.That(TutorialText.KeyLabelFor("Player/NoSuchAction", InputDeviceKind.KeyboardMouse), Is.EqualTo("NoSuchAction"));
            Assert.Throws<ArgumentException>(() => TutorialText.KeyLabelFor(" ", InputDeviceKind.KeyboardMouse));
        }
    }
}
