using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS 射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) に足すスライディングの手順。坂 (<see cref="SlideSlopeFixture"/>) の場所で次を確かめる。
    /// <list type="number">
    /// <item>ルールの <see cref="ActionRules.Slide"/> が On で、プレイヤーの <see cref="SlideMovement"/> が有効。</item>
    /// <item>平らな走路 (<see cref="SlideSlopeFixture.FlatStart"/>) で -Z へダッシュしてから滑り (<c>[Slide] start</c>)、
    /// 滑っている間はしゃがみの姿勢で、<see cref="PlayerActionConditions.Sliding"/> が立ち、撃てて照準が止まっているときより広がる。
    /// 速さが落ちて止まり (<c>[Slide] end reason=Stopped</c>)、しゃがみを押していないので立つ。</item>
    /// <item>坂の上 (<see cref="SlideSlopeFixture.SlopeStart"/>) から同じように滑ると、平らな床の <see cref="MinSlopeRatio"/> 倍より長く滑る。</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// batchmode には入力が無いので、ダッシュは <see cref="PlayerController.MoveFilter"/> への差し込みで、しゃがみの押下は <see cref="SlideMovement.TryStart"/> で代える
    /// (ダッシュ中のしゃがみで始まることは EditMode のテストで確かめる)。
    /// </remarks>
    public sealed class SlideSmokeStep : ITpsActionSmokeStep
    {
        public const string LogTag = "[SlideSmoke]";

        /// <summary>坂で滑った距離が、平らな床で滑った距離の何倍を超えるべきか。</summary>
        public const float MinSlopeRatio = 1.5f;

        /// <summary>坂の面に置くとき、足元を面からこれだけ浮かせる (m)。置いた直後に面へめり込まないため。</summary>
        public const float DropHeight = 0.1f;

        private static readonly string _startLog = SlideMotion.LogTag + " start ";
        private static readonly string _endLog = SlideMotion.LogTag + " end reason=";

        private Phase _phase;
        private SprintFilter _filter;
        private PlayerController _filteredMover;
        private bool _sawStart;
        private string _endLine;
        private bool _crouchedWhileSliding;
        private bool _flaggedWhileSliding;
        private bool _firedWhileSliding;
        private float _slidingCone;
        private float _flatDistance;
        private float _slopeDistance;

        private enum Phase
        {
            CheckRule,
            PlaceFlat,
            RunFlat,
            SlideFlat,
            StandUp,
            PlaceSlope,
            RunSlope,
            SlideSlope,
            Done,
        }

        public int Order => 100;

        public string Name => "slide";

        public float TimeoutSeconds => 20f;

        public void Reset()
        {
            _phase = Phase.CheckRule;
            _filter = null;
            _filteredMover = null;
            ResetSlideObservations();
            _flatDistance = 0f;
            _slopeDistance = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition.StartsWith(_startLog, StringComparison.Ordinal))
            {
                _sawStart = true;
            }
            else if (condition.StartsWith(_endLog, StringComparison.Ordinal))
            {
                _endLine = condition;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var mover = world.Mover;
            var slide = world.Player.GetComponent<SlideMovement>();
            switch (_phase)
            {
                case Phase.CheckRule:
                    problem = CheckRule(world, slide);
                    return false;

                case Phase.PlaceFlat:
                    Place(world, SlideSlopeFixture.FlatStart);
                    _phase = Phase.RunFlat;
                    return false;

                case Phase.RunFlat:
                    problem = StartWhenRunning(mover, slide, Phase.SlideFlat);
                    return false;

                case Phase.SlideFlat:
                    problem = WatchSlide(world, slide, fire: true, out var flatDone);
                    if (problem == null && flatDone)
                    {
                        _flatDistance = slide.Motion.Distance;
                        problem = CheckFlatSlide(world, slide);
                        _phase = Phase.StandUp;
                    }

                    return false;

                case Phase.StandUp:
                    // しゃがみを押していないので、頭上の空いた走路では次の Tick で立つ。
                    if (!mover.IsCrouched)
                    {
                        _phase = Phase.PlaceSlope;
                    }

                    return false;

                case Phase.PlaceSlope:
                    problem = PlaceOnSlope(world, slide);
                    return false;

                case Phase.RunSlope:
                    problem = StartWhenRunning(mover, slide, Phase.SlideSlope);
                    return false;

                case Phase.SlideSlope:
                    problem = WatchSlide(world, slide, fire: false, out var slopeDone);
                    if (problem == null && slopeDone)
                    {
                        _slopeDistance = slide.Motion.Distance;
                        problem = CheckSlopeSlide();
                    }

                    return _phase == Phase.Done;

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_filteredMover != null && ReferenceEquals(_filteredMover.MoveFilter, _filter))
            {
                _filteredMover.MoveFilter = _filter.Inner;
            }

            _filter = null;
            _filteredMover = null;
        }

        private string CheckRule(BootstrapWorld world, SlideMovement slide)
        {
            if (world.Rules.Actions.Slide != Slide.On)
            {
                return $"the TPS range rules must turn slide on; rerun {GameRulesAssetBuilder.MenuPath} (got {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            if (slide == null || !slide.enabled || slide.Rule != Slide.On)
            {
                return $"the player has no enabled SlideMovement with the slide rule on (installers: {InstallerNames()})";
            }

            if (world.ActionReader == null)
            {
                return "the player has no PlayerActionReader";
            }

            if (GameObject.Find(SlideSlopeFixture.FixtureName + "/" + SlideSlopeFixture.RampName) == null)
            {
                return $"the scene has no '{SlideSlopeFixture.FixtureName}/{SlideSlopeFixture.RampName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _filteredMover = world.Mover;
            _filter = new SprintFilter(world.Mover.MoveFilter);
            world.Mover.MoveFilter = _filter;
            _phase = Phase.PlaceFlat;
            return null;
        }

        private void Place(BootstrapWorld world, Vector3 feet)
        {
            _filter.Sprinting = true;
            ResetSlideObservations();
            var controller = world.Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = feet + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            var look = world.Look;
            var yaw = Quaternion.LookRotation(SlideSlopeFixture.Downhill).eulerAngles.y;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private string PlaceOnSlope(BootstrapWorld world, SlideMovement slide)
        {
            if (slide.IsSliding)
            {
                return "the player was still sliding before the slope run";
            }

            Place(world, SlideSlopeFixture.SlopeStart + new Vector3(0f, DropHeight, 0f));
            _phase = Phase.RunSlope;
            return null;
        }

        // 移した直後は接地が前の位置のままで、ダッシュの速さにも乗っていないので、両方そろうまで待ってから滑らせる。
        private string StartWhenRunning(PlayerController mover, SlideMovement slide, Phase next)
        {
            var velocity = mover.Motor.Velocity;
            var speed = new Vector2(velocity.X, velocity.Z).magnitude;
            if (!mover.IsGrounded || !(speed > mover.WalkSpeed))
            {
                return null;
            }

            if (!slide.TryStart())
            {
                return $"the player could not start a slide while running at {speed.ToString("0.##", CultureInfo.InvariantCulture)}m/s (rule {slide.Rule}, grounded={mover.IsGrounded}, crouched={mover.IsCrouched})";
            }

            _phase = next;
            return null;
        }

        private string WatchSlide(BootstrapWorld world, SlideMovement slide, bool fire, out bool done)
        {
            done = false;
            if (slide.IsSliding)
            {
                // 滑り始めた Tick では向きを決めるだけで、ダッシュの差し込みはもう要らない (横取りの間は入力を移動に使わない)。
                _filter.Sprinting = false;
                _crouchedWhileSliding |= world.Mover.IsCrouched;
                _flaggedWhileSliding |= world.ActionReader.Current.IsSliding;
                if (fire && !_firedWhileSliding && world.Weapon.Weapon != null && world.Weapon.PullTrigger(true, true))
                {
                    _firedWhileSliding = true;
                    _slidingCone = world.Weapon.CurrentConeDegrees(world.Weapon.Weapon.Spec);
                }

                return null;
            }

            if (!_sawStart)
            {
                // TryStart の後、最初の Tick で滑り始めるまで待つ。
                return null;
            }

            if (_endLine == null)
            {
                return $"the slide ended without a '{_endLog}' line";
            }

            done = true;
            return null;
        }

        private string CheckFlatSlide(BootstrapWorld world, SlideMovement slide)
        {
            if (slide.Motion.LastEnd != SlideEnd.Stopped)
            {
                return $"the slide on the flat lane ended with {slide.Motion.LastEnd} instead of slowing down to a stop ({_endLine})";
            }

            if (!_crouchedWhileSliding)
            {
                return "the player did not take the crouched shape while sliding";
            }

            if (!_flaggedWhileSliding)
            {
                return "PlayerActionReader never reported Sliding while the player slid";
            }

            if (!_firedWhileSliding)
            {
                return "the weapon did not fire while sliding";
            }

            var spec = world.Weapon.Weapon.Spec;
            var resting = SpreadPattern.ConeDegrees(spec, 0f, 0f);
            if (SpreadPattern.ConeDegrees(spec, 0f, 1f) > resting && !(_slidingCone > resting))
            {
                return $"firing while sliding used a {_slidingCone.ToString("0.##", CultureInfo.InvariantCulture)}° cone, not wider than the {resting.ToString("0.##", CultureInfo.InvariantCulture)}° one when standing still";
            }

            return null;
        }

        private string CheckSlopeSlide()
        {
            if (!(_slopeDistance > _flatDistance * MinSlopeRatio))
            {
                return $"sliding down the slope went {Meters(_slopeDistance)}, not more than {MinSlopeRatio.ToString("0.##", CultureInfo.InvariantCulture)}x the {Meters(_flatDistance)} on the flat lane ({_endLine})";
            }

            Debug.Log($"{LogTag} flat={Meters(_flatDistance)} slope={Meters(_slopeDistance)}");
            _phase = Phase.Done;
            return null;
        }

        private void ResetSlideObservations()
        {
            _sawStart = false;
            _endLine = null;
            _crouchedWhileSliding = false;
            _flaggedWhileSliding = false;
            _firedWhileSliding = false;
            _slidingCone = 0f;
        }

        private static string Meters(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture) + "m";
        }

        private static string InstallerNames()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var installer in PlayerActions.Installers)
            {
                names.Add(installer.GetType().Name);
            }

            return string.Join(", ", names);
        }

        /// <summary>
        /// 入力の代わりに、<see cref="Sprinting"/> の間は前へダッシュし続け、そうでなければ何も押さない差し込み。しゃがみとジャンプは押さない。
        /// 先に差し込まれていたもの (カバー) は後から呼び、カバーの制限を上書きしない。
        /// </summary>
        private sealed class SprintFilter : IPlayerMoveFilter
        {
            public SprintFilter(IPlayerMoveFilter inner)
            {
                Inner = inner;
            }

            public IPlayerMoveFilter Inner { get; }

            public bool Sprinting { get; set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Sprinting ? Vector2.up : Vector2.zero;
                sprint = Sprinting;
                crouch = false;
                jumpPressed = false;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
