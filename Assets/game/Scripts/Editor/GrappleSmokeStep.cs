using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // batchmode には入力が無いので、Ability1 / Ability2 の代わりに AbilityHost.TryUse を、照準の代わりに FirstPersonLook を回し、
    // Jump の代わりに MoveFilter への差し込みを使う。射撃場のルールは Abilities を Off のままにしているので、
    // まず使えないことを確かめてから、この手順の間だけ On にする。壁は GrappleWallFixture。
    public sealed class GrappleSmokeStep : ITpsActionSmokeStep
    {
        // 置き直してから撃つまでに待つ Editor の更新の回数。移した位置で接地し直し、肩越しのカメラが追い付くのを待つ。
        public const int SettleUpdates = 3;

        // 照準を合わせ直す回数の上限。三人称のカメラは向きを変えると位置も動くので、1 回では狙った点に当たらないことがある。
        public const int MaxAimUpdates = 10;

        // 掴める壁で狙う、縁から下がった高さ (m)。GrappleSpec.DefaultEdgeSnapDistance より浅いので縁へ寄る。
        public const float AimBelowEdge = 0.6f;

        // 照準が当たったと見なす、狙った高さからのずれ (m)。
        public const float AimTolerance = 0.4f;

        // 掴めない壁で狙う高さ (m)。
        public const float NotGrabbableAimHeight = 2.5f;

        // 上がった後の足元の高さの、壁の上端からの許容のずれ (m)。CharacterController の skinWidth を見込む。
        public const float FeetTolerance = 0.15f;

        // ジャンプで離すまでに引き寄せられる距離 (m)。速さに乗った所で離す。
        public const float JumpAfterDistance = 3f;

        // 離れた後に勢いが残っていると見なす水平の速さ (m/s)。
        public const float MinMomentumSpeed = 2f;

        private static readonly string _outOfRangeLog = $"{GrappleLog.MissPrefix}reason={GrappleMiss.OutOfRange} ";
        private static readonly string _notGrabbableLog =
            $"{GrappleLog.MissPrefix}reason={GrappleMiss.NotGrabbable} surface={GrappleWallFixture.NotGrabbableWallName} ";
        private static readonly string _attachedLog = $"{GrappleLog.AttachedPrefix}surface={GrappleWallFixture.GrabbableWallName} ";
        private static readonly string _climbLog = GrappleLog.Climb(true);
        private static readonly string _jumpedLog = $"{GrappleLog.ReleasedPrefix}reason={GrappleEnd.Jumped} ";
        private static readonly string _mantleEndLog = PlayerController.FormatMantlingLog(false);

        private Phase _phase;
        private int _settled;
        private int _aimUpdates;
        private int _slot = -1;
        private bool _sawOutOfRange;
        private bool _sawNotGrabbable;
        private bool _sawAttached;
        private bool _attachedOnEdge;
        private bool _sawArrived;
        private bool _sawClimb;
        private bool _sawMantleEnd;
        private bool _sawJumped;
        private bool _sawAbilityMoving;
        private Vector3 _startPosition;
        private AbilityHost _host;
        private GameRules _rules;
        private PlayerController _mover;
        private JumpOnceFilter _jumpFilter;

        private enum Phase
        {
            Setup,
            PlaceForOutOfRange,
            ShootOutOfRange,
            PlaceForNotGrabbable,
            ShootNotGrabbable,
            PlaceForClimb,
            ShootForClimb,
            Climb,
            PlaceForJump,
            ShootForJump,
            PullUntilJump,
            Fly,
            Done,
        }

        public int Order => 100;

        public string Name => "grapple";

        // 2 回引き寄せ (1 回はよじ登りまで)、待ち時間 (AbilityAssetBuilder.GrappleCooldownSeconds) と置き直して接地を待つ分を見込む。
        public float TimeoutSeconds => 25f;

        public void Reset()
        {
            Cleanup();
            _phase = Phase.Setup;
            _settled = 0;
            _aimUpdates = 0;
            _slot = -1;
            _sawOutOfRange = false;
            _sawNotGrabbable = false;
            _sawAttached = false;
            _attachedOnEdge = false;
            _sawArrived = false;
            _sawClimb = false;
            _sawMantleEnd = false;
            _sawJumped = false;
            _sawAbilityMoving = false;
            _startPosition = Vector3.zero;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(_outOfRangeLog, StringComparison.Ordinal))
            {
                _sawOutOfRange = true;
            }
            else if (condition.StartsWith(_notGrabbableLog, StringComparison.Ordinal))
            {
                _sawNotGrabbable = true;
            }
            else if (condition.StartsWith(_attachedLog, StringComparison.Ordinal))
            {
                _sawAttached = true;
                _attachedOnEdge = condition.EndsWith(" edge=true", StringComparison.Ordinal);
            }
            else if (condition.StartsWith(GrappleLog.ArrivedPrefix, StringComparison.Ordinal))
            {
                _sawArrived = true;
            }
            else if (string.Equals(condition, _climbLog, StringComparison.Ordinal))
            {
                _sawClimb = true;
            }
            else if (string.Equals(condition, _mantleEndLog, StringComparison.Ordinal))
            {
                _sawMantleEnd = true;
            }
            else if (condition.StartsWith(_jumpedLog, StringComparison.Ordinal))
            {
                _sawJumped = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world);
                    return false;

                case Phase.PlaceForOutOfRange:
                    Place(world, GrappleWallFixture.StandPoint);
                    _phase = Phase.ShootOutOfRange;
                    return false;

                case Phase.ShootOutOfRange:
                    problem = ShootOutOfRange(world);
                    return false;

                case Phase.PlaceForNotGrabbable:
                    Place(world, GrappleWallFixture.NotGrabbableStandPoint);
                    _phase = Phase.ShootNotGrabbable;
                    return false;

                case Phase.ShootNotGrabbable:
                    problem = ShootNotGrabbable(world);
                    return false;

                case Phase.PlaceForClimb:
                case Phase.PlaceForJump:
                    Place(world, GrappleWallFixture.StandPoint);
                    _phase = _phase == Phase.PlaceForClimb ? Phase.ShootForClimb : Phase.ShootForJump;
                    return false;

                case Phase.ShootForClimb:
                case Phase.ShootForJump:
                    problem = ShootGrabbable(world);
                    return false;

                case Phase.Climb:
                    problem = Climb(world);
                    return false;

                case Phase.PullUntilJump:
                    problem = PullUntilJump(world);
                    return false;

                case Phase.Fly:
                    problem = Fly(world);
                    return _phase == Phase.Done;

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_mover != null && _jumpFilter != null && ReferenceEquals(_mover.MoveFilter, _jumpFilter))
            {
                _mover.MoveFilter = _jumpFilter.Inner;
            }

            _jumpFilter = null;
            if (_host != null && _rules != null)
            {
                _host.Configure(_rules);
            }

            _host = null;
            _rules = null;
            _mover = null;
        }

        private string Setup(BootstrapWorld world)
        {
            var host = world.Abilities;
            if (host == null)
            {
                return "the player has no AbilityHost (is AbilityActionInstaller registered?)";
            }

            _slot = FindGrappleSlot(host);
            if (_slot < 0)
            {
                return $"no ability slot holds a {nameof(GrappleAbilityDefinition)}; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (GameObject.Find(GrappleWallFixture.RootName) == null)
            {
                return $"the scene has no '{GrappleWallFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (world.Rules.Actions.Abilities != Abilities.On)
            {
                var charges = host.Slot(_slot).Charges;
                if (host.TryUse(_slot) || host.IsRunning(_slot) || host.Slot(_slot).Charges != charges)
                {
                    return "the grapple was used while the Abilities rule was Off";
                }
            }

            _host = host;
            _rules = world.Rules;
            _mover = world.Mover;
            host.Configure(world.Rules.WithActions(world.Rules.Actions.WithAbilities(Abilities.On)));
            if (!host.UsesAbilities)
            {
                return "configuring the Abilities rule On did not turn the ability slots on";
            }

            _phase = Phase.PlaceForOutOfRange;
            return null;
        }

        // 真上へ撃つ。射撃場の上には何も無いので、届く距離の中に面が無い。
        private string ShootOutOfRange(BootstrapWorld world)
        {
            if (!Settle(world))
            {
                return null;
            }

            Look(world, 90f, 80f);
            var charges = _host.Slot(_slot).Charges;
            if (_host.TryUse(_slot) || _host.IsRunning(_slot))
            {
                return "the grapple was used while aiming at the open sky";
            }

            if (_host.Slot(_slot).Charges != charges)
            {
                return "a grapple that hit nothing used a charge";
            }

            if (!_sawOutOfRange)
            {
                return $"aiming at the open sky logged no '{_outOfRangeLog}...' line";
            }

            _phase = Phase.PlaceForNotGrabbable;
            return null;
        }

        private string ShootNotGrabbable(BootstrapWorld world)
        {
            if (!Settle(world))
            {
                return null;
            }

            var wall = GrappleWallFixture.NotGrabbableWall;
            var target = new Vector3(GrappleWallFixture.FaceX, NotGrabbableAimHeight, wall.Center.z);
            var aimed = AimAt(world, target, GrappleWallFixture.NotGrabbableWallName, out var problem);
            if (!aimed)
            {
                return problem;
            }

            var charges = _host.Slot(_slot).Charges;
            if (_host.TryUse(_slot) || _host.IsRunning(_slot))
            {
                return $"the grapple stuck into '{GrappleWallFixture.NotGrabbableWallName}', which is not grabbable";
            }

            if (_host.Slot(_slot).Charges != charges)
            {
                return "a grapple that hit a surface it cannot grab used a charge";
            }

            if (!_sawNotGrabbable)
            {
                return $"aiming at '{GrappleWallFixture.NotGrabbableWallName}' logged no '{_notGrabbableLog}...' line";
            }

            _aimUpdates = 0;
            _phase = Phase.PlaceForClimb;
            return null;
        }

        private string ShootGrabbable(BootstrapWorld world)
        {
            if (!Settle(world) || !_host.Slot(_slot).CanStart)
            {
                return null;
            }

            var target = new Vector3(GrappleWallFixture.FaceX, GrappleWallFixture.WallHeight - AimBelowEdge, GrappleWallFixture.GrabbableWallZ);
            var aimed = AimAt(world, target, GrappleWallFixture.GrabbableWallName, out var problem);
            if (!aimed)
            {
                return problem;
            }

            var charges = _host.Slot(_slot).Charges;
            _sawAttached = false;
            _attachedOnEdge = false;
            _sawArrived = false;
            _sawAbilityMoving = false;
            if (!_host.TryUse(_slot))
            {
                return $"the grapple did not stick into '{GrappleWallFixture.GrabbableWallName}' ({_host.UseBlock(_slot)})";
            }

            if (_host.Slot(_slot).Charges != charges - 1)
            {
                return $"using the grapple left {_host.Slot(_slot).Charges} charges (expected {charges - 1})";
            }

            if (!_sawAttached || !_attachedOnEdge)
            {
                return $"the grapple did not log '{_attachedLog}... edge=true' (attached={_sawAttached}, edge={_attachedOnEdge})";
            }

            _startPosition = world.Player.transform.position;
            _phase = _phase == Phase.ShootForClimb ? Phase.Climb : Phase.PullUntilJump;
            return null;
        }

        private string Climb(BootstrapWorld world)
        {
            var mover = world.Mover;
            WatchConditions(mover);
            if (_host.IsRunning(_slot) || mover.IsMantling || !mover.IsGrounded)
            {
                return null;
            }

            if (!_sawArrived)
            {
                return "the grapple ended without arriving at the wall";
            }

            if (!_sawAbilityMoving)
            {
                return $"the movement never had {PlayerActionConditions.AbilityMoving} while pulled";
            }

            var mantle = world.Rules.Actions.Mantle == Mantle.On;
            if (mantle && (!_sawClimb || !_sawMantleEnd))
            {
                return $"arriving at the edge with mantle On did not log '{_climbLog}' and '{_mantleEndLog}' (climb={_sawClimb}, mantle end={_sawMantleEnd})";
            }

            if (mantle)
            {
                var feetY = world.Controller.bounds.min.y;
                var bodyX = world.Player.transform.position.x;
                if (Mathf.Abs(feetY - GrappleWallFixture.WallHeight) > FeetTolerance || bodyX - GameBootstrap.PlayerRadius < GrappleWallFixture.FaceX)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "after the grapple the feet are at y={0:0.##} and the body at x={1:0.##}, not standing on top of '{2}' (top {3:0.##}, face x {4:0.##})",
                        feetY,
                        bodyX,
                        GrappleWallFixture.GrabbableWallName,
                        GrappleWallFixture.WallHeight,
                        GrappleWallFixture.FaceX);
                }
            }

            _phase = Phase.PlaceForJump;
            return null;
        }

        private string PullUntilJump(BootstrapWorld world)
        {
            var mover = world.Mover;
            WatchConditions(mover);
            if (_jumpFilter == null)
            {
                if (!_host.IsRunning(_slot))
                {
                    return "the grapple ended before the player could jump off the rope";
                }

                if (Vector3.Distance(world.Player.transform.position, _startPosition) < JumpAfterDistance)
                {
                    return null;
                }

                _jumpFilter = new JumpOnceFilter(mover.MoveFilter);
                mover.MoveFilter = _jumpFilter;
                return null;
            }

            if (_host.IsRunning(_slot) && !_sawJumped)
            {
                return null;
            }

            if (!_sawJumped)
            {
                return $"jumping while pulled did not log '{_jumpedLog}...' (arrived={_sawArrived})";
            }

            if (ReferenceEquals(mover.MoveFilter, _jumpFilter))
            {
                mover.MoveFilter = _jumpFilter.Inner;
            }

            _jumpFilter = null;
            var velocity = mover.Motor.Velocity;
            var flat = new Vector2(velocity.X, velocity.Z).magnitude;
            if (flat < MinMomentumSpeed)
            {
                return $"after jumping off the rope the player moved at {flat.ToString("0.##", CultureInfo.InvariantCulture)} m/s, not keeping the pull's momentum";
            }

            _phase = Phase.Fly;
            return null;
        }

        private string Fly(BootstrapWorld world)
        {
            if (_host.IsRunning(_slot))
            {
                return "the grapple was still running after jumping off the rope";
            }

            if (!world.Mover.IsGrounded)
            {
                return null;
            }

            Cleanup();
            _phase = Phase.Done;
            return null;
        }

        private void WatchConditions(PlayerController mover)
        {
            if ((mover.MovementOverrideConditions & PlayerActionConditions.AbilityMoving) != 0)
            {
                _sawAbilityMoving = true;
            }
        }

        private bool Settle(BootstrapWorld world)
        {
            if (!world.Mover.IsGrounded || ++_settled < SettleUpdates)
            {
                return false;
            }

            return true;
        }

        // 照準の Ray (目から画面中央が指す点へ、ViewRig.ResolveAim) が target の高さに当たるまで向きを直す。
        // 当たったら true。合わない間は false で、MaxAimUpdates を超えたら problem に理由を入れる。
        private bool AimAt(BootstrapWorld world, Vector3 target, string surfaceName, out string problem)
        {
            problem = null;
            var camera = world.Camera.transform.position;
            var toTarget = target - camera;
            var flat = new Vector2(toTarget.x, toTarget.z).magnitude;
            Look(world, Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg, Mathf.Atan2(toTarget.y, flat) * Mathf.Rad2Deg);

            if (TryAimHit(world, out var hit)
                && hit.collider.name == surfaceName
                && Mathf.Abs(hit.point.y - target.y) <= AimTolerance)
            {
                _aimUpdates = 0;
                return true;
            }

            if (++_aimUpdates > MaxAimUpdates)
            {
                var seen = hit.collider != null
                    ? string.Format(CultureInfo.InvariantCulture, "'{0}' at y={1:0.##}", hit.collider.name, hit.point.y)
                    : "nothing";
                problem = $"could not aim at '{surfaceName}' at y={target.y.ToString("0.##", CultureInfo.InvariantCulture)} (the aim hits {seen})";
            }

            return false;
        }

        private static bool TryAimHit(BootstrapWorld world, out RaycastHit hit)
        {
            hit = default;
            Vector3 origin;
            Vector3 direction;
            if (world.Rig != null && world.Rig.IsInitialized)
            {
                world.Rig.ResolveAim(GrappleSpec.DefaultMaxRange, out origin, out direction);
            }
            else
            {
                origin = world.Player.transform.position;
                direction = world.Player.transform.forward;
            }

            var best = float.PositiveInfinity;
            var found = false;
            foreach (var candidate in Physics.RaycastAll(origin, direction, GrappleSpec.DefaultMaxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (candidate.collider.transform.IsChildOf(world.Player.transform) || candidate.distance >= best)
                {
                    continue;
                }

                best = candidate.distance;
                hit = candidate;
                found = true;
            }

            return found;
        }

        // FirstPersonLook は自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。pitch は上向きを正で渡す。
        private static void Look(BootstrapWorld world, float yaw, float pitchUp)
        {
            var look = world.Look;
            if (look == null || !look.IsInitialized)
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }

            look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), pitchUp + look.Pitch));
            Physics.SyncTransforms();
        }

        private void Place(BootstrapWorld world, Vector3 feet)
        {
            var controller = world.Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = feet + (Vector3.up * GameBootstrap.PlayerSpawn.y);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            world.Mover.Motor.ResetVelocity();
            Look(world, 90f, 0f);
            _settled = 0;
            _aimUpdates = 0;
        }

        private static int FindGrappleSlot(AbilityHost host)
        {
            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                if (host.Definition(i) is GrappleAbilityDefinition)
                {
                    return i;
                }
            }

            return -1;
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
