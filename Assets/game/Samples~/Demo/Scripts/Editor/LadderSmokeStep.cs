using System;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using Shooter.Runtime.Traversal;
using UnityEngine;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;
using LadderRule = Shooter.Core.Ladder;

namespace Shooter.Editor
{
    // 射撃場のルールはほかの手順と共有しているので変えず、この手順の間だけ LadderActionInstaller でプレイヤーのはしごの設定を切り替え、終わったら戻す。
    public sealed class LadderSmokeStep : ITpsActionSmokeStep
    {
        public const float LadderOffSeconds = 0.6f;

        // つかまれる距離 (LadderComponent.GrabDistance) に収まるよう、手前へ離す距離を抑える。
        public const float StandBack = 0.2f;

        public const float LandingTolerance = 0.25f;

        public const float OffRiseLimit = 0.3f;

        private int _frontGrabs;
        private int _topGrabs;
        private int _grabs;
        private bool _sawExitTop;
        private bool _sawExitBottom;
        private bool _checkedWhileClimbing;
        private Phase _phase;
        private float _phaseStartedAt;
        private float _offStartY;
        private GameObject _player;
        private PlayerController _mover;
        private GameRules _rules;
        private LadderInputFilter _filter;

        private enum Phase
        {
            Start,
            PushWhileOff,
            ClimbUp,
            RestOnTop,
            ClimbDown,
            Done,
        }

        public int Order => 100;

        public string Name => "ladder";

        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _frontGrabs = 0;
            _topGrabs = 0;
            _grabs = 0;
            _sawExitTop = false;
            _sawExitBottom = false;
            _checkedWhileClimbing = false;
            _phase = Phase.Start;
            _phaseStartedAt = 0f;
            _offStartY = 0f;
            _player = null;
            _mover = null;
            _rules = null;
            _filter = null;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(LadderLog.GrabPrefix, StringComparison.Ordinal))
            {
                _grabs++;
                if (condition.Contains(" entry=" + LadderEntry.Front + " "))
                {
                    _frontGrabs++;
                }
                else if (condition.Contains(" entry=" + LadderEntry.Top + " "))
                {
                    _topGrabs++;
                }
            }
            else if (condition.StartsWith(LadderLog.LeavePrefixFor(LadderTowerBuilder.LadderName, LadderOutcome.ExitTop), StringComparison.Ordinal))
            {
                _sawExitTop = true;
            }
            else if (condition.StartsWith(LadderLog.LeavePrefixFor(LadderTowerBuilder.LadderName, LadderOutcome.ExitBottom), StringComparison.Ordinal))
            {
                _sawExitBottom = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var ladder = FindLadder(out problem);
            if (ladder == null)
            {
                return false;
            }

            if (!ladder.TryGetShape(out var span, out _))
            {
                problem = $"'{LadderRangeFixture.LadderPath}' has an invalid shape";
                return false;
            }

            var mover = world.Mover;
            var feet = LadderMovement.FeetOf(mover);
            switch (_phase)
            {
                case Phase.Start:
                    _player = world.Player;
                    _mover = mover;
                    _rules = world.Rules;
                    new LadderActionInstaller().Install(world.Player, WithLadder(LadderRule.Off));
                    PlaceInFront(world, span);
                    _filter = new LadderInputFilter(mover.MoveFilter) { Move = Vector2.up };
                    mover.MoveFilter = _filter;
                    _offStartY = LadderMovement.FeetOf(mover).y;
                    _phaseStartedAt = Time.time;
                    _phase = Phase.PushWhileOff;
                    return false;

                case Phase.PushWhileOff:
                    {
                        var movement = world.Player.GetComponent<LadderMovement>();
                        if (_grabs > 0 || (movement != null && movement.IsClimbing))
                        {
                            problem = "the player grabbed the ladder while the Ladder rule was Off";
                            return false;
                        }

                        if (feet.y > _offStartY + OffRiseLimit)
                        {
                            problem = $"the player rose to {Format(feet.y)}m in front of the ladder while the Ladder rule was Off";
                            return false;
                        }

                        if (Time.time - _phaseStartedAt < LadderOffSeconds)
                        {
                            return false;
                        }

                        new LadderActionInstaller().Install(world.Player, WithLadder(LadderRule.On));
                        movement = world.Player.GetComponent<LadderMovement>();
                        if (movement == null || movement.Rule != LadderRule.On)
                        {
                            problem = "LadderActionInstaller did not give the player a LadderMovement with the rule On";
                            return false;
                        }

                        PlaceInFront(world, span);
                        _phase = Phase.ClimbUp;
                        return false;
                    }

                case Phase.ClimbUp:
                    {
                        var movement = world.Player.GetComponent<LadderMovement>();
                        if (movement != null && movement.IsClimbing && !_checkedWhileClimbing)
                        {
                            problem = CheckWhileClimbing(world);
                            _checkedWhileClimbing = true;
                            if (problem != null)
                            {
                                return false;
                            }
                        }

                        if (!_sawExitTop)
                        {
                            return false;
                        }

                        _filter.Move = Vector2.zero;
                        if (_frontGrabs == 0 || !_checkedWhileClimbing)
                        {
                            problem = $"the player reached the top without a '{LadderLog.GrabPrefix}... entry=Front' line or without being seen on the ladder";
                            return false;
                        }

                        _phase = Phase.RestOnTop;
                        return false;
                    }

                case Phase.RestOnTop:
                    {
                        var movement = world.Player.GetComponent<LadderMovement>();
                        if (!mover.IsGrounded || movement == null || movement.RegrabRemaining > 0f)
                        {
                            return false;
                        }

                        var top = span.Top.ToUnity();
                        if (Mathf.Abs(feet.y - top.y) > LandingTolerance || Vector3.Dot(feet - top, span.Facing.ToUnity()) <= 0f)
                        {
                            problem = $"after leaving at the top the player stood at {Format(feet)}, not on the platform at {Format(top)}";
                            return false;
                        }

                        // 上の足場では はしごと同じ向き (足場の奥) を向いているので、後ろへ下がると縁へ進む。
                        _filter.Move = Vector2.down;
                        _phase = Phase.ClimbDown;
                        return false;
                    }

                case Phase.ClimbDown:
                    {
                        if (!_sawExitBottom)
                        {
                            return false;
                        }

                        _filter.Move = Vector2.zero;
                        if (!mover.IsGrounded)
                        {
                            return false;
                        }

                        if (_topGrabs == 0)
                        {
                            problem = $"the player left at the bottom without a '{LadderLog.GrabPrefix}... entry=Top' line";
                            return false;
                        }

                        var bottom = span.Bottom.ToUnity();
                        if (Mathf.Abs(feet.y - bottom.y) > LandingTolerance || Vector3.Dot(feet - bottom, span.Facing.ToUnity()) >= 0f)
                        {
                            problem = $"after leaving at the bottom the player stood at {Format(feet)}, not on the ground in front of the ladder at {Format(bottom)}";
                            return false;
                        }

                        RestoreFilter();
                        _phase = Phase.Done;
                        return true;
                    }

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            RestoreFilter();
            if (_player != null && _rules != null)
            {
                new LadderActionInstaller().Install(_player, _rules);
            }
        }

        private static string CheckWhileClimbing(BootstrapWorld world)
        {
            var weapon = world.Weapon;
            if (weapon == null || !weapon.IsHolstered)
            {
                return "the weapon was not put away while on the ladder";
            }

            if (weapon.PullTrigger(true, true))
            {
                return "the weapon fired while on the ladder";
            }

            if ((world.Mover.MovementOverrideConditions & PlayerActionConditions.OnLadder) == 0)
            {
                return $"the player was climbing without {PlayerActionConditions.OnLadder} (got {world.Mover.MovementOverrideConditions})";
            }

            return null;
        }

        private GameRules WithLadder(LadderRule rule)
        {
            return _rules.WithActions(_rules.Actions.WithLadder(rule));
        }

        private void RestoreFilter()
        {
            if (_mover != null && _filter != null && ReferenceEquals(_mover.MoveFilter, _filter))
            {
                _mover.MoveFilter = _filter.Inner;
            }

            _filter = null;
        }

        private static LadderComponent FindLadder(out string problem)
        {
            var found = GameObject.Find(LadderRangeFixture.LadderPath);
            var ladder = found != null ? found.GetComponent<LadderComponent>() : null;
            problem = ladder == null || !ladder.isActiveAndEnabled
                ? $"the scene has no enabled ladder at '{LadderRangeFixture.LadderPath}'; rerun {TpsRangeSceneBuilder.MenuPath}"
                : null;
            return problem == null ? ladder : null;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void PlaceInFront(BootstrapWorld world, LadderSpan span)
        {
            var mover = world.Mover;
            var controller = world.Controller;
            var feet = span.FeetAt(0f, controller.radius).ToUnity() - (span.Facing.ToUnity() * StandBack) + (Vector3.up * LadderMovement.LandingLift);
            var root = feet + (mover.transform.position - LadderMovement.FeetOf(mover));
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = root;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            var yaw = Mathf.Atan2(span.Facing.X, span.Facing.Z) * Mathf.Rad2Deg;
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }

        // 先に差し込まれていたもの (カバー) を外さないよう、後から呼ぶ。
        private sealed class LadderInputFilter : IPlayerMoveFilter
        {
            public LadderInputFilter(IPlayerMoveFilter inner)
            {
                Inner = inner;
            }

            public IPlayerMoveFilter Inner { get; }

            public Vector2 Move { get; set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Move;
                sprint = false;
                crouch = false;
                jumpPressed = false;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
