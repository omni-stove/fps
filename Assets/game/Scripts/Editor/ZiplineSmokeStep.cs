using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using UnityEngine;
using ZiplineLine = Fps.Runtime.Traversal.Zipline;
using ZiplineRule = Fps.Core.Zipline;

namespace Fps.Editor
{
    // batchmode には入力が無いので、Interact の代わりに ZiplineMovement.TryGrab を、Jump の代わりに MoveFilter への差し込みを使う。
    // 射撃場のルールは Zipline を Off のままにしているので、まず掴まれないことを確かめてから、この手順の間だけ On にする。
    public sealed class ZiplineSmokeStep : ITpsActionSmokeStep
    {
        // 掴まってから撃つまでに滑る距離 (m)。掴まった直後の 1 フレームだけでなく、滑っている最中に撃てることを見る。
        public const float FireAfterDistance = 3f;

        // ジャンプで離れるまでに滑る距離 (m)。線の真ん中あたりの、床から十分高い所で離す。
        public const float JumpAfterDistance = 12f;

        // 離れてから落ちたと見なす、離れた所からの下がった高さ (m)。
        public const float MinFallHeight = 1f;

        // 端に着いたと見なす、線の終点からの水平距離の上限 (m)。止まる所 (EndMargin) と最後のフレームの端数を見込む。
        public const float ArrivalTolerance = 1.5f;

        private static readonly string _grabLog = ZiplineLine.LogTag + " grab line=" + ZiplineRangeFixture.FixtureName + " ";
        private static readonly string _endLog = ZiplineLine.LogTag + " release reason=" + ZiplineMovement.ReasonEnd + " ";
        private static readonly string _jumpLog = ZiplineLine.LogTag + " release reason=" + ZiplineMovement.ReasonJump + " ";

        private Phase _phase;
        private bool _sawGrab;
        private bool _sawEnd;
        private bool _sawJump;
        private bool _sawZiplining;
        private bool _fired;
        private float _releaseY;
        private ZiplineMovement _movement;
        private GameRules _rules;
        private JumpOnceFilter _jumpFilter;

        private enum Phase
        {
            RefuseWhileOff,
            PlaceForRide,
            GrabForRide,
            Ride,
            PlaceForJump,
            GrabForJump,
            RideUntilJump,
            Fall,
            Done,
        }

        public int Order => 100;

        public string Name => "zipline";

        // 2 回滑る (1 回は端まで 5 秒ほど) のと、置き直して接地を待つ分を見込む。
        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _phase = Phase.RefuseWhileOff;
            _sawGrab = false;
            _sawEnd = false;
            _sawJump = false;
            _sawZiplining = false;
            _fired = false;
            _releaseY = 0f;
            _movement = null;
            _rules = null;
            _jumpFilter = null;
        }

        public void OnLog(string condition)
        {
            if (condition.StartsWith(_grabLog, StringComparison.Ordinal))
            {
                _sawGrab = true;
            }
            else if (condition.StartsWith(_endLog, StringComparison.Ordinal))
            {
                _sawEnd = true;
            }
            else if (condition.StartsWith(_jumpLog, StringComparison.Ordinal))
            {
                _sawJump = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var movement = world.Player.GetComponent<ZiplineMovement>();
            if (movement == null || !movement.IsInitialized)
            {
                problem = "the player has no initialized ZiplineMovement (is ZiplineActionInstaller registered?)";
                return false;
            }

            var lineObject = GameObject.Find(ZiplineRangeFixture.FixtureName);
            var line = lineObject != null ? lineObject.GetComponent<ZiplineLine>() : null;
            if (line == null)
            {
                problem = $"the scene has no '{ZiplineRangeFixture.FixtureName}' with a Zipline; rerun {TpsRangeSceneBuilder.MenuPath}";
                return false;
            }

            _movement = movement;
            _rules = world.Rules;
            switch (_phase)
            {
                case Phase.RefuseWhileOff:
                    problem = RefuseWhileOff(world, movement, line);
                    return false;

                case Phase.PlaceForRide:
                case Phase.PlaceForJump:
                    PlaceOnPlatform(world);
                    _phase = _phase == Phase.PlaceForRide ? Phase.GrabForRide : Phase.GrabForJump;
                    return false;

                case Phase.GrabForRide:
                case Phase.GrabForJump:
                    problem = Grab(world, movement);
                    return false;

                case Phase.Ride:
                    problem = Ride(world, movement);
                    return false;

                case Phase.RideUntilJump:
                    problem = RideUntilJump(world, movement);
                    return false;

                case Phase.Fall:
                    problem = Fall(world);
                    return _phase == Phase.Done;

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_movement == null)
            {
                return;
            }

            var mover = _movement.Mover;
            if (mover != null && _jumpFilter != null && ReferenceEquals(mover.MoveFilter, _jumpFilter))
            {
                mover.MoveFilter = _jumpFilter.Inner;
            }

            _jumpFilter = null;
            _movement.Release(ZiplineMovement.ReasonDisabled);
            if (_rules != null)
            {
                _movement.Configure(_rules);
            }

            _movement = null;
        }

        private string RefuseWhileOff(BootstrapWorld world, ZiplineMovement movement, ZiplineLine line)
        {
            if (world.Rules.Actions.Zipline == ZiplineRule.Off)
            {
                PlaceOnPlatform(world);
                if (movement.TryGrab(line) || movement.IsRiding || _sawGrab)
                {
                    return "the player grabbed the zipline while the rule was Off";
                }
            }

            movement.Configure(world.Rules.WithActions(world.Rules.Actions.WithZipline(ZiplineRule.On)));
            if (movement.Rule != ZiplineRule.On || !movement.enabled)
            {
                return "configuring the zipline rule On did not enable ZiplineMovement";
            }

            _phase = Phase.PlaceForRide;
            return null;
        }

        private string Grab(BootstrapWorld world, ZiplineMovement movement)
        {
            // 移した直後は接地が前の位置のままで、離れた直後は掴み直すまでの待ちがあるので、どちらも済むまで待つ。
            if (!world.Mover.IsGrounded || movement.RegrabRemaining > 0f)
            {
                return null;
            }

            _sawGrab = false;
            if (!movement.TryGrab())
            {
                return $"the player could not grab the zipline from the platform (hand {Format(movement.HandPosition)}, radius {movement.GrabRadius.ToString("0.##", CultureInfo.InvariantCulture)}m)";
            }

            if (!_sawGrab)
            {
                return $"grabbing the zipline logged no '{_grabLog}' line";
            }

            if (world.Ads != null && world.Ads.IsAiming)
            {
                return "the player kept aiming down sights after grabbing the zipline";
            }

            _phase = _phase == Phase.GrabForRide ? Phase.Ride : Phase.RideUntilJump;
            return null;
        }

        private string Ride(BootstrapWorld world, ZiplineMovement movement)
        {
            var motion = movement.Motion;
            if (world.ActionReader != null && world.ActionReader.Current.IsZiplining)
            {
                _sawZiplining = true;
            }

            if (movement.IsRiding)
            {
                if (!_fired && motion.Distance - motion.Spec.EndMargin >= FireAfterDistance)
                {
                    return TryFire(world);
                }

                return null;
            }

            if (!_sawEnd)
            {
                return $"the ride ended without a '{_endLog}' line";
            }

            if (!_fired)
            {
                return "the player reached the end of the zipline without firing on the way";
            }

            if (!_sawZiplining)
            {
                return "PlayerActionState never had Ziplining while riding";
            }

            var position = world.Player.transform.position;
            var end = ZiplineRangeFixture.EndAnchor;
            var flat = new Vector2(position.x - end.x, position.z - end.z).magnitude;
            if (flat > ArrivalTolerance)
            {
                return $"the ride ended at {Format(position)}, {flat.ToString("0.##", CultureInfo.InvariantCulture)}m from the end {Format(end)}";
            }

            _phase = Phase.PlaceForJump;
            return null;
        }

        private string TryFire(BootstrapWorld world)
        {
            var weapon = world.Weapon;
            if (weapon.Weapon == null || weapon.Weapon.IsReloading)
            {
                return null;
            }

            if (weapon.IsFireBlocked)
            {
                return "the weapon was blocked from firing while riding the zipline";
            }

            var spec = weapon.Weapon.Spec;
            var cone = weapon.CurrentConeDegrees(spec);
            var expected = SpreadPattern.ConeDegrees(spec, 0f, _movement.SpreadMoveFraction);
            if (cone + 1e-3f < expected)
            {
                return $"the spread while riding was {cone.ToString("0.##", CultureInfo.InvariantCulture)} degrees, narrower than hip fire on the move ({expected.ToString("0.##", CultureInfo.InvariantCulture)})";
            }

            if (weapon.PullTrigger(true, true))
            {
                _fired = true;
            }

            return null;
        }

        private string RideUntilJump(BootstrapWorld world, ZiplineMovement movement)
        {
            var mover = world.Mover;
            if (_jumpFilter == null)
            {
                if (!movement.IsRiding)
                {
                    return "the player left the zipline before jumping off";
                }

                var motion = movement.Motion;
                if (motion.Distance - motion.Spec.EndMargin < JumpAfterDistance)
                {
                    return null;
                }

                _jumpFilter = new JumpOnceFilter(mover.MoveFilter);
                mover.MoveFilter = _jumpFilter;
                return null;
            }

            if (movement.IsRiding)
            {
                return null;
            }

            if (!_sawJump)
            {
                return $"jumping on the zipline did not log '{_jumpLog}' (end={_sawEnd})";
            }

            if (ReferenceEquals(mover.MoveFilter, _jumpFilter))
            {
                mover.MoveFilter = _jumpFilter.Inner;
            }

            _jumpFilter = null;
            _releaseY = world.Player.transform.position.y;
            _phase = Phase.Fall;
            return null;
        }

        private string Fall(BootstrapWorld world)
        {
            if (_movement.IsRiding)
            {
                return "the player grabbed the zipline again while falling";
            }

            if (!world.Mover.IsGrounded)
            {
                return null;
            }

            var dropped = _releaseY - world.Player.transform.position.y;
            if (dropped < MinFallHeight)
            {
                return $"after jumping off, the player landed only {dropped.ToString("0.##", CultureInfo.InvariantCulture)}m lower";
            }

            _phase = Phase.Done;
            return null;
        }

        private static void PlaceOnPlatform(BootstrapWorld world)
        {
            var position = new Vector3(
                ZiplineRangeFixture.LineX,
                ZiplineRangeFixture.PlatformHeight + GameBootstrap.PlayerSpawn.y,
                ZiplineRangeFixture.PlatformZ);
            var controller = world.Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }

        // 先に差し込まれていたもの (カバー) は後から呼ぶ。カバーの外では何もしないので Jump は消えない。
        private sealed class JumpOnceFilter : IPlayerMoveFilter
        {
            private bool _pressed;

            public JumpOnceFilter(IPlayerMoveFilter inner)
            {
                Inner = inner;
            }

            public IPlayerMoveFilter Inner { get; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                jumpPressed = !_pressed;
                _pressed = true;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
