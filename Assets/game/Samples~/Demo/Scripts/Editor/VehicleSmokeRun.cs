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
            TakeOff,
            Climb,
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
                new LogExpectation(Prefix(VehicleRangeSceneBuilder.AircraftName) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(FlewLogPrefix(VehicleRangeSceneBuilder.AircraftName), null),
            };
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

            if (condition.StartsWith(VehicleLog.Tag + " ", StringComparison.Ordinal) || condition.StartsWith(LogTag + " drove ", StringComparison.Ordinal)
                || condition.StartsWith(LogTag + " flew ", StringComparison.Ordinal))
            {
                _lines.Add(condition);
            }

            if (TargetHitLog().Matches(condition))
            {
                _sawTargetHit = true;
            }
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
                    _input.Move = new Vector2(0f, 1f);
                    _input.Aim = _runwayForward;
                    StartStep(Step.TakeOff, now);
                    return null;

                case Step.TakeOff:
                    var motor = aircraft.GetComponent<SimpleAircraftMotor>();
                    if (motor == null)
                    {
                        return $"'{aircraft.name}' has no SimpleAircraftMotor";
                    }

                    if (motor.ForwardSpeed < motor.Spec.Aero.LiftSpeed * RotateSpeedFraction)
                    {
                        return null;
                    }

                    _input.Aim = Quaternion.AngleAxis(-ClimbPitchDegrees, Vector3.Cross(Vector3.up, _runwayForward)) * _runwayForward;
                    StartStep(Step.Climb, now);
                    return null;

                case Step.Climb:
                    var altitude = aircraft.transform.position.y - _runwayStart.y;
                    if (altitude < FlyAltitude)
                    {
                        return null;
                    }

                    Debug.Log(FlewLog(aircraft.name, altitude, aircraft.GetComponent<SimpleAircraftMotor>().ForwardSpeed));
                    _input.Clear();
                    StartStep(Step.Done, now);
                    return null;

                default:
                    return null;
            }
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
