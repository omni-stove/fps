using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
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
