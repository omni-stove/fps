using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 足元の MovingPlatform に運ばれる。CharacterMotorSimulation.Ride と同じく、前のフレームを床に立って終えたときだけ運び、
    // 床を離れたフレームに床の速さを足す。
    public sealed partial class PlayerController
    {
        // 足元を調べる球を体の半径より細くする。skin の隙間で床に触れている球が、始めから床に重なって当たりを外さないため
        private const float _platformProbeInset = 0.05f;

        private const float _platformProbeReach = 0.1f;

        // KnockbackMovement の着地と同じ。上や横へ運ぶ Move は下に触れないので、少し下へ動かして接地を戻さないと、このフレームが空中の扱いになる
        private static readonly Vector3 _rideSettle = new Vector3(0f, -0.05f, 0f);

        private IRideablePlatform _platform;
        private int _platformStepSeen;
        private float _rideSeconds;

        public MovingPlatform Platform => RiddenPlatform as MovingPlatform;

        public IRideablePlatform RiddenPlatform => IsGone(_platform) ? null : _platform;

        public PlatformRide LastRide { get; private set; }

        // 運ぶのを移動と別の Move にするのは、上がる床で頭上に塞がれたことを移動の当たりと分けて知るため (CharacterMotorSimulation.Ride と同じ)。
        // 視点を回すので、移動の向きを読む前に呼ぶ。
        private void Ride(bool canRide)
        {
            LastRide = PlatformRide.None;
            _rideSeconds = 0f;
            var platform = _platform;
            if (IsGone(platform) || !canRide || !_controller.isGrounded || platform.StepCount == _platformStepSeen)
            {
                return;
            }

            _platformStepSeen = platform.StepCount;
            var motion = platform.LastMotion;
            if (motion.IsStill)
            {
                return;
            }

            if (motion.YawDelta != 0f)
            {
                TurnWithPlatform(motion.YawDelta * Mathf.Rad2Deg);
            }

            var intended = motion.Displacement(transform.position.ToNumerics());
            var collisions = _controller.Move(intended.ToUnity());
            LastRide = new PlatformRide(motion, intended, (collisions & CollisionFlags.Above) != 0);
            _rideSeconds = platform.LastStepSeconds;
            if (LastRide.IsBlocked)
            {
                platform.Obstruct();
                _platformStepSeen = platform.StepCount;
            }

            _controller.Move(_rideSettle);
        }

        // 床を離れたかは移動の Move の後でないと分からないので、運ぶのとは別に呼ぶ
        private void EndRide(bool footing, bool canRide)
        {
            if (LastRide.IsRiding && !footing && canRide && _rideSeconds > 0f)
            {
                _motor.SetVelocity(_motor.Velocity + LastRide.Velocity(_rideSeconds));
            }

            SetPlatform(footing && canRide ? FindPlatformUnderFeet() : null);
        }

        private void ResetPlatform()
        {
            SetPlatform(null);
            LastRide = PlatformRide.None;
            _rideSeconds = 0f;
        }

        private void SetPlatform(IRideablePlatform next)
        {
            if (IsGone(next))
            {
                next = null;
            }

            if (ReferenceEquals(next, _platform))
            {
                return;
            }

            if (!IsGone(_platform))
            {
                _platform.Leave(this);
            }

            _platform = next;
            if (next != null)
            {
                // 乗ったフレームの動きは、着いた位置にもう入っているので運ばない
                _platformStepSeen = next.StepCount;
                next.Board(this);
            }
        }

        // 口 (interface) で持つと == null が Unity の破棄を見ないので、消えた床をここで null と同じに扱う。
        private static bool IsGone(IRideablePlatform platform)
        {
            return platform == null || (platform is UnityEngine.Object unityObject && unityObject == null);
        }

        private IRideablePlatform FindPlatformUnderFeet()
        {
            var radius = _controller.radius;
            var probe = Mathf.Max(radius - _platformProbeInset, radius * 0.5f);
            var center = transform.TransformPoint(_controller.center);
            var half = Mathf.Max((_controller.height * 0.5f) - radius, 0f);
            var origin = center + (Vector3.down * half);
            var reach = (radius - probe) + _controller.skinWidth + _platformProbeReach;
            if (!Physics.SphereCast(origin, probe, Vector3.down, out var hit, reach, _headroomMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<IRideablePlatform>();
        }

        private void TurnWithPlatform(float degrees)
        {
            if (_look.IsInitialized)
            {
                _look.ApplyLookDelta(new Vector2(degrees, 0f));
            }
            else
            {
                _look.transform.Rotate(0f, degrees, 0f, Space.World);
            }
        }
    }
}
