using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TutorialStepsTests
    {
        private const float _frame = 1f / 60f;

        private static TutorialObservation Frame(
            float moved = 0f,
            bool aiming = false,
            int hits = 0,
            bool reloading = false,
            bool crouching = false,
            bool inCover = false,
            float delta = _frame)
        {
            return new TutorialObservation(delta, moved, aiming, hits, reloading, crouching, inCover);
        }

        private static TutorialSteps AtStep(TutorialStep step)
        {
            var steps = new TutorialSteps();
            while (steps.Current != step)
            {
                Finish(steps);
            }

            return steps;
        }

        // 移動は 1 回の観測で跳び (DefaultTeleportMeters) を超えられないので、終えるまで繰り返す
        private static void Finish(TutorialSteps steps)
        {
            var step = steps.Current;
            for (var i = 0; i < 10; i++)
            {
                if (steps.Observe(Finishing(step)))
                {
                    return;
                }
            }

            Assert.Fail($"{step} did not finish");
        }

        private static TutorialObservation Finishing(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move:
                    return Frame(moved: 1f);
                case TutorialStep.Aim:
                    return Frame(aiming: true, delta: TutorialSteps.DefaultAimSeconds);
                case TutorialStep.Shoot:
                    return Frame(hits: TutorialSteps.DefaultHitsRequired);
                case TutorialStep.Reload:
                    return Frame(reloading: true);
                case TutorialStep.Crouch:
                    return Frame(crouching: true);
                case TutorialStep.Cover:
                    return Frame(inCover: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(step));
            }
        }

        [Test]
        public void TheStepsGoFromMoveToCover()
        {
            Assert.That(
                TutorialSteps.Order,
                Is.EqualTo(new[]
                {
                    TutorialStep.Move, TutorialStep.Aim, TutorialStep.Shoot, TutorialStep.Reload, TutorialStep.Crouch, TutorialStep.Cover,
                }));
        }

        [Test]
        public void StartsAtMove()
        {
            var steps = new TutorialSteps();

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Move));
            Assert.That(steps.CurrentIndex, Is.Zero);
            Assert.That(steps.IsComplete, Is.False);
            Assert.That(steps.StepProgress, Is.Zero);
        }

        [Test]
        public void WalkingTheDistanceFinishesMove()
        {
            var steps = new TutorialSteps(moveMeters: 3f);

            Assert.That(steps.Observe(Frame(moved: 1f)), Is.False);
            Assert.That(steps.Observe(Frame(moved: 1.5f)), Is.False);
            Assert.That(steps.StepProgress, Is.EqualTo(2.5f / 3f).Within(1e-5f));
            Assert.That(steps.Observe(Frame(moved: 0.5f)), Is.True);

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Aim));
        }

        [Test]
        public void ATeleportDoesNotCountAsWalking()
        {
            var steps = new TutorialSteps(moveMeters: 3f, teleportMeters: 2f);

            Assert.That(steps.Observe(Frame(moved: 50f)), Is.False);

            Assert.That(steps.StepProgress, Is.Zero);
            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Move));
        }

        [Test]
        public void OtherActionsDoNotFinishMove()
        {
            var steps = new TutorialSteps();

            Assert.That(steps.Observe(Frame(aiming: true, hits: 5, reloading: true, crouching: true, inCover: true)), Is.False);

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Move));
        }

        [Test]
        public void AimingAddsUpEvenWhenReleased()
        {
            var steps = AtStep(TutorialStep.Aim);

            Assert.That(steps.Observe(Frame(aiming: true, delta: 0.3f)), Is.False);
            Assert.That(steps.Observe(Frame(aiming: false, delta: 5f)), Is.False, "time spent not aiming is not counted");
            Assert.That(steps.Observe(Frame(aiming: true, delta: 0.2f)), Is.True);

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Shoot));
        }

        [Test]
        public void ZeroAimSecondsFinishesOnTheFirstAimingFrame()
        {
            var steps = new TutorialSteps(aimSeconds: 0f);
            Finish(steps);

            Assert.That(steps.Observe(Frame(aiming: false)), Is.False);
            Assert.That(steps.Observe(Frame(aiming: true, delta: 0f)), Is.True);
        }

        [Test]
        public void HittingTheTargetsEnoughTimesFinishesShoot()
        {
            var steps = AtStep(TutorialStep.Shoot);

            Assert.That(steps.Observe(Frame(hits: 1)), Is.False);
            Assert.That(steps.Hits, Is.EqualTo(1));
            Assert.That(steps.Prompt("LMB"), Does.EndWith("(1/3)"));
            Assert.That(steps.Observe(Frame(hits: 1)), Is.False);
            Assert.That(steps.Observe(Frame(hits: 1)), Is.True);

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Reload));
            Assert.That(steps.Hits, Is.Zero);
        }

        [TestCase(TutorialStep.Reload)]
        [TestCase(TutorialStep.Crouch)]
        [TestCase(TutorialStep.Cover)]
        public void TheStateStepsFinishWhenTheStateIsSeen(TutorialStep step)
        {
            var steps = AtStep(step);

            Assert.That(steps.Observe(Frame()), Is.False);
            Assert.That(steps.Observe(Finishing(step)), Is.True);
        }

        [Test]
        public void WhatWasDoneInAnEarlierStepIsNotCarriedOver()
        {
            var steps = new TutorialSteps();

            // 移動の手順で当てた分は射撃の手順に数えない
            steps.Observe(Frame(moved: 1f, hits: 3, aiming: true, delta: 1f));
            steps.Observe(Frame(moved: 2f));
            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Aim));
            Assert.That(steps.StepProgress, Is.Zero);

            Finish(steps);
            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Shoot));
            Assert.That(steps.Hits, Is.Zero);
        }

        [Test]
        public void OneObservationAdvancesOnlyOneStep()
        {
            var steps = AtStep(TutorialStep.Reload);

            Assert.That(steps.Observe(Frame(reloading: true, crouching: true, inCover: true)), Is.True);

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Crouch), "the crouch is counted on the next observation");
            Assert.That(steps.Observe(Frame(crouching: true, inCover: true)), Is.True);
            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Cover));
        }

        [Test]
        public void FinishingCoverCompletesTheTutorial()
        {
            var steps = AtStep(TutorialStep.Cover);

            Assert.That(steps.Observe(Frame(inCover: true)), Is.True);

            Assert.That(steps.IsComplete, Is.True);
            Assert.That(steps.CurrentIndex, Is.EqualTo(TutorialSteps.Order.Count));
            Assert.That(steps.StepProgress, Is.EqualTo(1f));
            Assert.That(steps.Prompt(null), Is.EqualTo(TutorialSteps.CompletedText));
            Assert.Throws<InvalidOperationException>(() => _ = steps.Current);
            Assert.That(steps.Observe(Frame(inCover: true)), Is.False, "nothing happens after completion");
        }

        [Test]
        public void RestartGoesBackToTheFirstStep()
        {
            var steps = AtStep(TutorialStep.Shoot);
            steps.Observe(Frame(hits: 1));

            steps.Restart();

            Assert.That(steps.Current, Is.EqualTo(TutorialStep.Move));
            Assert.That(steps.Hits, Is.Zero);
            Assert.That(steps.StepProgress, Is.Zero);
        }

        [Test]
        public void ThePromptNumbersTheStepAndNamesTheKey()
        {
            var steps = new TutorialSteps();

            Assert.That(steps.Prompt("WASD"), Is.EqualTo("[1/6] WASD で移動"));
            Assert.That(steps.Prompt(" Left Stick "), Is.EqualTo("[1/6] Left Stick で移動"));
            Assert.Throws<ArgumentException>(() => steps.Prompt(" "));
        }

        [Test]
        public void EveryStepHasAnInstructionWithTheKey()
        {
            foreach (var step in TutorialSteps.Order)
            {
                Assert.That(TutorialSteps.Instruction(step, "KEY"), Does.Contain("KEY"), step.ToString());
            }

            Assert.That(TutorialSteps.Instruction(TutorialStep.Shoot, "RT", 5), Does.Contain("5 回"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialSteps.Instruction((TutorialStep)99, "KEY"));
            Assert.Throws<ArgumentOutOfRangeException>(() => TutorialSteps.Instruction(TutorialStep.Shoot, "RT", 0));
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        [TestCase(false, true, true)]
        public void StartsTheFirstTimeOrWhenAskedFromTheMenu(bool completed, bool requested, bool expected)
        {
            Assert.That(TutorialSteps.ShouldStart(completed, requested), Is.EqualTo(expected));
        }

        [Test]
        public void SingleLetterKeysAreJoinedAndLongerOnesAreSeparated()
        {
            Assert.That(TutorialSteps.JoinDirectionalKeys(new[] { "W", "A", "S", "D" }), Is.EqualTo("WASD"));
            Assert.That(TutorialSteps.JoinDirectionalKeys(new[] { "Up", "Left", "Down", "Right" }), Is.EqualTo("Up/Left/Down/Right"));
            Assert.That(TutorialSteps.JoinDirectionalKeys(new[] { "W", null, " ", "D" }), Is.EqualTo("WD"));
            Assert.That(TutorialSteps.JoinDirectionalKeys(Array.Empty<string>()), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => TutorialSteps.JoinDirectionalKeys(null));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(moveMeters: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(moveMeters: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(aimSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(aimSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(hitsRequired: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialSteps(teleportMeters: 0f));
        }

        [Test]
        public void ObservationRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Frame(delta: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Frame(delta: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Frame(moved: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Frame(moved: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Frame(hits: -1));
        }
    }
}
