using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayerMockPoseTests
    {
        private const float Tolerance = 1e-4f;

        private static PlayerActionState State(PlayerActionConditions flags, ViewMode view = ViewMode.ThirdPerson)
        {
            var reload = (flags & PlayerActionConditions.Reloading) != 0 ? 0.5f : 0f;
            return new PlayerActionState(flags, reload, null, ShoulderSide.Right, view, "Rifle", 0f);
        }

        [Test]
        public void CrouchShrinksTheTorsoAndLowersTheHead()
        {
            var standing = PlayerMockPose.StanceScale(State(PlayerActionConditions.Grounded));
            var crouching = PlayerMockPose.StanceScale(State(PlayerActionConditions.Crouching));

            Assert.That(standing, Is.EqualTo(1f));
            Assert.That(crouching, Is.EqualTo(PlayerMockPose.CrouchScale));

            PlayerMockPose.Torso(standing, out _, out var standScale);
            PlayerMockPose.Torso(crouching, out _, out var crouchScale);
            Assert.That(crouchScale.y, Is.LessThan(standScale.y));
            Assert.That(crouchScale.x, Is.EqualTo(standScale.x), "only the height shrinks");
            Assert.That(PlayerMockPose.Head(crouching).y, Is.LessThan(PlayerMockPose.Head(standing).y));
        }

        [Test]
        public void ProneShrinksTheTorsoBelowTheCrouch()
        {
            var prone = PlayerMockPose.StanceScale(State(PlayerActionConditions.Prone));

            Assert.That(prone, Is.EqualTo(PlayerMockPose.ProneScale));
            Assert.That(prone, Is.LessThan(PlayerMockPose.CrouchScale));
            Assert.That(PlayerMockPose.StanceScale(State(PlayerActionConditions.Prone | PlayerActionConditions.Downed)), Is.EqualTo(1f));
        }

        [Test]
        public void TheStandingTopIsTheTopOfTheHead()
        {
            Assert.That(PlayerMockPose.StandingTop, Is.EqualTo(PlayerMockPose.Head(1f).y + (PlayerMockPose.HeadDiameter / 2f)).Within(Tolerance));
        }

        // 立った頭のてっぺんは 1.81 m、一番低い姿勢は 1 m。その間の壁では、頭のてっぺんが上端の 0.05 m 下へ来るまで縮む。
        [TestCase(1.5f)]
        [TestCase(1.7f)]
        [TestCase(1.3f)]
        [TestCase(1.1f)]
        public void InCoverTheHeadTopSitsJustBelowAMidHeightWall(float wallHeight)
        {
            var amount = PlayerMockPose.CoverCrouchAmount(true, false, wallHeight);
            var scale = PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.InCover), null, amount);

            var headTop = PlayerMockPose.Head(scale).y + (PlayerMockPose.HeadDiameter / 2f);
            Assert.That(headTop, Is.EqualTo(wallHeight - CoverCrouch.DefaultHeadClearance).Within(Tolerance));
        }

        [Test]
        public void InCoverALowWallCrouchesBelowTheCrouchDownToTheLowestPose()
        {
            var amount = PlayerMockPose.CoverCrouchAmount(true, false, 0.8f);
            var scale = PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.InCover | PlayerActionConditions.Crouching), null, amount);

            Assert.That(amount, Is.EqualTo(1f));
            Assert.That(scale, Is.EqualTo(PlayerMockPose.CoverLowestScale).Within(Tolerance));
            Assert.That(scale, Is.LessThan(PlayerMockPose.CrouchScale));
            Assert.That(PlayerMockPose.Head(scale).y + (PlayerMockPose.HeadDiameter / 2f), Is.EqualTo(PlayerMockPose.CoverLowestTop).Within(Tolerance));
        }

        [Test]
        public void InCoverAWallAboveTheHeadKeepsStanding()
        {
            var amount = PlayerMockPose.CoverCrouchAmount(true, false, 2f);

            Assert.That(amount, Is.EqualTo(0f));
            Assert.That(PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.InCover), null, amount), Is.EqualTo(1f));
        }

        [Test]
        public void PeekingOrOutOfCoverDoesNotCrouchForTheWall()
        {
            Assert.That(PlayerMockPose.CoverCrouchAmount(true, true, 1.5f), Is.EqualTo(0f));
            Assert.That(PlayerMockPose.CoverCrouchAmount(false, false, 1.5f), Is.EqualTo(0f));
            Assert.That(PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.Grounded), null, 1f), Is.EqualTo(1f), "the amount only applies in cover");
        }

        [Test]
        public void DownedOrProneInCoverKeepsItsOwnScale()
        {
            Assert.That(PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.InCover | PlayerActionConditions.Downed), null, 1f), Is.EqualTo(1f));
            Assert.That(
                PlayerMockPose.CoverStanceScale(State(PlayerActionConditions.InCover | PlayerActionConditions.Prone), null, 1f),
                Is.EqualTo(PlayerMockPose.ProneScale));
        }

        [Test]
        public void TorsoStandsOnTheFeet()
        {
            PlayerMockPose.Torso(1f, out var position, out var scale);

            // Unity のカプセルは拡縮 1 で高さ 2 なので、下端は中心から scale.y 下。
            Assert.That(position.y - scale.y, Is.EqualTo(PlayerMockPose.TorsoBottom).Within(Tolerance));
            Assert.That(PlayerMockPose.Head(1f).y - (PlayerMockPose.HeadDiameter / 2f), Is.EqualTo(position.y + scale.y).Within(Tolerance));
        }

        [Test]
        public void DownedIgnoresCrouchAndFallsSideways()
        {
            var state = State(PlayerActionConditions.Downed | PlayerActionConditions.Crouching | PlayerActionConditions.InCover);

            Assert.That(PlayerMockPose.StanceScale(state), Is.EqualTo(1f));
            var up = PlayerMockPose.BodyLean(state, Vector3.forward, Vector3.back) * Vector3.up;
            Assert.That(Mathf.Abs(up.x), Is.GreaterThan(0.9f), "lies on its side");
            Assert.That(PlayerMockPose.BodyShift(state, Vector3.back), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DodgeLeansTowardTheMoveDirection()
        {
            var state = State(PlayerActionConditions.Dodging);

            var right = PlayerMockPose.BodyLean(state, new Vector3(5f, 1f, 0f), Vector3.zero) * Vector3.up;
            Assert.That(right.x, Is.GreaterThan(0.2f));
            Assert.That(Mathf.Abs(right.z), Is.LessThan(Tolerance));

            var still = PlayerMockPose.BodyLean(state, Vector3.zero, Vector3.zero) * Vector3.up;
            Assert.That(still.z, Is.GreaterThan(0.2f), "falls back to forward without a horizontal velocity");
        }

        [Test]
        public void VaultLeansForward()
        {
            var up = PlayerMockPose.BodyLean(State(PlayerActionConditions.Vaulting | PlayerActionConditions.Dodging), Vector3.left, Vector3.zero)
                * Vector3.up;

            Assert.That(up.z, Is.GreaterThan(0.3f));
            Assert.That(Mathf.Abs(up.x), Is.LessThan(Tolerance), "vault wins over dodge");
        }

        [Test]
        public void CoverMovesTheBodyTowardTheWall()
        {
            var state = State(PlayerActionConditions.InCover);
            var wallNormal = Vector3.back; // 壁は前にあり、面は手前を向く

            var shift = PlayerMockPose.BodyShift(state, wallNormal);
            Assert.That(shift.z, Is.EqualTo(PlayerMockPose.CoverShift).Within(Tolerance));
            Assert.That((PlayerMockPose.BodyLean(state, Vector3.zero, wallNormal) * Vector3.up).z, Is.GreaterThan(0f));
            Assert.That(PlayerMockPose.BodyShift(State(PlayerActionConditions.Grounded), wallNormal), Is.EqualTo(Vector3.zero));
        }

        [TestCase(1f, 15f, 15f)]
        [TestCase(-1f, 15f, -15f)]
        [TestCase(0.5f, 20f, 10f)]
        [TestCase(-0.5f, 30f, -15f)]
        public void LeanTiltsTheBodySidewaysByTheLeanAngle(float lean, float leanAngle, float expectedDegrees)
        {
            var up = PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(lean: lean), leanAngle) * Vector3.up;

            Assert.That(up.x, Is.EqualTo(Mathf.Sin(expectedDegrees * Mathf.Deg2Rad)).Within(Tolerance), "a positive lean tips the head to the right");
            Assert.That(up.y, Is.EqualTo(Mathf.Cos(expectedDegrees * Mathf.Deg2Rad)).Within(Tolerance));
            Assert.That(Mathf.Abs(up.z), Is.LessThan(Tolerance), "leaning does not pitch");
        }

        [Test]
        public void NoLeanStandsUpright()
        {
            Assert.That(PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(), 15f), Is.EqualTo(Quaternion.identity));
            Assert.That(PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(lean: 1f), 0f), Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void SwimmingPitchesTheBodyForward()
        {
            var up = PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(lean: 1f, swimming: true), 15f) * Vector3.up;

            Assert.That(up.z, Is.EqualTo(Mathf.Sin(PlayerMockPose.SwimPitchDegrees * Mathf.Deg2Rad)).Within(Tolerance));
            Assert.That(up.y, Is.EqualTo(Mathf.Cos(PlayerMockPose.SwimPitchDegrees * Mathf.Deg2Rad)).Within(Tolerance));
            Assert.That(Mathf.Abs(up.x), Is.LessThan(Tolerance), "swimming wins over the lean");
        }

        [TestCase(WallRunSide.Left, 1f)]
        [TestCase(WallRunSide.Right, -1f)]
        public void WallRunTiltsAwayFromTheWall(WallRunSide side, float awaySign)
        {
            var up = PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(lean: -awaySign, swimming: true, wallRun: side), 15f)
                * Vector3.up;

            Assert.That(up.x, Is.EqualTo(awaySign * Mathf.Sin(PlayerMockPose.WallRunTiltDegrees * Mathf.Deg2Rad)).Within(Tolerance));
            Assert.That(Mathf.Abs(up.z), Is.LessThan(Tolerance), "the wall run wins over swimming and the lean");
        }

        [Test]
        public void AnimatorParametersTiltLikeTheMotorState()
        {
            var leaning = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, lean: 0.5f);
            var swimming = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, swimming: true);
            var wallRunning = new CharacterPoseParameters(
                0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, wallRun: CharacterPose.WallRunRight);

            Assert.That(PlayerMockPose.TraversalLean(leaning, 15f), Is.EqualTo(PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(lean: 0.5f), 15f)));
            Assert.That(PlayerMockPose.TraversalLean(swimming, 15f), Is.EqualTo(Quaternion.Euler(PlayerMockPose.SwimPitchDegrees, 0f, 0f)));
            Assert.That(
                PlayerMockPose.TraversalLean(wallRunning, 15f),
                Is.EqualTo(PlayerMockPose.TraversalLean(FakeCharacterMotorStateSource.Motor(wallRun: WallRunSide.Right), 15f)));
        }

        [Test]
        public void DownedVaultDodgeAndCoverWinOverTheMotorPoses()
        {
            var motor = FakeCharacterMotorStateSource.Motor(lean: 1f, wallRun: WallRunSide.Left);
            foreach (var flags in new[]
                     {
                         PlayerActionConditions.Downed, PlayerActionConditions.Vaulting, PlayerActionConditions.Dodging, PlayerActionConditions.InCover,
                     })
            {
                var state = State(flags);

                Assert.That(
                    PlayerMockPose.BodyLean(state, Vector3.forward, Vector3.back, motor, 15f),
                    Is.EqualTo(PlayerMockPose.BodyLean(state, Vector3.forward, Vector3.back)),
                    flags.ToString());
            }

            Assert.That(
                PlayerMockPose.BodyLean(State(PlayerActionConditions.Grounded), Vector3.forward, Vector3.zero, motor, 15f),
                Is.EqualTo(PlayerMockPose.TraversalLean(motor, 15f)),
                "otherwise the motor decides");
        }

        [Test]
        public void TraversalLeanRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(1.5f, false, WallRunSide.None, 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(float.NaN, false, WallRunSide.None, 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(0f, false, WallRunSide.None, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(0f, false, WallRunSide.None, 91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(0f, false, WallRunSide.None, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.TraversalLean(0f, false, (WallRunSide)7, 15f));
        }

        [Test]
        public void AdsRaisesTheGunToTheEyesAndReloadLowersAndTiltsIt()
        {
            var hip = PlayerMockPose.BodyGun(State(PlayerActionConditions.Grounded), Quaternion.identity);
            var aim = PlayerMockPose.BodyGun(State(PlayerActionConditions.Aiming), Quaternion.identity);
            var reload = PlayerMockPose.BodyGun(State(PlayerActionConditions.Reloading), Quaternion.identity);

            Assert.That(aim.Position.y, Is.GreaterThan(hip.Position.y));
            Assert.That(aim.Position.y, Is.EqualTo(PlayerMockPose.EyeHeight).Within(0.1f));
            Assert.That(reload.Position.y, Is.LessThan(hip.Position.y));
            Assert.That(Quaternion.Angle(reload.Rotation, Quaternion.identity), Is.GreaterThan(10f));
        }

        [Test]
        public void BodyGunFollowsTheAimPitchUnlessLowered()
        {
            var pitch = Quaternion.Euler(-20f, 0f, 0f);

            Assert.That(PlayerMockPose.BodyGun(State(PlayerActionConditions.Aiming), pitch).Rotation, Is.EqualTo(pitch));
            Assert.That(
                PlayerMockPose.BodyGun(State(PlayerActionConditions.Reloading), pitch).Rotation,
                Is.EqualTo(Quaternion.Euler(PlayerMockPose.BodyReloadGunEuler)));
        }

        [Test]
        public void ViewModelSitsBottomRightAndCentersWhileAiming()
        {
            var hip = PlayerMockPose.ViewModel(State(PlayerActionConditions.Grounded, ViewMode.FirstPerson));
            var aim = PlayerMockPose.ViewModel(State(PlayerActionConditions.Aiming, ViewMode.FirstPerson));
            var reload = PlayerMockPose.ViewModel(State(PlayerActionConditions.Reloading, ViewMode.FirstPerson));

            Assert.That(hip.Position.x, Is.GreaterThan(0f));
            Assert.That(hip.Position.y, Is.LessThan(0f));
            Assert.That(aim.Position.x, Is.EqualTo(0f));
            Assert.That(Mathf.Abs(aim.Position.y), Is.LessThan(Mathf.Abs(hip.Position.y)));
            Assert.That(reload.Position.y, Is.LessThan(hip.Position.y));
        }

        [Test]
        public void SwitchingLowersTheGunOutOfTheViewAndBringsItBack()
        {
            var held = PlayerMockPose.ViewModel(State(PlayerActionConditions.Grounded, ViewMode.FirstPerson));
            var half = PlayerMockPose.ViewModel(Lowering(0.5f));
            var away = PlayerMockPose.ViewModel(Lowering(1f));

            Assert.That(PlayerMockPose.ViewModel(Lowering(0f)), Is.EqualTo(held));
            Assert.That(away.Position, Is.EqualTo(PlayerMockPose.ViewLoweredPosition));
            Assert.That(away.Rotation, Is.EqualTo(Quaternion.Euler(PlayerMockPose.ViewLoweredEuler)));
            Assert.That(half.Position.y, Is.LessThan(held.Position.y).And.GreaterThan(away.Position.y));
            Assert.That((away.Rotation * Vector3.forward).y, Is.LessThan(0f), "the muzzle points down when put away");

            var pitch = Quaternion.identity;
            var bodyAway = PlayerMockPose.BodyGun(Lowering(1f), pitch);
            Assert.That(bodyAway.Position, Is.EqualTo(PlayerMockPose.BodyLoweredGunPosition));
            Assert.That(bodyAway.Position.y, Is.LessThan(PlayerMockPose.BodyGun(State(PlayerActionConditions.Grounded), pitch).Position.y));
        }

        private static PlayerActionState Lowering(float lowering)
        {
            return new PlayerActionState(PlayerActionConditions.Grounded, 0f, null, ShoulderSide.Right, ViewMode.FirstPerson, "Rifle", 0f, lowering);
        }

        [Test]
        public void KickPullsBackAndTipsUpThenRecovers()
        {
            var pose = new MockPartPose(new Vector3(0.2f, -0.2f, 0.4f), Quaternion.identity);

            var kicked = PlayerMockPose.WithKick(pose, 1f, 0.1f, 10f);
            Assert.That(kicked.Position.z, Is.EqualTo(0.3f).Within(Tolerance));
            Assert.That((kicked.Rotation * Vector3.forward).y, Is.GreaterThan(0f), "muzzle goes up");
            Assert.That(PlayerMockPose.WithKick(pose, 0f, 0.1f, 10f), Is.EqualTo(pose));

            Assert.That(PlayerMockPose.RecoverKick(1f, PlayerMockPose.KickRecoverSeconds / 2f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(PlayerMockPose.RecoverKick(1f, 1f), Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.WithKick(pose, 1.5f, 0.1f, 10f));
        }

        [Test]
        public void BlendFactorIsZeroForNoTimeAndApproachesOne()
        {
            Assert.That(PlayerMockPose.BlendFactor(0f), Is.EqualTo(0f));
            Assert.That(PlayerMockPose.BlendFactor(10f), Is.EqualTo(1f).Within(Tolerance));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.BlendFactor(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMockPose.BlendFactor(float.PositiveInfinity));
        }
    }
}
