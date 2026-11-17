using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    // サーバーの弾の起爆はサーバーのテスト (dotnet/Shooter.Server.Tests/ServerCombatTests.Projectiles.cs) で確かめるので、ここでは見ない。
    [InitializeOnLoad]
    public static class LauncherSmokeRun
    {
        public const string LogTag = "[LauncherSmokeRun]";

        public const string RocketLauncherJson = "RocketLauncher.json";
        public const string GrenadeLauncherJson = "GrenadeLauncher.json";

        public const string RocketDummyId = "RocketDummy";
        public const string GrenadeDummyId = "GrenadeDummy";

        public const float WarmUpSeconds = 0.5f;
        public const float ExerciseTimeoutSeconds = 30f;
        public const float EnterTimeoutSeconds = 120f;

        // 人の目の高さに合わせる。
        public const float MuzzleHeight = 1.5f;

        public const float RocketDummyDistance = 15f;

        public const float RocketAimShortOfDummy = 1f;

        public const float GrenadeElevationDegrees = 17f;

        // 弾の通り道に標的を置かないよう、寿命で止まると見込んだ所から横にずらす。
        public const float GrenadeDummyOffset = 2f;

        public const float SameSpotTolerance = 0.5f;

        // 爆発までの秒数はフレームの刻みの分だけずれる。
        public const float FuseTolerance = 0.15f;

        // クライアントの弾は寿命が切れた刻みでは動かさずに消すので、最後に見せた所は寿命の所から 1 フレーム分手前になる。その分まで許す。
        public const float ClientExpiryTolerance = 1.5f;

        // オフラインとクライアントの弾は別の刻みで進んで同じフレームでは終わらないので、クライアントの弾が追いつくのを待つ。
        public const float ClientSettleSeconds = 0.3f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.LauncherSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.LauncherSmokeRun.RequestedAt";
        private const float LaneSpacing = 10f;
        private const float FloorSize = 300f;
        private const float StraightTolerance = 0.05f;
        private const int LocalPlayerId = 1;

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<ExplosionInfo> _explosions = new List<ExplosionInfo>();
        private static Step _step;
        private static double _playStartedAt = -1d;
        private static double _exerciseStartedAt = -1d;
        private static bool _finished;
        private static Shot _rocket;
        private static Shot _grenade;
        private static bool _sawGrenadeExpired;

        private enum Step
        {
            FireRocket,
            WatchRocket,
            FireGrenade,
            WatchGrenade,
            Done,
        }

        static LauncherSmokeRun()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in an empty scene");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            CombatEvents.Exploded -= OnExploded;
            CombatEvents.Exploded += OnExploded;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
            CombatEvents.Exploded -= OnExploded;
        }

        private static void ResetObservations()
        {
            _errors.Clear();
            _explosions.Clear();
            _step = Step.FireRocket;
            _playStartedAt = -1d;
            _exerciseStartedAt = -1d;
            _finished = false;
            _rocket = null;
            _grenade = null;
            _sawGrenadeExpired = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // ほかの SmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            if (_grenade != null
                && condition != null
                && condition.StartsWith($"{ProjectileBody.LogTag} projectile weapon={_grenade.Spec.Id} end={BallisticState.Expired} ", StringComparison.Ordinal))
            {
                _sawGrenadeExpired = true;
            }
        }

        private static void OnExploded(ExplosionInfo explosion)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            _explosions.Add(explosion);
            foreach (var shot in new[] { _rocket, _grenade })
            {
                // 爆風のダメージは爆発を知らせる前に与え終わっているので、ここで見れば標的が復活する前に確かめられる。
                if (shot != null && string.Equals(explosion.WeaponId, shot.Spec.Id, StringComparison.Ordinal))
                {
                    shot.Explosions++;
                    shot.ExplodedAt = Time.time;
                    shot.ExplosionCenter = explosion.Center;
                    shot.DummyHurt = shot.Dummy.Health.Current < shot.Dummy.MaxHealth;
                }
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

            if (_exerciseStartedAt < 0d)
            {
                _exerciseStartedAt = now;
            }

            string problem;
            try
            {
                problem = Advance();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            if (problem == null && _step != Step.Done && now - _exerciseStartedAt > ExerciseTimeoutSeconds)
            {
                problem = $"step {_step} did not finish within {ExerciseTimeoutSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance()
        {
            switch (_step)
            {
                case Step.FireRocket:
                    BuildFloor();
                    _rocket = FireRocket();
                    _step = Step.WatchRocket;
                    return _rocket.Body == null ? "the rocket launcher fired no projectile" : CheckVisible(_rocket);

                case Step.WatchRocket:
                    _rocket.Watch();
                    if (_rocket.Body != null)
                    {
                        return CheckStraight(_rocket);
                    }

                    if (_rocket.Explosions == 0)
                    {
                        return "the rocket ended without exploding";
                    }

                    if (!_rocket.Settled)
                    {
                        return null;
                    }

                    _rocket.EndClient(ProjectileEndReason.HitWorld);
                    var rocketProblem = CheckRocket();
                    if (rocketProblem != null)
                    {
                        return rocketProblem;
                    }

                    _step = Step.FireGrenade;
                    return null;

                case Step.FireGrenade:
                    _grenade = FireGrenade();
                    _step = Step.WatchGrenade;
                    return _grenade.Body == null ? "the grenade launcher fired no projectile" : CheckVisible(_grenade);

                case Step.WatchGrenade:
                    _grenade.Watch();
                    if (_grenade.Body != null)
                    {
                        return _grenade.Explosions > 0 ? "the grenade exploded while it was still flying" : null;
                    }

                    if (!_grenade.Settled && !_grenade.ClientRemoved)
                    {
                        return null;
                    }

                    var grenadeProblem = CheckGrenade();
                    if (grenadeProblem != null)
                    {
                        return grenadeProblem;
                    }

                    _step = Step.Done;
                    return null;

                default:
                    return null;
            }
        }

        private static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "LauncherRangeFloor";
            floor.transform.position = new Vector3(0f, -0.5f, FloorSize / 2f - 10f);
            floor.transform.localScale = new Vector3(FloorSize, 1f, FloorSize);
            Physics.SyncTransforms();
        }

        private static Shot FireRocket()
        {
            var weapon = LoadSample(RocketLauncherJson);
            var muzzle = new Vector3(-LaneSpacing, MuzzleHeight, 0f);
            var dummy = SpawnDummy(RocketDummyId, new Vector3(muzzle.x, 0f, RocketDummyDistance));
            var aim = new Vector3(muzzle.x, 0f, RocketDummyDistance - RocketAimShortOfDummy);
            return Shot.Fire(weapon, muzzle, (aim - muzzle).normalized, dummy, aim);
        }

        private static Shot FireGrenade()
        {
            var weapon = LoadSample(GrenadeLauncherJson);
            var muzzle = new Vector3(LaneSpacing, MuzzleHeight, 0f);
            var direction = Quaternion.AngleAxis(-GrenadeElevationDegrees, Vector3.right) * Vector3.forward;

            // 標的を置く前の床だけの世界で同じ弾を飛ばし、寿命で止まる所を見込んでその横に標的を置く。
            var spec = weapon.ToSpec();
            var flight = new BallisticFlight(
                spec.Round.Projectile,
                null,
                new ShotWorldCollision(null),
                Physics.DefaultRaycastLayers,
                muzzle.ToNumerics(),
                direction.ToNumerics(),
                Physics.gravity.ToNumerics());
            while (!flight.IsDone)
            {
                flight.Step(Time.fixedDeltaTime);
            }

            var expected = flight.Position.ToUnity();
            var dummy = SpawnDummy(GrenadeDummyId, new Vector3(expected.x + GrenadeDummyOffset, 0f, expected.z));
            return Shot.Fire(weapon, muzzle, direction, dummy, expected);
        }

        private static WeaponDefinition LoadSample(string jsonFile)
        {
            var path = ShooterKitRoot.PhysicalPath(WeaponAssetBuilder.JsonFolder + "/" + jsonFile);
            var id = WeaponJsonImporter.ParseSpec(System.IO.File.ReadAllText(path), jsonFile).Id;
            if (!WeaponAssetBuilder.LoadRangeWeapons().TryFind(id, out var weapon))
            {
                throw new InvalidOperationException($"{WeaponAssetBuilder.RangeWeaponsPath} does not list {jsonFile} ({id}); run {WeaponAssetBuilder.MenuPath}");
            }

            return weapon;
        }

        private static Damageable SpawnDummy(string id, Vector3 position)
        {
            var dummy = TargetDummy.Create(id, position, Quaternion.LookRotation(Vector3.back, Vector3.up));
            Physics.SyncTransforms();
            return dummy.Damageable;
        }

        private static string CheckVisible(Shot shot)
        {
            return shot.Body.GetComponentInChildren<Renderer>() == null ? $"the {shot.Name} projectile has nothing to see" : null;
        }

        private static string CheckStraight(Shot shot)
        {
            var offLine = Vector3.Cross(shot.Body.transform.position - shot.Muzzle, shot.Direction).magnitude;
            return offLine > StraightTolerance
                ? $"the rocket left its straight line by {offLine.ToString("0.###", CultureInfo.InvariantCulture)}m (it should not fall)"
                : null;
        }

        private static string CheckRocket()
        {
            var rocket = _rocket;
            if (rocket.Explosions != 1)
            {
                return $"the rocket exploded {rocket.Explosions} times";
            }

            if (rocket.Travelled < (rocket.Target - rocket.Muzzle).magnitude / 2f)
            {
                return $"the rocket was seen flying only {Format(rocket.Travelled)}m";
            }

            if (Vector3.Distance(rocket.ExplosionCenter, rocket.Target) > SameSpotTolerance)
            {
                return $"the rocket exploded at {Format(rocket.ExplosionCenter)}, not where it hit the floor {Format(rocket.Target)}";
            }

            if (!rocket.DummyHurt)
            {
                return $"the rocket's blast did not reach '{RocketDummyId}'";
            }

            return rocket.CheckClient(rocket.ExplosionCenter, SameSpotTolerance);
        }

        private static string CheckGrenade()
        {
            var grenade = _grenade;
            var lifetime = grenade.Spec.Round.Projectile.LifetimeSeconds;
            if (grenade.Explosions != 1)
            {
                return $"the grenade exploded {grenade.Explosions} times";
            }

            if (!_sawGrenadeExpired)
            {
                return "the grenade did not end by running out its lifetime";
            }

            if (grenade.Apex < grenade.Muzzle.y + 1f)
            {
                return $"the grenade did not arc: it rose only to {Format(grenade.Apex)}m";
            }

            if (grenade.Bounces == 0)
            {
                return "the grenade did not bounce";
            }

            var fuse = grenade.ExplodedAt - grenade.FiredAt;
            if (Mathf.Abs(fuse - lifetime) > FuseTolerance)
            {
                return $"the grenade exploded {Format(fuse)}s after it was fired, not after its {Format(lifetime)}s lifetime";
            }

            if (Vector3.Distance(grenade.ExplosionCenter, grenade.Target) > SameSpotTolerance)
            {
                return $"the grenade exploded at {Format(grenade.ExplosionCenter)}, not where its flight ends {Format(grenade.Target)}";
            }

            if (!grenade.DummyHurt)
            {
                return $"the grenade's blast did not reach '{GrenadeDummyId}'";
            }

            var problem = grenade.CheckClient(grenade.ExplosionCenter, ClientExpiryTolerance);
            if (problem != null)
            {
                return problem;
            }

            var clientFuse = grenade.ClientRemovedAt - grenade.FiredAt;
            return Mathf.Abs(clientFuse - lifetime) > FuseTolerance
                ? $"the net client removed the grenade {Format(clientFuse)}s after it was fired, not after its {Format(lifetime)}s lifetime"
                : null;
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            Debug.Log(
                $"{LogTag} step={_step} explosions={_explosions.Count.ToString(CultureInfo.InvariantCulture)} "
                + $"rocket={_rocket?.Summary ?? "none"} grenade={_grenade?.Summary ?? "none"} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");

            if (problems.Count == 0 && _step == Step.Done)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every step was confirmed");
                }

                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            _rocket?.Detach();
            _grenade?.Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);

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

        private sealed class Shot
        {
            private ClientProjectiles _client;
            private RecordingViews _views;
            private float _clientTickedAt;
            private bool _seenBefore;

            public WeaponSpec Spec { get; private set; }

            public string Name => Spec.NameKey;

            public Vector3 Muzzle { get; private set; }

            public Vector3 Direction { get; private set; }

            public Vector3 Target { get; private set; }

            public Damageable Dummy { get; private set; }

            public ProjectileBody Body { get; private set; }

            public float FiredAt { get; private set; }

            public Vector3 LastSeen { get; private set; }

            public float Travelled { get; private set; }

            public float? OfflineEndedAt { get; private set; }

            public bool Settled => OfflineEndedAt.HasValue && Time.time - OfflineEndedAt.Value >= ClientSettleSeconds;

            public bool ClientRemoved => _views.Removed;

            public float ClientRemovedAt => _views.RemovedAt;

            public float Apex { get; private set; } = float.MinValue;

            public int Bounces { get; private set; }

            public int Explosions { get; set; }

            public float ExplodedAt { get; set; }

            public Vector3 ExplosionCenter { get; set; }

            public bool DummyHurt { get; set; }

            public string Summary => string.Format(
                CultureInfo.InvariantCulture,
                "(seen={0:0.#}m apex={1:0.##} bounces={2} explosions={3} at={4} hurt={5} client={6})",
                Travelled,
                Apex,
                Bounces,
                Explosions,
                Format(ExplosionCenter),
                DummyHurt,
                _views != null && _views.Last.HasValue ? Format(_views.Last.Value.ToUnity()) : "none");

            public static Shot Fire(WeaponDefinition weapon, Vector3 muzzle, Vector3 direction, Damageable dummy, Vector3 target)
            {
                var spec = weapon.ToSpec();
                var shot = new Shot
                {
                    Spec = spec,
                    Muzzle = muzzle,
                    Direction = direction,
                    Dummy = dummy,
                    Target = target,
                    FiredAt = Time.time,
                    LastSeen = muzzle,
                };

                var before = new HashSet<ProjectileBody>(Object.FindObjectsByType<ProjectileBody>());
                var request = new ShotRequest(
                    null, null, muzzle, spec.Round, spec.Impact, spec.Id, Physics.DefaultRaycastLayers, projectileLook: weapon.Projectile);
                ShotDeliveries.Deliver(request, new PlannedRound(muzzle.ToNumerics(), direction.ToNumerics(), 0));
                foreach (var body in Object.FindObjectsByType<ProjectileBody>())
                {
                    if (!before.Contains(body))
                    {
                        shot.Body = body;
                    }
                }

                shot._views = new RecordingViews(weapon.Projectile);
                // サーバーがいないので予測の弾を引き継ぐ弾が来ない。待ち切れずに手放さないよう、弾の寿命より長く待たせる。
                shot._client = new ClientProjectiles(
                    id => string.Equals(id, spec.Id, StringComparison.Ordinal) ? spec : null,
                    pendingSeconds: spec.Round.Projectile.LifetimeSeconds + ClientSettleSeconds + 1f);
                shot._client.Attach(shot._views, new ShotWorldCollision(null), Physics.DefaultRaycastLayers, Physics.gravity.ToNumerics(), LocalPlayerId);
                shot._client.LocalFired(
                    new FireRequestMessage { WeaponId = spec.Id, Origin = muzzle.ToNumerics(), Direction = direction.ToNumerics() },
                    spec);
                shot._clientTickedAt = Time.time;
                Debug.Log($"{LogTag} fired {spec.NameKey} weapon={spec.Id} from {Format(muzzle)} toward {Format(direction)}");
                return shot;
            }

            public void Watch()
            {
                var dt = Time.time - _clientTickedAt;
                if (dt > 0f)
                {
                    _client.Tick(dt);
                    _clientTickedAt = Time.time;
                }

                if (Body == null)
                {
                    OfflineEndedAt ??= Time.time;
                    return;
                }

                var position = Body.transform.position;
                if (_seenBefore)
                {
                    Travelled += Vector3.Distance(position, LastSeen);
                }

                _seenBefore = true;
                LastSeen = position;
                Apex = Mathf.Max(Apex, position.y);
                if (Body.Flight != null)
                {
                    Bounces = Body.Flight.Bounces;
                }
            }

            public void EndClient(ProjectileEndReason reason)
            {
                _client.Ended(new ProjectileEndedMessage { ProjectileId = 0, Reason = reason });
            }

            public string CheckClient(Vector3 offlineEnd, float tolerance)
            {
                if (_views.Shown == 0)
                {
                    return $"the net client showed no projectile for the {Name}";
                }

                if (!_views.Last.HasValue)
                {
                    return $"the net client's {Name} projectile never moved";
                }

                var clientEnd = _views.Last.Value.ToUnity();
                if (Vector3.Distance(clientEnd, offlineEnd) > tolerance)
                {
                    return $"the net client's {Name} projectile ended at {Format(clientEnd)}, not where the offline one did {Format(offlineEnd)}";
                }

                if (!_views.Removed)
                {
                    return $"the net client's {Name} projectile was left on screen after the round ended";
                }

                return null;
            }

            public void Detach()
            {
                _client?.Detach();
            }
        }

        private sealed class RecordingViews : IClientProjectileViews
        {
            private readonly ProjectileSettings _look;

            public RecordingViews(ProjectileSettings look)
            {
                _look = look;
            }

            public int Shown { get; private set; }

            public System.Numerics.Vector3? Last { get; private set; }

            public bool Removed { get; private set; }

            public float RemovedAt { get; private set; }

            public IClientProjectileView Show(string weaponId, System.Numerics.Vector3 position, System.Numerics.Vector3 velocity)
            {
                Shown++;
                return new View(this, ProjectileBody.Show(_look, weaponId, position.ToUnity(), velocity.ToUnity()));
            }

            private sealed class View : IClientProjectileView
            {
                private readonly RecordingViews _owner;
                private readonly ProjectileBody _body;

                public View(RecordingViews owner, ProjectileBody body)
                {
                    _owner = owner;
                    _body = body;
                }

                public void Move(System.Numerics.Vector3 position, System.Numerics.Vector3 velocity)
                {
                    _owner.Last = position;
                    if (_body != null)
                    {
                        _body.MoveTo(position.ToUnity(), velocity.ToUnity());
                    }
                }

                public void Stick(System.Numerics.Vector3 position)
                {
                    _owner.Last = position;
                    if (_body != null)
                    {
                        _body.StickAt(position.ToUnity());
                    }
                }

                public void Remove()
                {
                    if (!_owner.Removed)
                    {
                        _owner.Removed = true;
                        _owner.RemovedAt = Time.time;
                    }
                    if (_body != null)
                    {
                        _body.Remove();
                    }
                }
            }
        }
    }
}
