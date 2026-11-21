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
    // 移動・上下・ブレーキ・狙い・射撃・フレアは PlayerVehicleRider.SeatInputFilter に差し込む。固定銃座・ヘリ・馬・ボート・飛行機までは歩かせずに移す。
    // ロックオンのタレットは、飛んでいる飛行機の行く先の地面へ移す。飛行機は数秒で置いた場所から離れ、射程の外へ出てしまうため。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class VehicleSmokeRun
    {
        public const string LogTag = "[VehicleSmokeRun]";

        public const float WarmUpSeconds = 1f;
        public const float EnterTimeoutSeconds = 120f;

        public const float DriveDistance = 10f;

        // ヘリは脚が地面効果の高さ (HelicopterSpec.DefaultGroundEffectHeight) より上へ出るまで上げ、浮いたまま前へ進めてから降ろす。
        public const float HoverAltitude = 5f;
        public const float HelicopterAdvanceDistance = 10f;
        public const float LandedAltitude = 0.3f;
        public const float SettledSpeed = 0.5f;

        // 既定の加速では Canter の境目に 5m ほどで届くので、その先も駆け続けて Canter のまま進めることまで見る距離にする。
        public const float RideDistance = 15f;

        public const float SailDistance = 10f;

        // Bot の飛行機は試走場が始まってすぐ外周の地面を走り出すので、プレイヤーの飛行機に乗るころには、地面に近いときの高さ (AircraftAutopilotSpec.DefaultMinGroundClearance) を超えている。
        public const float BotAircraftClimbedAltitude = 40f;

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

        // タレットは射程の 9 割の距離に置き、飛んで来る飛行機が射程の端を出入りするたびに捕捉が切れないようにする。
        // 捉えないまま行き過ぎたら、行く先へ置き直す。
        public const float LockOnReachFraction = 0.9f;
        public const float LockOnRetrySeconds = 6f;

        // 片翼を失った機体を当て方 (AircraftControlScheme) が水平へ戻そうとしても、揚力の差で釣り合う傾きは 9° ほど残るので、その手前で見る。
        public const float BrokenWingBankDegrees = 5f;
        public const float StoppedSpeed = 0.3f;
        public const float AimToleranceDegrees = 1.5f;

        // 熱で撃つ固定銃座は 10 発ほどで止まって冷めるのを待つので、耐久 1000 の車が燃え尽きるまでを見込む。
        public const float DestroyTimeoutSeconds = 120f;

        // 1 周は 150m ほどで、角ごとに落として曲がるので、見始めた所から 1 周と少しを見込む。
        public const float LapTimeoutSeconds = 150f;

        // 脱出の高さは数十 m あり、パラシュートでゆっくり降りるので長めに見込む。
        public const float LandTimeoutSeconds = 90f;
        public const float StepTimeoutSeconds = 30f;

        // 的の胴の真ん中より少し上を狙う。銃座は屋根の上にあり、真ん中を狙うと下へ向ける角度が大きくなるため。
        public static readonly Vector3 TargetAimLocal = TargetDummy.BodyLocalPosition + new Vector3(0f, 0.4f, 0f);

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.VehicleSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.VehicleSmokeRun.RequestedAt";

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<string> _lines = new List<string>();
        private static readonly string[] _stepLogVerbs =
        {
            "drove", "hovered", "advanced", "rode", "sailed", "lapped", "circling", "taxied", "flew", "held", "locked", "evaded", "banked", "landed",
        };

        private static readonly ScriptedSeatInput _input = new ScriptedSeatInput();
        private static double _playStartedAt = -1d;
        private static double _stepStartedAt;
        private static double _placedTurretAt;
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
        private static bool _evaded;
        private static int _lapLastIndex;
        private static int _lapAdvances;
        private static Vector3 _bailOutPoint;

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
            MoveToHelicopter,
            BoardHelicopter,
            Lift,
            Advance,
            Land,
            ExitHelicopter,
            MoveToHorse,
            BoardHorse,
            RideHorse,
            ExitHorse,
            MoveToBoat,
            BoardBoat,
            Sail,
            ExitBoat,
            WatchBotCar,
            WatchBotAircraft,
            MoveToAircraft,
            BoardAircraft,
            Taxi,
            TakeOff,
            Climb,
            PullUp,
            Level,
            LockOn,
            Flare,
            BankLeft,
            BailOut,
            Parachute,
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
                _evaded = false;
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
            var helicopter = VehicleRangeSceneBuilder.HelicopterName;
            var horse = VehicleRangeSceneBuilder.HorseName;
            var boat = VehicleRangeSceneBuilder.BoatName;
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
                new LogExpectation(Prefix(helicopter) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(HoveredLogPrefix(helicopter), null),
                new LogExpectation(AdvancedLogPrefix(helicopter), null),
                new LogExpectation(Prefix(helicopter) + "leave rider=", " seat=0 reason=" + VehicleLeaveReason.Request + " "),
                new LogExpectation(Prefix(horse) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(RodeLogPrefix(horse), " gait=" + HorseGait.Canter),
                new LogExpectation(Prefix(horse) + "leave rider=", " seat=0 reason=" + VehicleLeaveReason.Request + " "),
                new LogExpectation(Prefix(boat) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(SailedLogPrefix(boat), null),
                new LogExpectation(Prefix(boat) + "leave rider=", " seat=0 reason=" + VehicleLeaveReason.Request + " "),
                new LogExpectation(LappedLogPrefix(VehicleRangeSceneBuilder.BotCarName), null),
                new LogExpectation(CirclingLogPrefix(VehicleRangeSceneBuilder.BotAircraftName), null),
                new LogExpectation(Prefix(aircraft) + "board rider=", " seat=0 role=" + VehicleSeatRole.Driver),
                new LogExpectation(TaxiedLogPrefix(aircraft), null),
                new LogExpectation(FlewLogPrefix(aircraft), null),
                new LogExpectation(HeldLogPrefix(aircraft), null),
                new LogExpectation(LockedLogPrefix(aircraft), null),
                new LogExpectation(Prefix(aircraft) + "flare deployed ", null),
                new LogExpectation(EvadedLogPrefix(aircraft), null),
                new LogExpectation(Prefix(aircraft) + "part broken part=", " effect=" + AircraftDamageEffect.LeftLift + " "),
                new LogExpectation(BankedLogPrefix(aircraft), null),
                new LogExpectation(Prefix(aircraft) + "bail out rider=", " parachute=" + ParachuteOpenVerdict.Open),
                new LogExpectation(LandedLogPrefix(aircraft), null),
            };
        }

        public static string HoveredLogPrefix(string vehicle)
        {
            return $"{LogTag} hovered '{vehicle}' ";
        }

        public static string HoveredLog(string vehicle, float altitude, float seconds)
        {
            return HoveredLogPrefix(vehicle)
                + "altitude=" + altitude.ToString("0.0", CultureInfo.InvariantCulture)
                + " seconds=" + seconds.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string AdvancedLogPrefix(string vehicle)
        {
            return $"{LogTag} advanced '{vehicle}' ";
        }

        public static string AdvancedLog(string vehicle, float distance, float altitude)
        {
            return AdvancedLogPrefix(vehicle)
                + "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture)
                + " altitude=" + altitude.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string RodeLogPrefix(string vehicle)
        {
            return $"{LogTag} rode '{vehicle}' ";
        }

        public static string RodeLog(string vehicle, float distance, float speed, HorseGait gait)
        {
            return RodeLogPrefix(vehicle)
                + "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture)
                + " speed=" + speed.ToString("0.0", CultureInfo.InvariantCulture)
                + " gait=" + gait;
        }

        public static string SailedLogPrefix(string vehicle)
        {
            return $"{LogTag} sailed '{vehicle}' ";
        }

        public static string SailedLog(string vehicle, float distance, float speed)
        {
            return SailedLogPrefix(vehicle)
                + "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture)
                + " speed=" + speed.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string LappedLogPrefix(string vehicle)
        {
            return $"{LogTag} lapped '{vehicle}' ";
        }

        public static string LappedLog(string vehicle, int points, float seconds)
        {
            return LappedLogPrefix(vehicle)
                + "points=" + points.ToString(CultureInfo.InvariantCulture)
                + " seconds=" + seconds.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string CirclingLogPrefix(string vehicle)
        {
            return $"{LogTag} circling '{vehicle}' ";
        }

        public static string CirclingLog(string vehicle, float altitude, int waypoint)
        {
            return CirclingLogPrefix(vehicle)
                + "altitude=" + altitude.ToString("0.0", CultureInfo.InvariantCulture)
                + " waypoint=" + waypoint.ToString(CultureInfo.InvariantCulture);
        }

        public static string LockedLogPrefix(string vehicle)
        {
            return $"{LogTag} locked '{vehicle}' ";
        }

        public static string LockedLog(string vehicle, string turret, float distance)
        {
            return LockedLogPrefix(vehicle)
                + "by=" + turret
                + " distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        public static string EvadedLogPrefix(string vehicle)
        {
            return $"{LogTag} evaded '{vehicle}' ";
        }

        public static string EvadedLog(string vehicle, int remaining, int count)
        {
            return EvadedLogPrefix(vehicle)
                + "flares=" + remaining.ToString(CultureInfo.InvariantCulture) + "/" + count.ToString(CultureInfo.InvariantCulture);
        }

        public static string LandedLogPrefix(string vehicle)
        {
            return $"{LogTag} landed '{vehicle}' ";
        }

        // 降りた機体の名前で書くのは、どの機体から脱出したかを、並びの確かめで機体の行と結び付けるため。
        public static string LandedLog(string vehicle, string rider, float seconds, float drift)
        {
            return LandedLogPrefix(vehicle)
                + "rider=" + rider
                + " seconds=" + seconds.ToString("0.0", CultureInfo.InvariantCulture)
                + " drift=" + drift.ToString("0.0", CultureInfo.InvariantCulture);
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

        // タレットを置く地面の点。飛行機の真下から行く先へ、射程の reach 倍の距離に飛行機が来る所まで離す。
        // 高すぎて真下に置いても届かないときは null を返す。
        public static Vector3? TurretGroundAhead(Vector3 aircraft, Vector3 heading, float heightAboveGround, float range, float reachFraction)
        {
            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            if (!(reachFraction > 0f && reachFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(reachFraction), reachFraction, "reachFraction must be greater than 0 and at most 1");
            }

            var reach = range * reachFraction;
            var height = Mathf.Max(0f, heightAboveGround);
            if (height >= reach)
            {
                return null;
            }

            var flat = new Vector3(heading.x, 0f, heading.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            var ahead = Mathf.Sqrt((reach * reach) - (height * height));
            return new Vector3(aircraft.x, aircraft.y - height, aircraft.z) + (flat * ahead);
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

        public static float TimeoutFor(Step step)
        {
            switch (step)
            {
                case Step.DestroyCar:
                    return DestroyTimeoutSeconds;
                case Step.WatchBotCar:
                case Step.WatchBotAircraft:
                    return LapTimeoutSeconds;
                case Step.Parachute:
                    return LandTimeoutSeconds;
                default:
                    return StepTimeoutSeconds;
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

            var limit = TimeoutFor(_step);
            if (now - _stepStartedAt > limit)
            {
                return $"did not finish within {limit.ToString(CultureInfo.InvariantCulture)}s";
            }

            var gunCar = FindVehicle(CarFixtureBuilder.GunnerVehicleId);
            var car = FindVehicle(CarFixtureBuilder.VehicleId);
            var mountedGun = FindVehicle(VehicleRangeSceneBuilder.MountedGunName);
            var aircraft = FindVehicle(AircraftFixtureBuilder.VehicleId);
            var helicopter = FindVehicle(VehicleFixtureBuilder.HelicopterVehicleId);
            var horse = FindVehicle(VehicleFixtureBuilder.HorseVehicleId);
            var boat = FindVehicle(VehicleFixtureBuilder.BoatVehicleId);
            var botCar = FindVehicle(VehicleRangeSceneBuilder.BotCarVehicleId);
            var botAircraft = FindVehicle(VehicleRangeSceneBuilder.BotAircraftVehicleId);
            if (gunCar == null || car == null || mountedGun == null || aircraft == null
                || helicopter == null || horse == null || boat == null || botCar == null || botAircraft == null)
            {
                return $"the scene lacks a vehicle (gun car={gunCar != null}, car={car != null}, mounted gun={mountedGun != null}, aircraft={aircraft != null}, "
                    + $"helicopter={helicopter != null}, horse={horse != null}, boat={boat != null}, bot car={botCar != null}, bot aircraft={botAircraft != null}); "
                    + $"rerun {VehicleRangeSceneBuilder.MenuPath}";
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
                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player out of '{gunCar.name}'";
                    }

                    StartStep(Step.MoveToMountedGun, now);
                    return null;

                case Step.MoveToMountedGun:
                    MoveToEnterPoint(mountedGun);
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
                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player off '{mountedGun.name}'";
                    }

                    _input.Clear();
                    StartStep(Step.MoveToHelicopter, now);
                    return null;

                case Step.MoveToHelicopter:
                case Step.BoardHelicopter:
                case Step.Lift:
                case Step.Advance:
                case Step.Land:
                case Step.ExitHelicopter:
                    return AdvanceHelicopter(helicopter, now);

                case Step.MoveToHorse:
                case Step.BoardHorse:
                case Step.RideHorse:
                case Step.ExitHorse:
                    return AdvanceHorse(horse, now);

                case Step.MoveToBoat:
                case Step.BoardBoat:
                case Step.Sail:
                case Step.ExitBoat:
                    return AdvanceBoat(boat, botCar, now);

                case Step.WatchBotCar:
                case Step.WatchBotAircraft:
                    return WatchBots(botCar, botAircraft, now);

                case Step.MoveToAircraft:
                    MoveToEnterPoint(aircraft);
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

        private static string AdvanceHelicopter(Vehicle helicopter, double now)
        {
            var origin = helicopter.transform.position;
            var altitude = origin.y - _driveStart.y;
            switch (_step)
            {
                case Step.MoveToHelicopter:
                    MoveToEnterPoint(helicopter);
                    StartStep(Step.BoardHelicopter, now);
                    return null;

                case Step.BoardHelicopter:
                    if (!_rider.Interact() || _rider.Vehicle != helicopter || _rider.Seat.Role != VehicleSeatRole.Driver)
                    {
                        return $"Interact did not seat the player as the pilot of '{helicopter.name}'";
                    }

                    // 狙いを機首の向きに留めるのは、ヘリの当て方 (HelicopterControlScheme) が視線へ機首を回し、カメラの向きで勝手に旋回しないようにするため。
                    _driveStart = origin;
                    _driveForward = Heading(helicopter.transform.rotation, Vector3.forward);
                    _input.Aim = _driveForward;
                    _input.Vertical = 1f;
                    StartStep(Step.Lift, now);
                    return null;

                case Step.Lift:
                    if (altitude < HoverAltitude)
                    {
                        return null;
                    }

                    Debug.Log(HoveredLog(helicopter.name, altitude, (float)(now - _stepStartedAt)));
                    _input.Vertical = 0f;
                    _input.Move = new Vector2(0f, 1f);
                    StartStep(Step.Advance, now);
                    return null;

                case Step.Advance:
                    if (altitude < LandedAltitude)
                    {
                        return $"'{helicopter.name}' sank to the ground while moving forward";
                    }

                    var travelled = Vector3.Dot(origin - _driveStart, _driveForward);
                    if (travelled < HelicopterAdvanceDistance)
                    {
                        return null;
                    }

                    Debug.Log(AdvancedLog(helicopter.name, travelled, altitude));
                    _input.Move = Vector2.zero;
                    _input.Vertical = -1f;
                    StartStep(Step.Land, now);
                    return null;

                case Step.Land:
                    var body = helicopter.GetComponent<Rigidbody>();
                    if (altitude > LandedAltitude || (body != null && body.linearVelocity.magnitude > SettledSpeed))
                    {
                        return null;
                    }

                    _input.Clear();
                    StartStep(Step.ExitHelicopter, now);
                    return null;

                case Step.ExitHelicopter:
                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player out of '{helicopter.name}'";
                    }

                    StartStep(Step.MoveToHorse, now);
                    return null;

                default:
                    return null;
            }
        }

        private static string AdvanceHorse(Vehicle horse, double now)
        {
            var motor = horse.GetComponent<SimpleHorseMotor>();
            if (motor == null)
            {
                return $"'{horse.name}' has no SimpleHorseMotor";
            }

            switch (_step)
            {
                case Step.MoveToHorse:
                    MoveToEnterPoint(horse);
                    StartStep(Step.BoardHorse, now);
                    return null;

                case Step.BoardHorse:
                    if (!_rider.Interact() || _rider.Vehicle != horse || _rider.Seat.Role != VehicleSeatRole.Driver)
                    {
                        return $"Interact did not seat the player as the rider of '{horse.name}'";
                    }

                    _driveStart = horse.transform.position;
                    _driveForward = Heading(horse.transform.rotation, Vector3.forward);
                    _input.Move = new Vector2(0f, 1f);
                    _input.Boost = true;
                    StartStep(Step.RideHorse, now);
                    return null;

                case Step.RideHorse:
                    var travelled = Vector3.Dot(horse.transform.position - _driveStart, _driveForward);
                    if (travelled < RideDistance || motor.Gait != HorseGait.Canter)
                    {
                        return null;
                    }

                    Debug.Log(RodeLog(horse.name, travelled, motor.ForwardSpeed, motor.Gait));
                    _input.Move = Vector2.zero;
                    _input.Boost = false;
                    StartStep(Step.ExitHorse, now);
                    return null;

                // 止まるまで待ってから降りるのは、駆けたまま降ろすと、降りる位置が馬とともに流れ、降りられたかを馬の速さ次第で確かめることになるため。
                case Step.ExitHorse:
                    if (Mathf.Abs(motor.ForwardSpeed) > StoppedSpeed)
                    {
                        return null;
                    }

                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player off '{horse.name}'";
                    }

                    _input.Clear();
                    StartStep(Step.MoveToBoat, now);
                    return null;

                default:
                    return null;
            }
        }

        private static string AdvanceBoat(Vehicle boat, Vehicle botCar, double now)
        {
            var motor = boat.GetComponent<SimpleBoatMotor>();
            if (motor == null)
            {
                return $"'{boat.name}' has no SimpleBoatMotor";
            }

            switch (_step)
            {
                case Step.MoveToBoat:
                    MoveToEnterPoint(boat);
                    StartStep(Step.BoardBoat, now);
                    return null;

                case Step.BoardBoat:
                    if (!_rider.Interact() || _rider.Vehicle != boat || _rider.Seat.Role != VehicleSeatRole.Driver)
                    {
                        return $"Interact did not seat the player as the driver of '{boat.name}'";
                    }

                    _driveStart = boat.transform.position;
                    _driveForward = Heading(boat.transform.rotation, Vector3.forward);
                    _input.Move = new Vector2(0f, 1f);
                    StartStep(Step.Sail, now);
                    return null;

                case Step.Sail:
                    if (!motor.IsAfloat)
                    {
                        return $"'{boat.name}' is not afloat; the water under it is missing or too shallow";
                    }

                    var travelled = Vector3.Dot(boat.transform.position - _driveStart, _driveForward);
                    if (travelled < SailDistance)
                    {
                        return null;
                    }

                    Debug.Log(SailedLog(boat.name, travelled, motor.ForwardSpeed));
                    _input.Move = Vector2.zero;
                    StartStep(Step.ExitBoat, now);
                    return null;

                case Step.ExitBoat:
                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player out of '{boat.name}'";
                    }

                    var driver = botCar.GetComponent<BotVehicleDriver>();
                    if (driver == null)
                    {
                        return $"'{botCar.name}' has no BotVehicleDriver";
                    }

                    _lapLastIndex = driver.Follower.TargetIndex;
                    _lapAdvances = 0;
                    StartStep(Step.WatchBotCar, now);
                    return null;

                default:
                    return null;
            }
        }

        // 1 周は、向かう点が道の点の数だけ先へ進んだことで数える。どこから見始めても同じ長さを見るため。
        private static string WatchBots(Vehicle botCar, Vehicle botAircraft, double now)
        {
            switch (_step)
            {
                case Step.WatchBotCar:
                    var driver = botCar.GetComponent<BotVehicleDriver>();
                    if (driver == null || driver.Route == null)
                    {
                        return $"'{botCar.name}' has no BotVehicleDriver with a route";
                    }

                    if (!driver.IsStandInSeated)
                    {
                        return $"nobody sits in the driver seat of '{botCar.name}'";
                    }

                    var points = driver.Route.Points.Count;
                    var index = driver.Follower.TargetIndex;
                    if (index != _lapLastIndex)
                    {
                        _lapLastIndex = index;
                        _lapAdvances++;
                    }

                    if (_lapAdvances < points)
                    {
                        return null;
                    }

                    Debug.Log(LappedLog(botCar.name, points, (float)(now - _stepStartedAt)));
                    StartStep(Step.WatchBotAircraft, now);
                    return null;

                case Step.WatchBotAircraft:
                    var pilot = botAircraft.GetComponent<BotAircraftPilot>();
                    var motor = botAircraft.GetComponent<SimpleAircraftMotor>();
                    if (pilot == null || motor == null)
                    {
                        return $"'{botAircraft.name}' has no BotAircraftPilot or SimpleAircraftMotor";
                    }

                    if (botAircraft.IsWrecked)
                    {
                        return $"'{botAircraft.name}' was wrecked before it climbed";
                    }

                    var altitude = botAircraft.transform.position.y - VehicleRangeSceneBuilder.BotAircraftOrigin.y;
                    if (!pilot.IsFlying || motor.State.Grounded || altitude < BotAircraftClimbedAltitude)
                    {
                        return null;
                    }

                    Debug.Log(CirclingLog(botAircraft.name, altitude, pilot.WaypointIndex));
                    StartStep(Step.MoveToAircraft, now);
                    return null;

                default:
                    return null;
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

                    // 捉えられてフレアで外すのは両翼のあるうちに済ませる。片翼を壊すと曲がりながら落ちていき、タレットの射程を出入りするため。
                    if (!_evaded)
                    {
                        var placeProblem = PlaceTurretAhead(aircraft, now);
                        if (placeProblem != null)
                        {
                            return placeProblem;
                        }

                        StartStep(Step.LockOn, now);
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

                case Step.LockOn:
                case Step.Flare:
                    return AdvanceLockOn(aircraft, now);

                case Step.BankLeft:
                    // 機首の向きを追って狙うのは、曲がり始めた機体を元の向きへ戻そうとする右への傾きで、片翼の分の傾きを打ち消さないようにするため。
                    _input.Aim = Heading(rotation, _runwayForward);
                    var gained = LeftBankDegrees(rotation) - _bankAtBreak;
                    if (gained < BrokenWingBankDegrees)
                    {
                        return null;
                    }

                    Debug.Log(BankedLog(aircraft.name, gained));
                    StartStep(Step.BailOut, now);
                    return null;

                case Step.BailOut:
                case Step.Parachute:
                    return AdvanceBailOut(aircraft, now);

                default:
                    return null;
            }
        }

        // 警告は、タレットの捕捉 (LockOnAim) と、それを受けて出す GameSignals.LockOnWarning の両方で確かめる。HUD が読むのは後者のため。
        private static string AdvanceLockOn(Vehicle aircraft, double now)
        {
            var turret = UnityEngine.Object.FindAnyObjectByType<LockOnTurret>();
            if (turret == null)
            {
                return $"the scene has no LockOnTurret; rerun {VehicleRangeSceneBuilder.MenuPath}";
            }

            if (UnityEngine.Object.FindAnyObjectByType<LockOnWarningPublisher>() == null)
            {
                return $"the scene has no LockOnWarningPublisher; rerun {VehicleRangeSceneBuilder.MenuPath}";
            }

            var aimedAt = turret.Aim.Stage == LockOnWarningStage.Locked
                && LockOnWarningPublisher.IsAimedAt(turret.Aim, _rider.transform, aircraft.transform);
            var warned = GameSignals.LockOnWarning.Value is LockOnWarningStatus status && status.Stage == LockOnWarningStage.Locked;
            _input.Aim = _runwayForward;
            switch (_step)
            {
                case Step.LockOn:
                    if (!aimedAt || !warned)
                    {
                        return now - _placedTurretAt > LockOnRetrySeconds ? PlaceTurretAhead(aircraft, now) : null;
                    }

                    Debug.Log(LockedLog(aircraft.name, turret.name, Vector3.Distance(turret.Head.position, aircraft.transform.position)));
                    _input.PressCountermeasure();
                    StartStep(Step.Flare, now);
                    return null;

                case Step.Flare:
                    var flares = aircraft.GetComponent<VehicleCountermeasures>();
                    if (flares == null)
                    {
                        return $"'{aircraft.name}' has no VehicleCountermeasures";
                    }

                    var count = flares.Pod.Spec.Count;
                    if (flares.Remaining >= count || aimedAt || warned)
                    {
                        return null;
                    }

                    Debug.Log(EvadedLog(aircraft.name, flares.Remaining, count));
                    _evaded = true;
                    StartStep(Step.Level, now);
                    return null;

                default:
                    return null;
            }
        }

        private static string PlaceTurretAhead(Vehicle aircraft, double now)
        {
            var turret = UnityEngine.Object.FindAnyObjectByType<LockOnTurret>();
            if (turret == null)
            {
                return $"the scene has no LockOnTurret; rerun {VehicleRangeSceneBuilder.MenuPath}";
            }

            if (turret.Weapon == null)
            {
                return $"'{turret.name}' has no weapon to lock on with";
            }

            var height = aircraft.MeasureHeightAboveGround();
            if (!height.HasValue)
            {
                return $"there is no ground under '{aircraft.name}' to stand the turret on";
            }

            var range = turret.Weapon.ToSpec().Round.Range;
            var ground = TurretGroundAhead(aircraft.transform.position, aircraft.transform.forward, height.Value, range, LockOnReachFraction);
            if (!ground.HasValue)
            {
                return $"'{aircraft.name}' flies {height.Value.ToString("0.0", CultureInfo.InvariantCulture)}m up, beyond the reach of '{turret.name}' "
                    + $"({range.ToString("0.0", CultureInfo.InvariantCulture)}m)";
            }

            turret.transform.position = ground.Value;
            Physics.SyncTransforms();
            _placedTurretAt = now;
            return null;
        }

        private static string AdvanceBailOut(Vehicle aircraft, double now)
        {
            var parachute = _rider.GetComponent<ParachuteMovement>();
            if (parachute == null)
            {
                return "the player has no ParachuteMovement to land with";
            }

            switch (_step)
            {
                case Step.BailOut:
                    var verdict = aircraft.EvaluateBailOut(out var height);
                    if (verdict != BailOutVerdict.BailOut)
                    {
                        var shown = height.HasValue ? height.Value.ToString("0.0", CultureInfo.InvariantCulture) + "m" : "no ground";
                        return $"'{aircraft.name}' cannot be bailed out of ({verdict}, height {shown})";
                    }

                    if (!LeaveVehicle())
                    {
                        return $"Interact did not get the player out of '{aircraft.name}' in the air";
                    }

                    if (!parachute.IsDeployedByBailOut)
                    {
                        return $"the parachute did not open after bailing out of '{aircraft.name}' (verdict {parachute.LastVerdict})";
                    }

                    _input.Clear();
                    _bailOutPoint = _rider.transform.position;
                    StartStep(Step.Parachute, now);
                    return null;

                case Step.Parachute:
                    var health = _rider.GetComponent<Damageable>();
                    if (health != null && health.IsDead)
                    {
                        return "the player died coming down from the bail-out";
                    }

                    if (!_rider.Mover.IsGrounded)
                    {
                        return null;
                    }

                    var drift = Vector3.ProjectOnPlane(_rider.transform.position - _bailOutPoint, Vector3.up).magnitude;
                    Debug.Log(LandedLog(aircraft.name, _rider.name, (float)(now - _stepStartedAt), drift));
                    StartStep(Step.Done, now);
                    return null;

                default:
                    return null;
            }
        }

        // 木を作らない画面 (batchmode など) では表示が結ばれないので、そのときはシグナルまでを確かめる。
        private static string HudProblem(Vehicle aircraft)
        {
            if (!(AircraftSignals.Status.Value is AircraftStatus status) || status.Grounded)
            {
                return $"AircraftSignals.Status does not carry '{aircraft.name}' in flight; the HUD has no speed or altitude to show";
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

        private static void MoveToEnterPoint(Vehicle vehicle)
        {
            var seat = vehicle.Seats[0];
            var feet = seat.EnterPoints.Count > 0 ? seat.EnterPoints[0].position : seat.transform.position;
            Teleport(_rider.Mover.Controller, feet + GameBootstrap.PlayerSpawn);
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

        // 降車に掛かる秒数を待つ手順を足さず、その時間をここで一度に進める。通しで見たいのは降りられるかで、待ち時間は PlayerVehicleRiderTests で確かめる。
        private static bool LeaveVehicle()
        {
            var vehicle = _rider.Vehicle;
            if (vehicle == null || !_rider.Interact())
            {
                return false;
            }

            if (_rider.Transition == VehicleSeatTransitionKind.Exiting)
            {
                _rider.StepTransition(vehicle.Spec.ExitSeconds);
            }

            return !_rider.IsRiding;
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
        // フレアは押した 1 回だけ渡す。押したままにすると、間隔が空くたびに撒き続けるため。
        private sealed class ScriptedSeatInput : IVehicleSeatInputFilter
        {
            private bool _countermeasurePending;

            public Vector2 Move { get; set; }

            public float Vertical { get; set; }

            public bool Boost { get; set; }

            public bool Fire { get; set; }

            public Vector3 Aim { get; set; }

            public void PressCountermeasure()
            {
                _countermeasurePending = true;
            }

            public void Clear()
            {
                Move = Vector2.zero;
                Vertical = 0f;
                Boost = false;
                Fire = false;
                Aim = Vector3.zero;
                _countermeasurePending = false;
            }

            public void Filter(VehicleSeat seat, ref VehicleSeatInput input)
            {
                var countermeasure = _countermeasurePending;
                _countermeasurePending = false;
                input = new VehicleSeatInput(
                    Move, Vertical, Boost, Fire, false, false, input.AimOrigin, Aim != Vector3.zero ? Aim : input.AimDirection, countermeasure);
            }
        }
    }
}
