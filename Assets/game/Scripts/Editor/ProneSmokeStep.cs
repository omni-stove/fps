using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) に足す伏せの手順。くぐり道 (<see cref="ProneCrawlwayFixture"/>) と組で使う。
    /// <list type="number">
    /// <item>くぐり道の入り口の外で伏せ、<c>[Player] stance=Prone hitboxHeight=..</c> が出て、当たり判定が伏せの高さまで下がり、
    /// <see cref="PlayerActionConditions.Prone"/> が立ち、照準の広がりの倍率と上下の範囲が伏せの値になる。</item>
    /// <item>伏せたまま天井の下へ移って Prone を押しても、立つこともしゃがむこともできず伏せたまま。</item>
    /// <item>天井の外へ戻すと立ち上がり (<c>[Player] stance=Standing</c>)、当たり判定が立ちの高さに戻る。</item>
    /// <item>ルールの伏せを Off にすると、Prone を押しても伏せない (確かめたらルールの値に戻す)。</item>
    /// <item>開けた場所でダッシュしながら Prone を押すと飛び込み (<c>[Player] diving=true</c> → <c>diving=false</c>)、前へ滑って伏せる。</item>
    /// </list>
    /// batchmode には入力が無いので、Prone とダッシュの押下は <see cref="PlayerController.UpdateProne"/>・<see cref="PlayerController.UpdateSprint"/> を直接呼ぶ。
    /// </summary>
    public sealed class ProneSmokeStep : ITpsActionSmokeStep
    {
        /// <summary>天井の下で伏せたままでいることを確かめるフレーム数。起き上がりを毎フレーム試すので、数フレーム続けば足りる。</summary>
        public const int FramesUnderCeiling = 10;

        /// <summary>ルールを Off にして Prone を押した後、伏せないことを確かめるフレーム数。</summary>
        public const int FramesWithRuleOff = 5;

        /// <summary>当たり判定の高さを比べるときの許容差 (m)。</summary>
        public const float HeightTolerance = 0.01f;

        /// <summary>飛び込みで少なくとも動くべき水平の距離の、<see cref="ProneSpec.DiveDistance"/> に対する割合。床との摩擦や最後のフレームの端数を見込む。</summary>
        public const float MinDiveDistanceRatio = 0.5f;

        private static readonly string _proneStanceLog = $"{PlayerController.LogTag} stance={PlayerStance.Prone} ";
        private static readonly string _standingStanceLog = $"{PlayerController.LogTag} stance={PlayerStance.Standing} ";
        private static readonly string _diveStartLog = PlayerController.FormatDivingLog(true);
        private static readonly string _diveEndLog = PlayerController.FormatDivingLog(false);

        private Phase _phase;
        private int _frames;
        private float _standingHeight;
        private bool _sawProneStance;
        private bool _sawStandingStance;
        private bool _sawDiveStart;
        private bool _sawDiveEnd;
        private Vector3 _diveFrom;
        private PlayerController _ruleChanged;
        private Prone _ruleToRestore;

        private enum Phase
        {
            PlaceAtEntrance,
            LieDown,
            WaitProne,
            MoveUnderCeiling,
            StayUnderCeiling,
            WaitStand,
            RuleOff,
            WaitRuleOff,
            PlaceInOpenArea,
            Dive,
            WaitDiveEnd,
            StandAfterDive,
            Done,
        }

        public int Order => 0;

        public string Name => "prone";

        public float TimeoutSeconds => 20f;

        public void Reset()
        {
            _phase = Phase.PlaceAtEntrance;
            _frames = 0;
            _standingHeight = 0f;
            _sawProneStance = false;
            _sawStandingStance = false;
            _sawDiveStart = false;
            _sawDiveEnd = false;
            _diveFrom = Vector3.zero;
            _ruleChanged = null;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(_proneStanceLog, StringComparison.Ordinal))
            {
                _sawProneStance = true;
            }
            else if (condition.StartsWith(_standingStanceLog, StringComparison.Ordinal))
            {
                _sawStandingStance = true;
            }
            else if (string.Equals(condition, _diveStartLog, StringComparison.Ordinal))
            {
                _sawDiveStart = true;
            }
            else if (string.Equals(condition, _diveEndLog, StringComparison.Ordinal))
            {
                _sawDiveEnd = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var mover = world.Mover;
            if (mover == null || !mover.IsInitialized)
            {
                problem = "the player's PlayerController is not initialized";
                return false;
            }

            switch (_phase)
            {
                case Phase.PlaceAtEntrance:
                    problem = PlaceAtEntrance(world, mover);
                    return false;

                case Phase.LieDown:
                    problem = LieDown(mover);
                    return false;

                case Phase.WaitProne:
                    problem = WaitProne(world, mover);
                    return false;

                case Phase.MoveUnderCeiling:
                    problem = MoveUnderCeiling(world, mover);
                    return false;

                case Phase.StayUnderCeiling:
                    problem = StayUnderCeiling(world, mover);
                    return false;

                case Phase.WaitStand:
                    problem = WaitStand(mover);
                    return false;

                case Phase.RuleOff:
                    problem = TurnRuleOffAndPress(mover);
                    return false;

                case Phase.WaitRuleOff:
                    problem = WaitRuleOff(world, mover);
                    return false;

                case Phase.PlaceInOpenArea:
                    problem = PlaceInOpenArea(world);
                    return false;

                case Phase.Dive:
                    problem = Dive(world, mover);
                    return false;

                case Phase.WaitDiveEnd:
                    problem = WaitDiveEnd(world, mover);
                    return false;

                case Phase.StandAfterDive:
                    return StandAfterDive(mover, out problem);

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            RestoreRule();
        }

        private string PlaceAtEntrance(BootstrapWorld world, PlayerController mover)
        {
            if (world.Rules.Actions.Prone != Prone.On || mover.ProneRule != Prone.On)
            {
                return $"the TPS range rules must turn prone on; rerun {GameRulesAssetBuilder.MenuPath} (rules prone={world.Rules.Actions.Prone}, player prone={mover.ProneRule})";
            }

            if (GameObject.Find(ProneCrawlwayFixture.FixtureName + "/" + ProneCrawlwayFixture.CeilingName) == null)
            {
                return $"the scene has no '{ProneCrawlwayFixture.FixtureName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (mover.CurrentStance != PlayerStance.Standing)
            {
                return $"the player must be standing before lying down (stance {mover.CurrentStance})";
            }

            Teleport(world.Controller, ProneCrawlwayFixture.Entrance + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            FaceForward(world);
            _standingHeight = mover.HitboxHeight;
            _phase = Phase.LieDown;
            return null;
        }

        private string LieDown(PlayerController mover)
        {
            // 移した直後は CharacterController の接地が前の位置のままなので、次の移動で床に着くまで待つ。
            if (!mover.IsGrounded)
            {
                return null;
            }

            if (!mover.UpdateProne(true, false, false, false, Vector2.zero))
            {
                return $"pressing Prone on the floor did not lie down (rule {mover.ProneRule}, dodging={mover.IsDodging}, vaulting={mover.IsVaulting})";
            }

            if (mover.IsDiving)
            {
                return "lying down while standing still started a dive";
            }

            _phase = Phase.WaitProne;
            return null;
        }

        private string WaitProne(BootstrapWorld world, PlayerController mover)
        {
            if (!mover.IsProne)
            {
                return null;
            }

            if (!_sawProneStance)
            {
                return $"the player lay down but no '{_proneStanceLog}...' line was logged";
            }

            var expected = mover.Stance.Prone.Height * Mathf.Abs(mover.transform.lossyScale.y);
            if (Mathf.Abs(mover.HitboxHeight - expected) > HeightTolerance || mover.HitboxHeight >= _standingHeight - HeightTolerance)
            {
                return $"the hitbox is {Format(mover.HitboxHeight)}m high while prone (expected {Format(expected)}m, standing {Format(_standingHeight)}m)";
            }

            var reader = world.Player.GetComponent<PlayerActionReader>();
            if (reader == null || !reader.isActiveAndEnabled)
            {
                return "the player has no enabled PlayerActionReader";
            }

            // Reader は LateUpdate で組み立てるので、伏せたフレームの次まで待つ。
            if (!reader.Current.IsProne)
            {
                return null;
            }

            if (reader.Current.IsCrouching)
            {
                return $"the action state is crouching while prone ({reader.Current})";
            }

            var weapon = world.Weapon;
            if (weapon != null && !Mathf.Approximately(weapon.SpreadScale, mover.ProneSpec.SpreadScale))
            {
                return $"the weapon spread scale is {Format(weapon.SpreadScale)} while prone (expected {Format(mover.ProneSpec.SpreadScale)})";
            }

            var look = world.Look;
            if (look != null && look.PitchLimit > mover.ProneSpec.MaxPitchDegrees + 0.001f)
            {
                return $"the view can look {Format(look.PitchLimit)} degrees up or down while prone (expected at most {Format(mover.ProneSpec.MaxPitchDegrees)})";
            }

            _phase = Phase.MoveUnderCeiling;
            return null;
        }

        private string MoveUnderCeiling(BootstrapWorld world, PlayerController mover)
        {
            Teleport(world.Controller, ProneCrawlwayFixture.Center + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            if (mover.HasHeadroomToStand() || mover.HasHeadroomFor(PlayerStance.Crouching))
            {
                return $"the ceiling of '{ProneCrawlwayFixture.FixtureName}' leaves room to stand or crouch; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (mover.UpdateProne(true, false, false, false, Vector2.zero))
            {
                return "pressing Prone again did not ask to get up";
            }

            _sawStandingStance = false;
            _frames = 0;
            _phase = Phase.StayUnderCeiling;
            return null;
        }

        private string StayUnderCeiling(BootstrapWorld world, PlayerController mover)
        {
            if (!mover.IsProne)
            {
                return $"the player got up to {mover.CurrentStance} under the ceiling";
            }

            if (++_frames < FramesUnderCeiling)
            {
                return null;
            }

            Teleport(world.Controller, ProneCrawlwayFixture.Entrance + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
            _phase = Phase.WaitStand;
            return null;
        }

        private string WaitStand(PlayerController mover)
        {
            if (mover.CurrentStance != PlayerStance.Standing)
            {
                return null;
            }

            if (!_sawStandingStance)
            {
                return $"the player stood up but no '{_standingStanceLog}...' line was logged";
            }

            if (Mathf.Abs(mover.HitboxHeight - _standingHeight) > HeightTolerance)
            {
                return $"the hitbox is {Format(mover.HitboxHeight)}m high after standing up (expected {Format(_standingHeight)}m)";
            }

            _phase = Phase.RuleOff;
            return null;
        }

        private string TurnRuleOffAndPress(PlayerController mover)
        {
            if (!mover.IsGrounded)
            {
                return null;
            }

            _ruleChanged = mover;
            _ruleToRestore = mover.ProneRule;
            mover.ProneRule = Prone.Off;
            if (mover.UpdateProne(true, false, false, false, Vector2.zero))
            {
                return "pressing Prone with the rule off asked to lie down";
            }

            _frames = 0;
            _phase = Phase.WaitRuleOff;
            return null;
        }

        private string WaitRuleOff(BootstrapWorld world, PlayerController mover)
        {
            if (mover.IsProne || mover.IsProneActive)
            {
                return "the player lay down with the prone rule off";
            }

            if (++_frames < FramesWithRuleOff)
            {
                return null;
            }

            RestoreRule();
            if (mover.ProneRule != world.Rules.Actions.Prone)
            {
                return $"the prone rule was not restored (got {mover.ProneRule})";
            }

            _phase = Phase.PlaceInOpenArea;
            return null;
        }

        private string PlaceInOpenArea(BootstrapWorld world)
        {
            var area = GameObject.Find(TpsRangeSceneBuilder.OpenAreaName);
            if (area == null)
            {
                return $"the scene has no '{TpsRangeSceneBuilder.OpenAreaName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var center = area.transform.position;
            Teleport(world.Controller, new Vector3(center.x, center.y + GameBootstrap.PlayerSpawn.y, center.z));
            FaceForward(world);
            _phase = Phase.Dive;
            return null;
        }

        private string Dive(BootstrapWorld world, PlayerController mover)
        {
            if (!mover.IsGrounded)
            {
                return null;
            }

            _diveFrom = world.Player.transform.position;
            if (!mover.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse))
            {
                return "the player could not start sprinting in the open area";
            }

            if (!mover.UpdateProne(true, false, false, true, Vector2.up))
            {
                return "pressing Prone while sprinting did not lie down";
            }

            if (!mover.IsDiving || !_sawDiveStart)
            {
                return $"pressing Prone while sprinting did not start a dive (diving={mover.IsDiving}, log={_sawDiveStart})";
            }

            if (mover.IsSprinting)
            {
                return "the player kept sprinting after lying down";
            }

            _phase = Phase.WaitDiveEnd;
            return null;
        }

        private string WaitDiveEnd(BootstrapWorld world, PlayerController mover)
        {
            if (mover.IsDiving)
            {
                return null;
            }

            if (!_sawDiveEnd)
            {
                return $"the dive ended but no '{_diveEndLog}' line was logged";
            }

            if (!mover.IsProne)
            {
                return $"the player is {mover.CurrentStance} after the dive, not prone";
            }

            var moved = world.Player.transform.position - _diveFrom;
            moved.y = 0f;
            var minimum = mover.ProneSpec.DiveDistance * MinDiveDistanceRatio;
            if (moved.magnitude < minimum || Vector3.Dot(moved, Vector3.forward) <= 0f)
            {
                return $"the dive moved the player by {Format(moved)} (expected at least {Format(minimum)}m forward)";
            }

            mover.UpdateProne(true, false, false, false, Vector2.zero);
            _phase = Phase.StandAfterDive;
            return null;
        }

        private bool StandAfterDive(PlayerController mover, out string problem)
        {
            problem = null;
            if (mover.CurrentStance != PlayerStance.Standing)
            {
                return false;
            }

            _phase = Phase.Done;
            return true;
        }

        private void RestoreRule()
        {
            if (_ruleChanged != null)
            {
                _ruleChanged.ProneRule = _ruleToRestore;
            }

            _ruleChanged = null;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;

            // 頭上の検査は同じフレームに行うので、移した位置を物理に反映させておく。
            Physics.SyncTransforms();
        }

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
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
    }
}
