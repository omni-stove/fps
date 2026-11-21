using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Rigidbody に入れた速さ (または置き直した位置) から Refresh が出す歩き方と、Animator へ渡す値を確かめる。
    public sealed class HorseGaitAnimatorTests
    {
        private const float Epsilon = 1e-3f;
        private const float FrameSeconds = 0.02f;

        // 既定の境目 (Trot 2.5 m/s 以上、Canter 6 m/s 未満) の間。
        private const float TrotRangeSpeed = 3.5f;

        private readonly List<Object> _created = new List<Object>();
        private GameObject _horse;

        [SetUp]
        public void SetUp()
        {
            _horse = new GameObject("Horse");
            _created.Add(_horse);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheDefaultsNameGaitSpeedAndJump()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();

            Assert.That(gait.GaitParameter, Is.EqualTo("gait"));
            Assert.That(gait.SpeedParameter, Is.EqualTo("speed"));
            Assert.That(gait.JumpTrigger, Is.EqualTo("jump"));
            Assert.That(gait.JumpRiseSpeed, Is.EqualTo(HorseGaitAnimator.DefaultJumpRiseSpeed));
            Assert.That(gait.Animator, Is.Null);
            Assert.That(gait.Body, Is.Not.Null, "a Rigidbody is added with the component");
        }

        [Test]
        public void ASpeedInTheTrotRangeGivesTheTrotGait()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Body.linearVelocity = Vector3.forward * TrotRangeSpeed;

            gait.Refresh(FrameSeconds);

            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Trot));
            Assert.That(gait.GaitValue, Is.EqualTo(2));
            Assert.That(gait.ForwardSpeed, Is.EqualTo(TrotRangeSpeed).Within(Epsilon));
        }

        [Test]
        public void TheGaitFollowsTheSpeedThroughEveryStage()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();

            gait.Body.linearVelocity = Vector3.zero;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Idle));

            gait.Body.linearVelocity = Vector3.forward * 1f;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Walk));

            gait.Body.linearVelocity = Vector3.forward * 7f;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Canter));
            Assert.That(gait.GaitValue, Is.EqualTo(3));
        }

        [Test]
        public void BackingUpIsAWalkWithANegativeSpeed()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Body.linearVelocity = Vector3.back * 1f;

            gait.Refresh(FrameSeconds);

            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Walk));
            Assert.That(gait.ForwardSpeed, Is.EqualTo(-1f).Within(Epsilon));
        }

        [Test]
        public void OnlyTheForwardPartOfTheVelocityCounts()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            _horse.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            gait.Body.rotation = _horse.transform.rotation;
            gait.Body.linearVelocity = new Vector3(TrotRangeSpeed, 0f, 8f);

            gait.Refresh(FrameSeconds);

            Assert.That(gait.ForwardSpeed, Is.EqualTo(TrotRangeSpeed).Within(Epsilon), "the head faces +x; sliding along z is not a stride");
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Trot));
        }

        [Test]
        public void TheThresholdsComeFromTheMotorsDefinition()
        {
            var motor = _horse.AddComponent<SimpleHorseMotor>();
            var definition = ScriptableObject.CreateInstance<HorseDefinition>();
            _created.Add(definition);
            definition.CopyFrom(new HorseSpec(trotThreshold: 4f));
            motor.Definition = definition;
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Body.linearVelocity = Vector3.forward * TrotRangeSpeed;

            gait.Refresh(FrameSeconds);

            Assert.That(gait.Spec.TrotThreshold, Is.EqualTo(4f));
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Walk), "3.5 m/s is below this horse's trot threshold of 4");
        }

        [Test]
        public void WithoutAMotorTheThresholdsAreTheDefaults()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();

            Assert.That(gait.Spec.WalkThreshold, Is.EqualTo(HorseSpec.DefaultWalkThreshold));
            Assert.That(gait.Spec.TrotThreshold, Is.EqualTo(HorseSpec.DefaultTrotThreshold));
            Assert.That(gait.Spec.CanterThreshold, Is.EqualTo(HorseSpec.DefaultCanterThreshold));
        }

        [Test]
        public void AKinematicHorseIsMeasuredFromWhereItIsPlaced()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Body.isKinematic = true;

            gait.Refresh(FrameSeconds);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Idle), "the first frame has nothing to compare with");

            _horse.transform.position = Vector3.forward * (TrotRangeSpeed * FrameSeconds);
            gait.Refresh(FrameSeconds);

            Assert.That(gait.ForwardSpeed, Is.EqualTo(TrotRangeSpeed).Within(Epsilon));
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Trot));

            gait.Refresh(0f);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Trot), "a frame with no time keeps the last speed instead of dividing by zero");

            gait.Refresh(FrameSeconds);
            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Idle), "it stood still for a frame");
        }

        [Test]
        public void TheJumpIsSeenOnlyWhenTheRiseStarts()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();

            gait.Body.linearVelocity = Vector3.up * HorseSpec.DefaultJumpSpeed;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Jumped, Is.True);

            gait.Refresh(FrameSeconds);
            Assert.That(gait.Jumped, Is.False, "still rising is the same jump");

            gait.Body.linearVelocity = Vector3.down * 2f;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Jumped, Is.False);

            gait.Body.linearVelocity = Vector3.up * HorseSpec.DefaultJumpSpeed;
            gait.Refresh(FrameSeconds);
            Assert.That(gait.Jumped, Is.True, "a second rise is a second jump");
        }

        [Test]
        public void ASlowRiseIsNotAJump()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.JumpRiseSpeed = 2f;
            gait.Body.linearVelocity = Vector3.up * 1.5f;

            gait.Refresh(FrameSeconds);

            Assert.That(gait.Jumped, Is.False);
        }

        [Test]
        public void AnEmptyAnimatorDoesNothingAndDoesNotThrow()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Body.linearVelocity = (Vector3.forward * TrotRangeSpeed) + (Vector3.up * HorseSpec.DefaultJumpSpeed);

            Assert.DoesNotThrow(() => gait.Refresh(FrameSeconds));

            Assert.That(gait.Gait, Is.EqualTo(HorseGait.Trot), "the gait is still worked out for whoever reads it");
            Assert.That(gait.IsMissingParameters, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AnAnimatorWithoutAControllerDoesNothingAndDoesNotWarn()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            gait.Animator = _horse.AddComponent<Animator>();
            gait.Body.linearVelocity = Vector3.forward * TrotRangeSpeed;

            Assert.DoesNotThrow(() => gait.Refresh(FrameSeconds));

            Assert.That(gait.IsMissingParameters, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheGaitParameterIsSetToTheTrotValue()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            var animator = AnimatorWith(
                ("gait", AnimatorControllerParameterType.Int),
                ("speed", AnimatorControllerParameterType.Float),
                ("jump", AnimatorControllerParameterType.Trigger));
            gait.Animator = animator;
            gait.Body.linearVelocity = Vector3.forward * TrotRangeSpeed;

            gait.Refresh(FrameSeconds);

            Assert.That(animator.GetInteger("gait"), Is.EqualTo((int)HorseGait.Trot));
            Assert.That(animator.GetFloat("speed"), Is.EqualTo(TrotRangeSpeed).Within(Epsilon));
            Assert.That(gait.IsMissingParameters, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RenamedParametersAreTheOnesSet()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            var animator = AnimatorWith(("Stride", AnimatorControllerParameterType.Int));
            gait.GaitParameter = "Stride";
            gait.SpeedParameter = string.Empty;
            gait.JumpTrigger = string.Empty;
            gait.Animator = animator;
            gait.Body.linearVelocity = Vector3.forward * 7f;

            gait.Refresh(FrameSeconds);

            Assert.That(animator.GetInteger("Stride"), Is.EqualTo((int)HorseGait.Canter));
            Assert.That(gait.IsMissingParameters, Is.False, "an empty name is switched off, not missing");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingParametersAreToldOnceAndSkipped()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();
            var animator = AnimatorWith(
                ("speed", AnimatorControllerParameterType.Float),
                ("gait", AnimatorControllerParameterType.Float));
            gait.Animator = animator;
            gait.Body.linearVelocity = Vector3.forward * TrotRangeSpeed;

            LogAssert.Expect(
                LogType.Warning,
                new Regex("^" + Regex.Escape(VehicleLog.Tag) + @" 'Horse' animator controller 'HorseController' has no parameter gait, jump;"));
            gait.Refresh(FrameSeconds);
            gait.Refresh(FrameSeconds);

            Assert.That(gait.IsMissingParameters, Is.True, "a float named gait is not the int it needs");
            Assert.That(animator.GetFloat("gait"), Is.EqualTo(0f), "the wrong-typed parameter is left alone");
            Assert.That(animator.GetFloat("speed"), Is.EqualTo(TrotRangeSpeed).Within(Epsilon), "the parameter that is there still gets set");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABadDeltaOrRiseSpeedIsRefused()
        {
            var gait = _horse.AddComponent<HorseGaitAnimator>();

            Assert.Throws<ArgumentOutOfRangeException>(() => gait.Refresh(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => gait.Refresh(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => gait.Refresh(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => gait.JumpRiseSpeed = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => gait.JumpRiseSpeed = float.NaN);
        }

        [Test]
        public void TheMissingParameterLineNamesTheVehicleControllerAndParameters()
        {
            Assert.That(
                VehicleLog.MissingAnimatorParameters("Horse", "HorseController", new[] { "gait", "jump" }),
                Is.EqualTo("[Vehicle] 'Horse' animator controller 'HorseController' has no parameter gait, jump; the gait animator does not set them"));
            Assert.That(
                VehicleLog.MissingAnimatorParameters("Horse", "HorseController", Array.Empty<string>()),
                Is.EqualTo("[Vehicle] 'Horse' animator controller 'HorseController' has no parameter -; the gait animator does not set them"));
            Assert.Throws<ArgumentNullException>(() => VehicleLog.MissingAnimatorParameters("Horse", "HorseController", null));
        }

        // アセットに書かず、メモリ上の AnimatorController を Animator に持たせる。Edit Mode で Animator が初期化されない環境では確かめようがないので飛ばす。
        private Animator AnimatorWith(params (string Name, AnimatorControllerParameterType Type)[] parameters)
        {
            var controller = new AnimatorController { name = "HorseController" };
            _created.Add(controller);
            controller.AddLayer("Base Layer");
            foreach (var (parameterName, type) in parameters)
            {
                controller.AddParameter(parameterName, type);
            }

            var animator = _horse.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            Assume.That(animator.isInitialized, Is.True, "the Animator must be initialized to take parameters");
            return animator;
        }
    }
}
