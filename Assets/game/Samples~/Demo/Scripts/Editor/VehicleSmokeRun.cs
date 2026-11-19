using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、Interact / Previous の代わりに PlayerVehicleRider.Interact / SwitchSeat を呼び、
    // 移動・ブレーキ・狙い・射撃は PlayerVehicleRider.SeatInputFilter に差し込む。固定銃座と飛行機までは歩かせずに移す。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class VehicleSmokeRun
    {
        public const string LogTag = "[VehicleSmokeRun]";

        public const float WarmUpSeconds = 1f;
        public const float EnterTimeoutSeconds = 120f;

        public const float DriveDistance = 10f;

        // 離陸の速さに近づいてから機首を上げる。止まったまま上げると、尾が地面を擦って加速が鈍るため。
        public const float RotateSpeedFraction = 0.8f;
        public const float ClimbPitchDegrees = 15f;
        public const float FlyAltitude = 15f;

        // 前輪は速いほど向けられなくなる (AircraftGround.SteerLimit) ので、歩くほどの速さで曲げる。
        // 15° で滑走路へ向け直すので、横へずれるのは 1m ほどで、離陸に使える長さもほとんど減らない。
        public const float TaxiSpeed = 4f;
        public const float TaxiAimDegrees = 40f;
        public const float TaxiTurnDegrees = 15f;

        // 真上の手前を狙わせるのは、真上だと機首が上を越えた途端に視線が背中側へ回り、宙返りを始めてしまうため。
        public const float PullPitchDegrees = 75f;
        public const float PullHoldSeconds = 4f;
        public const float PullSinkAllowance = 2f;

        public const float LevelToleranceDegrees = 10f;

        // 片翼を失った機体を当て方 (AircraftControlScheme) が水平へ戻そうとしても、揚力の差で釣り合う傾きは 9° ほど残るので、その手前で見る。
        public const float BrokenWingBankDegrees = 5f;
        public const float StoppedSpeed = 0.3f;
        public const float AimToleranceDegrees = 1.5f;

        // 熱で撃つ固定銃座は 10 発ほどで止まって冷めるのを待つので、耐久 1000 の車が燃え尽きるまでを見込む。
        public const float DestroyTimeoutSeconds = 120f;
        public const float StepTimeoutSeconds = 30f;

        // 的の胴の真ん中より少し上を狙う。銃座は屋根の上にあり、真ん中を狙うと下へ向ける角度が大きくなるため。
        public static readonly Vector3 TargetAimLocal = TargetDummy.BodyLocalPosition + new Vector3(0f, 0.4f, 0f);

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.VehicleSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.VehicleSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<string> _lines = new List<string>();
        private static readonly string[] _stepLogVerbs = { "drove", "taxied", "flew", "held", "banked" };
        private static readonly ScriptedSeatInput _input = new ScriptedSeatInput();
        private static double _playStartedAt = -1d;
        private static double _stepStartedAt;
        private static bool _finished;
        private static Step _step;
        private static Vector3 _driveStart;
        private static Vector3 _driveForward;
        private static Vector3 _runwayStart;
        private static Vector3 _runwayForward;
        private static TargetDummy _target;
        private static bool _sawTargetHit;
        private static PlayerVehicleRider _rider;
        private static float _pullFloor;
        private static float _maxAngleOfAttack;
        private static float _bankAtBreak;

        static VehicleSmokeRun()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public enum Step
        {
            Board,
            Drive,
            Stop,
            SwitchToGunner,
            AimAtTarget,
            ShootTarget,
            ExitCar,
            MoveToMountedGun,
            BoardMountedGun,
            AimAtCar,
            DestroyCar,
            ExitMountedGun,
            MoveToAircraft,
            BoardAircraft,
            Taxi,
            TakeOff,
            Climb,
            PullUp,
            Level,
            BankLeft,
            Done,
        }

        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                VehicleRangeSceneBuilder.Build();
                EditorSceneManager.OpenScene(VehicleRangeSceneBuilder.ScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _lines.Clear();
                _rider = null;
                _step = Step.Board;
                _sawTargetHit = false;
                _playStartedAt = -1d;
                _finished = false;
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{VehicleRangeSceneBuilder.ScenePath}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static IReadOnlyList<LogExpectation> ExpectedLogs()
        {
            var gunCar = Prefix(VehicleRangeSceneBuilder.GunCarName);
            var mountedGun = Prefix(VehicleRangeSceneBuilder.MountedGunName);
            var car = Prefix(VehicleRangeSceneBuilder.CarName);
            var aircraft = VehicleRangeSceneBuilder.AircraftName;
            return new[]
            {
                new LogExpectation(gunCar + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(DroveLogPrefix(VehicleRangeSceneBuilder.GunCarName), null),
                new LogExpectation(gunCar + "switch rider=", " to=2 role=" + VehicleSeatRole.Gunner),
                TargetHitLog(),
                new LogExpectation(gunCar + "leave rider=", " seat=2 reason=" + VehicleLeaveReason.Request + " "),
                new LogExpectation(mountedGun + "board rider=", " seat=0 role=" + VehicleSeatRole.Gunner),
                new LogExpectation(car + "exploded at=", null),
                new LogExpectation(car + "wrecked ", null),
                new LogExpectation(mountedGun + "leave rider=", " seat=0 reason=" + VehicleLeaveReason.Request + " "),
                new LogExpectation(Prefix(aircraft) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(TaxiedLogPrefix(aircraft), null),
                new LogExpectation(FlewLogPrefix(aircraft), null),
                new LogExpectation(HeldLogPrefix(aircraft), null),
                new LogExpectation(Prefix(aircraft) + "part broken part=", " effect=" + AircraftDamageEffect.LeftLift + " "),
                new LogExpectation(BankedLogPrefix(aircraft), null),
            };
        }

        public static string TaxiedLogPrefix(string vehicle)
        {
            return $"{LogTag} taxied '{vehicle}' ";
        }

        public static string TaxiedLog(string vehicle, float turnedDegrees, float speed)
        {
            return TaxiedLogPrefix(vehicle)
                + "turned=" + turnedDegrees.ToString("0.0", CultureInfo.InvariantCulture)
                + " speed=" + speed.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string HeldLogPrefix(string vehicle)
        {
            return $"{LogTag} held '{vehicle}' ";
        }

        public static string HeldLog(string vehicle, float seconds, float maxAngleOfAttackDegrees, float altitude)
        {
            return HeldLogPrefix(vehicle)
                + "seconds=" + seconds.ToString("0.0", CultureInfo.InvariantCulture)
                + " aoa=" + maxAngleOfAttackDegrees.ToString("0.0", CultureInfo.InvariantCulture)
                + " altitude=" + altitude.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string BankedLogPrefix(string vehicle)
        {
            return $"{LogTag} banked '{vehicle}' ";
        }

        public static string BankedLog(string vehicle, float leftBankDegrees)
        {
            return BankedLogPrefix(vehicle) + "left=" + leftBankDegrees.ToString("0.0", CultureInfo.InvariantCulture);
        }

        // 左へ傾く (右の翼が上がる) 向きを正にするのは、壊す翼が左で、壊した後の傾きの増え方をそのまま閾値と比べられるようにするため。
        public static float LeftBankDegrees(Quaternion rotation)
        {
            return Mathf.Asin(Mathf.Clamp((rotation * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static float PitchDegrees(Quaternion rotation)
        {
            return Mathf.Asin(Mathf.Clamp((rotation * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        // 左へ曲がる向きを正にするのは、滑走路の右 (東) に銃座と車があり、地上では左へ曲げるため。
        public static float LeftTurnDegrees(Vector3 from, Quaternion rotation)
        {
            return -Vector3.SignedAngle(from, Heading(rotation, from), Vector3.up);
        }

        // fallback を受けるのは、真上か真下を向くと機首の水平の向きが決まらないため。
        public static Vector3 Heading(Quaternion rotation, Vector3 fallback)
        {
            var flat = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            return flat.sqrMagnitude > 0.01f ? flat.normalized : fallback;
        }

        public static LogExpectation TargetHitLog()
        {
            return new LogExpectation(Prefix(VehicleRangeSceneBuilder.GunCarName) + "fire rider=", " hit=" + VehicleRangeSceneBuilder.TargetIdPrefix);
        }

        public static string DroveLogPrefix(string vehicle)
        {
            return $"{LogTag} drove '{vehicle}' ";
        }

        public static string FlewLogPrefix(string vehicle)
        {
            return $"{LogTag} flew '{vehicle}' ";
        }

        public static string FlewLog(string vehicle, float altitude, float speed)
        {
            return FlewLogPrefix(vehicle)
                + "altitude=" + altitude.ToString("0.0", CultureInfo.InvariantCulture)
                + " speed=" + speed.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string DroveLog(string vehicle, float distance)
        {
            return DroveLogPrefix(vehicle) + "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static int CountInOrder(IReadOnlyList<string> lines, IReadOnlyList<LogExpectation> expected)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            var matched = 0;
            foreach (var line in lines)
            {
                if (matched < expected.Count && expected[matched].Matches(line))
                {
                    matched++;
                }
            }

            return matched;
        }

        private static string Prefix(string vehicle)
        {
            return $"{VehicleLog.Tag} '{vehicle}' ";
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // WeaponSwitchSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(VehicleLog.Tag + " ", StringComparison.Ordinal) || IsStepLog(condition))
            {
                _lines.Add(condition);
            }

            if (TargetHitLog().Matches(condition))
            {
                _sawTargetHit = true;
            }
        }

        // 自分の行のうち手順の行だけを数えるのは、最後に出す要約の行 (step=...) が並びの確かめに混ざらないようにするため。
        public static bool IsStepLog(string line)
        {
            if (line == null)
            {
                return false;
            }

            foreach (var verb in _stepLogVerbs)
            {
                if (line.StartsWith(LogTag + " " + verb + " '", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                _playStartedAt = now;
                return;
            }

            if (now - _playStartedAt < WarmUpSeconds)
            {
                return;
            }

            string problem;
            try
            {
                problem = Advance(now);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name}: {e.Message}";
            }

            if (problem == null && _errors.Count == 0 && _step != Step.Done)
            {
                return;
            }

            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add($"{_step}: {problem}");
            }

            problems.AddRange(_errors);
            if (problems.Count == 0)
            {
                var expected = ExpectedLogs();
                var matched = CountInOrder(_lines, expected);
                if (matched < expected.Count)
                {
                    problems.Add($"the vehicle logs did not follow the expected order; missing '{expected[matched]}' after {matched} matched");
                }
            }

            Debug.Log($"{LogTag} step={_step} vehicle logs={_lines.Count.ToString(CultureInfo.InvariantCulture)} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static string Advance(double now)
        {
            if (_rider == null)
            {
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
                if (bootstrap == null || bootstrap.World == null)
                {
                    return "the scene has no GameBootstrap that built the player";
                }

                _rider = bootstrap.World.Player.GetComponent<PlayerVehicleRider>();
                if (_rider == null)
                {
                    return "the player has no PlayerVehicleRider";
                }

                _rider.SeatInputFilter = _input;
                _input.Clear();
                StartStep(Step.Board, now);
            }

            var limit = _step == Step.DestroyCar ? DestroyTimeoutSeconds : StepTimeoutSeconds;
            if (now - _stepStartedAt > limit)
            {
                return $"did not finish within {limit.ToString(CultureInfo.InvariantCulture)}s";
            }

            var gunCar = FindVehicle(CarFixtureBuilder.GunnerVehicleId);
            var car = FindVehicle(CarFixtureBuilder.VehicleId);
            var mountedGun = FindVehicle(VehicleRangeSceneBuilder.MountedGunName);
            var aircraft = FindVehicle(AircraftFixtureBuilder.VehicleId);
            if (gunCar == null || car == null || mountedGun == null || aircraft == null)
            {
                return $"the scene lacks a vehicle (gun car={gunCar != null}, car={car != null}, mounted gun={mountedGun != null}, aircraft={aircraft != null}); rerun {VehicleRangeSceneBuilder.MenuPath}";
            }

            switch (_step)
            {
                case Step.Board:
                    if (!_rider.Interact() || _rider.Vehicle != gunCar || _rider.Seat.Role != VehicleSeatRole.Driver)
                    {
                        return $"Interact did not seat the player as the driver of '{gunCar.name}'";
                    }

                    _driveStart = gunCar.transform.position;
                    _driveForward = gunCar.transform.forward;
                    _input.Move = new Vector2(0f, 1f);
                    StartStep(Step.Drive, now);
                    return null;

                case Step.Drive:
                    var distance = Vector3.Dot(gunCar.transform.position - _driveStart, _driveForward);
                    if (distance < DriveDistance)
                    {
                        return null;
                    }

                    Debug.Log(DroveLog(gunCar.name, distance));
                    _input.Move = Vector2.zero;
                    _input.Boost = true;
                    StartStep(Step.Stop, now);
                    return null;

                case Step.Stop:
                    var body = gunCar.GetComponent<Rigidbody>();
                    if (body != null && body.linearVelocity.magnitude > StoppedSpeed)
                    {
                        return null;
                    }

                    _input.Boost = false;
                    StartStep(Step.SwitchToGunner, now);
                    return null;

                case Step.SwitchToGunner:
                    // 運転席から前へ巡ると、助手席を飛ばして一番後ろの銃座に移る。
                    if (!_rider.SwitchSeat(-1) || _rider.Seat == null || _rider.Seat.Role != VehicleSeatRole.Gunner)
                    {
                        return "Previous did not move the player to the gunner seat";
                    }

                    _target = NearestTarget(gunCar.transform.position);
                    if (_target == null)
                    {
                        return $"the scene has no target whose id starts with '{VehicleRangeSceneBuilder.TargetIdPrefix}'";
                    }

                    _sawTargetHit = false;
                    StartStep(Step.AimAtTarget, now);
                    return null;

                case Step.AimAtTarget:
                    if (Aim(gunCar, _target.transform.TransformPoint(TargetAimLocal)))
                    {
                        _input.Fire = true;
                        StartStep(Step.ShootTarget, now);
                    }

                    return null;

                case Step.ShootTarget:
                    Aim(gunCar, _target.transform.TransformPoint(TargetAimLocal));
                    if (!_sawTargetHit)
                    {
                        return null;
                    }

                    _input.Fire = false;
                    StartStep(Step.ExitCar, now);
                    return null;

                case Step.ExitCar:
                    if (!_rider.Interact() || _rider.IsRiding)
                    {
                        return $"Interact did not get the player out of '{gunCar.name}'";
                    }

                    StartStep(Step.MoveToMountedGun, now);
                    return null;

                case Step.MoveToMountedGun:
                    var seat = mountedGun.Seats[0];
                    var feet = seat.EnterPoints.Count > 0 ? seat.EnterPoints[0].position : seat.transform.position;
                    Teleport(_rider.Mover.Controller, feet + GameBootstrap.PlayerSpawn);
                    StartStep(Step.BoardMountedGun, now);
                    return null;

                case Step.BoardMountedGun:
                    if (!_rider.Interact() || _rider.Vehicle != mountedGun)
                    {
                        return $"Interact did not seat the player on '{mountedGun.name}'";
                    }

                    StartStep(Step.AimAtCar, now);
                    return null;

                case Step.AimAtCar:
                    if (Aim(mountedGun, car.transform.TransformPoint(CarFixtureBuilder.BodyCenter)))
                    {
                        _input.Fire = true;
                        StartStep(Step.DestroyCar, now);
                    }

                    return null;

                case Step.DestroyCar:
                    Aim(mountedGun, car.transform.TransformPoint(CarFixtureBuilder.BodyCenter));
                    if (!car.HasExploded || !car.IsWrecked)
                    {
                        return null;
                    }

                    _input.Fire = false;
                    StartStep(Step.ExitMountedGun, now);
                    return null;

                case Step.ExitMountedGun:
                    if (!_rider.Interact() || _rider.IsRiding)
                    {
                        return $"Interact did not get the player off '{mountedGun.name}'";
                    }

                    StartStep(Step.MoveToAircraft, now);
                    return null;

                case Step.MoveToAircraft:
                    var pilotSeat = aircraft.Seats[0];
                    var pilotFeet = pilotSeat.EnterPoints.Count > 0 ? pilotSeat.EnterPoints[0].position : pilotSeat.transform.position;
                    Teleport(_rider.Mover.Controller, pilotFeet + GameBootstrap.PlayerSpawn);
                    StartStep(Step.BoardAircraft, now);
                    return null;

                case Step.BoardAircraft:
                    if (!_rider.Interact() || _rider.Vehicle != aircraft || _rider.Seat.Role != VehicleSeatRole.Driver)
                    {
                        return $"Interact did not seat the player as the pilot of '{aircraft.name}'";
                    }

                    _runwayStart = aircraft.transform.position;
                    _runwayForward = Vector3.ProjectOnPlane(aircraft.transform.forward, Vector3.up).normalized;
                    _input.Aim = Quaternion.AngleAxis(-TaxiAimDegrees, Vector3.up) * _runwayForward;
                    StartStep(Step.Taxi, now);
                    return null;

                default:
                    return AdvanceAircraft(aircraft, now);
            }
        }

        private static string AdvanceAircraft(Vehicle aircraft, double now)
        {
            var motor = aircraft.GetComponent<SimpleAircraftMotor>();
            if (motor == null)
            {
                return $"'{aircraft.name}' has no SimpleAircraftMotor";
            }

            var rotation = aircraft.transform.rotation;
            var altitude = aircraft.transform.position.y - _runwayStart.y;
            switch (_step)
            {
                case Step.Taxi:
                    // 全開にしないのは、速くなるほど前輪を向けられる角度が絞られ、曲がり切る前に離陸の速さへ近づくため。出過ぎたら閉じてブレーキを掛ける。
                    _input.Move = new Vector2(0f, Mathf.Clamp((TaxiSpeed - motor.ForwardSpeed) / TaxiSpeed, -1f, 1f));
                    var turned = LeftTurnDegrees(_runwayForward, rotation);
                    if (turned < TaxiTurnDegrees)
                    {
                        return null;
                    }

                    if (!motor.State.Grounded)
                    {
                        return $"'{aircraft.name}' left the ground while taxiing";
                    }

                    Debug.Log(TaxiedLog(aircraft.name, turned, motor.ForwardSpeed));
                    _input.Move = new Vector2(0f, 1f);
                    _input.Aim = _runwayForward;
                    StartStep(Step.TakeOff, now);
                    return null;

                case Step.TakeOff:
                    if (motor.ForwardSpeed < motor.Spec.Aero.LiftSpeed * RotateSpeedFraction)
                    {
                        return null;
                    }

                    _input.Aim = Quaternion.AngleAxis(-ClimbPitchDegrees, Vector3.Cross(Vector3.up, _runwayForward)) * _runwayForward;
                    StartStep(Step.Climb, now);
                    return null;

                case Step.Climb:
                    if (altitude < FlyAltitude)
                    {
                        return null;
                    }

                    var hudProblem = HudProblem(aircraft);
                    if (hudProblem != null)
                    {
                        return hudProblem;
                    }

                    Debug.Log(FlewLog(aircraft.name, altitude, motor.ForwardSpeed));
                    _input.Aim = Quaternion.AngleAxis(-PullPitchDegrees, Vector3.Cross(Vector3.up, _runwayForward)) * _runwayForward;
                    _pullFloor = altitude - PullSinkAllowance;
                    _maxAngleOfAttack = 0f;
                    StartStep(Step.PullUp, now);
                    return null;

                case Step.PullUp:
                    var state = motor.State;
                    var angleOfAttack = Mathf.Abs(state.AngleOfAttackRadians * Mathf.Rad2Deg);
                    _maxAngleOfAttack = Mathf.Max(_maxAngleOfAttack, angleOfAttack);
                    var criticalAngle = motor.Spec.Aero.CriticalAngle;
                    if (angleOfAttack > criticalAngle)
                    {
                        return $"the angle of attack reached {angleOfAttack.ToString("0.0", CultureInfo.InvariantCulture)}° past the critical "
                            + $"{criticalAngle.ToString("0.0", CultureInfo.InvariantCulture)}° while pulling up; the stall guard did not hold it";
                    }

                    if (state.Grounded || altitude < _pullFloor)
                    {
                        return $"'{aircraft.name}' sank to {altitude.ToString("0.0", CultureInfo.InvariantCulture)}m "
                            + $"(below {_pullFloor.ToString("0.0", CultureInfo.InvariantCulture)}m) while pulling up";
                    }

                    if (now - _stepStartedAt < PullHoldSeconds)
                    {
                        return null;
                    }

                    Debug.Log(HeldLog(aircraft.name, (float)(now - _stepStartedAt), _maxAngleOfAttack, altitude));
                    _input.Aim = _runwayForward;
                    StartStep(Step.Level, now);
                    return null;

                case Step.Level:
                    _input.Aim = _runwayForward;
                    if (Mathf.Abs(PitchDegrees(rotation)) > LevelToleranceDegrees || Mathf.Abs(LeftBankDegrees(rotation)) > LevelToleranceDegrees)
                    {
                        return null;
                    }

                    var breakProblem = BreakLeftWing(aircraft);
                    if (breakProblem != null)
                    {
                        return breakProblem;
                    }

                    _bankAtBreak = LeftBankDegrees(rotation);
                    StartStep(Step.BankLeft, now);
                    return null;

                case Step.BankLeft:
                    // 機首の向きを追って狙うのは、曲がり始めた機体を元の向きへ戻そうとする右への傾きで、片翼の分の傾きを打ち消さないようにするため。
                    _input.Aim = Heading(rotation, _runwayForward);
                    var gained = LeftBankDegrees(rotation) - _bankAtBreak;
                    if (gained < BrokenWingBankDegrees)
                    {
                        return null;
                    }

                    Debug.Log(BankedLog(aircraft.name, gained));
                    _input.Clear();
                    StartStep(Step.Done, now);
                    return null;

                default:
                    return null;
            }
        }

        // 木を作らない画面 (batchmode など) では表示が結ばれないので、そのときはシグナルまでを確かめる。
        private static string HudProblem(Vehicle aircraft)
        {
            if (!(GameSignals.Aircraft.Value is AircraftStatus status) || status.Grounded)
            {
                return $"GameSignals.Aircraft does not carry '{aircraft.name}' in flight; the HUD has no speed or altitude to show";
            }

            var hud = DefaultHudFactory.Current;
            var view = hud != null ? hud.GetComponent<AircraftHudView>() : null;
            if (view != null && view.Element != null && (!view.IsShown || view.Lines.Count == 0))
            {
                return "the default HUD does not show the aircraft readings";
            }

            return null;
        }

        // 命中の流れ (Hitbox → Vehicle.PartDamaged → AircraftPartDamage) を通すため、部位を直に削らず、左の翼の当たりへ耐久の分だけ当てる。
        private static string BreakLeftWing(Vehicle aircraft)
        {
            var damage = aircraft.GetComponent<AircraftPartDamage>();
            if (damage == null)
            {
                return $"'{aircraft.name}' has no AircraftPartDamage";
            }

            var wing = AircraftFixtureBuilder.PartFor(damage.Spec, AircraftDamageEffect.LeftLift);
            if (wing == null)
            {
                return $"the damage of '{aircraft.name}' has no part for {AircraftDamageEffect.LeftLift}";
            }

            Hitbox hitbox = null;
            foreach (var part in aircraft.GetComponentsInChildren<VehiclePart>())
            {
                if (string.Equals(part.PartId, wing.Id, StringComparison.Ordinal))
                {
                    hitbox = part.GetComponent<Hitbox>();
                    break;
                }
            }

            if (hitbox == null)
            {
                return $"'{aircraft.name}' has no VehiclePart with a Hitbox for '{wing.Id}'";
            }

            hitbox.ApplyResolvedHit(damage.Damage.Remaining(wing.Id), hitbox.transform.position + Vector3.up, Vector3.down, DamageKind.Bullet);
            return damage.Damage.IsBroken(wing.Id) ? null : $"hitting '{wing.Id}' for its durability did not break it";
        }

        private static void StartStep(Step step, double now)
        {
            _step = step;
            _stepStartedAt = now;
        }

        private static bool Aim(Vehicle vehicle, Vector3 point)
        {
            var seat = _rider.Seat;
            if (seat == null || seat.Vehicle != vehicle)
            {
                return false;
            }

            var mounts = vehicle.WeaponMountsOf(seat.Index);
            if (mounts.Count == 0)
            {
                return false;
            }

            var direction = point - mounts[0].Muzzle.position;
            _input.Aim = direction;
            return Vector3.Angle(mounts[0].AimDirection, direction) <= AimToleranceDegrees;
        }

        private static Vehicle FindVehicle(string vehicleId)
        {
            foreach (var vehicle in Vehicle.Active)
            {
                if (vehicle != null && string.Equals(vehicle.VehicleId, vehicleId, StringComparison.Ordinal))
                {
                    return vehicle;
                }
            }

            return null;
        }

        private static TargetDummy NearestTarget(Vector3 from)
        {
            TargetDummy best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var target in UnityEngine.Object.FindObjectsByType<TargetDummy>())
            {
                if (target.Damageable == null || target.Damageable.Id == null
                    || !target.Damageable.Id.StartsWith(VehicleRangeSceneBuilder.TargetIdPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var distance = Vector3.Distance(from, target.transform.position);
                if (distance < bestDistance)
                {
                    best = target;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);
            if (_rider != null && ReferenceEquals(_rider.SeatInputFilter, _input))
            {
                _rider.SeatInputFilter = null;
            }

            _rider = null;
            _input.Clear();

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
            }

            if (SmokeSuite.TakeOver(LogTag, exitCode))
            {
                return;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        public readonly struct LogExpectation
        {
            public LogExpectation(string prefix, string fragment)
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    throw new ArgumentException("prefix must not be empty", nameof(prefix));
                }

                Prefix = prefix;
                Fragment = fragment;
            }

            public string Prefix { get; }

            public string Fragment { get; }

            public bool Matches(string line)
            {
                return line != null
                    && line.StartsWith(Prefix, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(Fragment) || line.IndexOf(Fragment, Prefix.Length, StringComparison.Ordinal) >= 0);
            }

            public override string ToString()
            {
                return string.IsNullOrEmpty(Fragment) ? Prefix + "..." : Prefix + "..." + Fragment;
            }
        }

        // 入力をすべて置き換える。狙いが無ければ視点の向きのまま (運転中に屋根の銃を動かさない)。
        private sealed class ScriptedSeatInput : IVehicleSeatInputFilter
        {
            public Vector2 Move { get; set; }

            public bool Boost { get; set; }

            public bool Fire { get; set; }

            public Vector3 Aim { get; set; }

            public void Clear()
            {
                Move = Vector2.zero;
                Boost = false;
                Fire = false;
                Aim = Vector3.zero;
            }

            public void Filter(VehicleSeat seat, ref VehicleSeatInput input)
            {
                input = new VehicleSeatInput(
                    Move, 0f, Boost, Fire, false, false, input.AimOrigin, Aim != Vector3.zero ? Aim : input.AimDirection);
            }
        }
    }
}
