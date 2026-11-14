using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// 拠点制圧 (<see cref="Fps.Core.Domination"/>) をシーンで進める。Start で <see cref="GameBootstrap"/> の生成物と、シーンの
    /// <see cref="ControlPointZone"/>・<see cref="EnemyAgent"/> を拾い、調整値のアセット (<see cref="Definition"/>) か、それが無ければ Inspector の設定から
    /// <see cref="DominationSpec"/> を作って試合を始める。
    /// プレイヤーは Alpha (Id <see cref="PlayerId"/>)、敵は名前の順に Bravo (Id 1, 2, ...) として加える。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Update で、各拠点に範囲の参加者を調べさせ (<see cref="ControlPointZone.Scan"/>)、生きている参加者がいる拠点を
    /// <see cref="ControlPointMap.Locate"/> で決めて <see cref="Fps.Core.Domination.SetPlayerPoint"/> に渡してから <see cref="Fps.Core.Domination.Tick"/> する。</item>
    /// <item>倒れたら、倒した者 (<see cref="CombatEvents.CurrentAttribution"/>) が参加者なら <see cref="Fps.Core.Domination.ReportKill"/>、
    /// そうでなければ <see cref="Fps.Core.Domination.ReportDeath"/> に渡し、<see cref="EffectiveRespawnSeconds"/> 秒後に自チームの出現点へ戻す。
    /// 出現点は <see cref="SetSpawnPoints"/> で渡した Transform (足元の位置) から <see cref="SpawnSelector"/> で選ぶ。渡していないチームは、
    /// 試合を始めたときにそのチームの参加者が立っていた位置を使う。倒れている間のプレイヤーは動けず撃てない。</item>
    /// <item>状態の変化・拠点の占領と中立化・終了と勝者・キルとリスポーンを <see cref="DominationLog"/> の書式 (<c>[Domination]</c>) でログに出す。</item>
    /// <item>HUD があれば拠点制圧の表示 (<see cref="IHud.AttachDomination"/>) を付けて両チームのポイント・残り時間・拠点の持ち主を出し、スコアボードにキル数とデス数を渡す。</item>
    /// <item>終わったらプレイヤーの移動と射撃を止め、<see cref="MatchRunner"/> と同じく <see cref="RestartActionPath"/> (キーボードの R) で
    /// 今のシーンを読み込み直して最初からやり直す。</item>
    /// </list>
    /// GameBootstrap が並べる標的ダミーは試合に数えないので、<see cref="HidesTargetDummies"/> が true (既定) なら隠す。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DominationRunner : MonoBehaviour
    {
        public const string LogTag = DominationLog.Tag;

        /// <summary>Domination の中でのプレイヤーの Id。敵には名前の順に 1, 2, ... を振る。</summary>
        public const int PlayerId = 0;

        public const float DefaultCaptureSeconds = 8f;
        public const float DefaultPointsPerSecond = 1f;
        public const int DefaultScoreLimit = 100;
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 5f;

        /// <summary>再開に使うアクション。<see cref="MatchRunner.RestartActionPath"/> と同じ (終了後は武器を止めるのでリロードと取り合わない)。</summary>
        public const string RestartActionPath = MatchRunner.RestartActionPath;

        [Header("Rules")]
        [SerializeField, Tooltip("調整値のアセット。設定すれば下の値の代わりにこれを使う。空なら入口 (GameContent) のものを使う")]
        private DominationDefinition _definition;

        [SerializeField, Min(0.01f), Tooltip("1 人で中立の拠点を占領し終えるまでの秒数")]
        private float _captureSeconds = DefaultCaptureSeconds;

        [SerializeField, Min(0.01f), Tooltip("拠点 1 つを持っているチームに 1 秒あたり入るポイント")]
        private float _pointsPerSecond = DefaultPointsPerSecond;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこのポイントに届いたら試合が終わる")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(0.01f), Tooltip("試合の制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(1), Tooltip("占領の速さに数える人数の上限")]
        private int _maxCapturers = DominationSpec.DefaultMaxCapturers;

        [SerializeField, Min(0f), Tooltip("倒れてから自チームの出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [Header("Spawns")]
        [SerializeField, Tooltip("Alpha (プレイヤー) の出現点 (足元)。空なら試合開始時のプレイヤーの位置")]
        private Transform[] _alphaSpawns = Array.Empty<Transform>();

        [SerializeField, Tooltip("Bravo (敵) の出現点 (足元)。空なら試合開始時の各敵の位置")]
        private Transform[] _bravoSpawns = Array.Empty<Transform>();

        [SerializeField, Tooltip("GameBootstrap が並べる標的ダミーを隠すか (試合には数えない)")]
        private bool _hideTargetDummies = true;

        private readonly List<Participant> _participants = new List<Participant>();
        private readonly Dictionary<Damageable, Participant> _byDamageable = new Dictionary<Damageable, Participant>();
        private readonly List<Damageable> _candidates = new List<Damageable>();
        private readonly List<ScoreboardEntry> _scoreboardEntries = new List<ScoreboardEntry>();
        private readonly SpawnSelector _spawnSelector = new SpawnSelector(0f);
        private ControlPointZone[] _zones = Array.Empty<ControlPointZone>();
        private ControlPointMap _map;
        private Domination _domination;
        private BootstrapWorld _world;
        private IDominationHud _view;
        private InputAction _restartAction;
        private bool _enabledRestartAction;

        /// <summary>進めている試合。開始前や開始に失敗したときは null。</summary>
        public Domination Domination => _domination;

        public bool IsStarted => _domination != null;

        /// <summary>試合の拠点。番号の順。開始前は空。</summary>
        public IReadOnlyList<ControlPointZone> Zones => _zones;

        /// <summary>HUD に出しているビュー。HUD が無い (専用サーバー) か開始前は null。</summary>
        public IDominationHud View => _view;

        /// <summary>終了後に押すと再開するアクション。解決できなければ null (試合は進むが再開できない)。</summary>
        public InputAction RestartAction => _restartAction;

        /// <summary>
        /// 調整値のアセット。設定されていれば <see cref="CreateSpec"/> とリスポーンの秒数はこれの値を使い、
        /// <see cref="CaptureSeconds"/> などの Runner 自身の値は使わない。null なら Runner 自身の値 (既定は Default*) を使う。
        /// 試合を始めた後に変えても、その試合には効かない。
        /// </summary>
        public DominationDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        /// <summary>配線した <see cref="Definition"/>、空なら入口 (<see cref="GameContent"/>) のもの。どちらも無ければ null。</summary>
        public DominationDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.Domination);

        /// <summary>試合で使うリスポーンの秒数。<see cref="Definition"/> があればその値、無ければ <see cref="RespawnSeconds"/>。</summary>
        public float EffectiveRespawnSeconds => EffectiveDefinition is { } definition ? definition.RespawnSeconds : _respawnSeconds;

        public float CaptureSeconds
        {
            get => _captureSeconds;
            set => _captureSeconds = value;
        }

        public float PointsPerSecond
        {
            get => _pointsPerSecond;
            set => _pointsPerSecond = value;
        }

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public int MaxCapturers
        {
            get => _maxCapturers;
            set => _maxCapturers = value;
        }

        /// <summary>倒れてから自チームの出現点に戻るまでの秒数。試合を始めた後に変えても、その試合には効かない。</summary>
        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public bool HidesTargetDummies
        {
            get => _hideTargetDummies;
            set => _hideTargetDummies = value;
        }

        /// <summary>team の出現点 (足元の位置)。設定していなければ空。</summary>
        public IReadOnlyList<Transform> SpawnPoints(Team team)
        {
            return (team == Team.Alpha ? _alphaSpawns : _bravoSpawns) ?? Array.Empty<Transform>();
        }

        /// <summary>team の出現点 (足元の位置) を設定する。null か空なら、試合開始時にそのチームの参加者が立っていた位置を使う。</summary>
        public void SetSpawnPoints(Team team, IReadOnlyList<Transform> points)
        {
            var copy = new Transform[points != null ? points.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = points[i];
            }

            switch (team)
            {
                case Team.Alpha:
                    _alphaSpawns = copy;
                    break;
                case Team.Bravo:
                    _bravoSpawns = copy;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>
        /// 拠点が pointCount 個の <see cref="DominationSpec"/> を、<see cref="Definition"/> があればその値から、無ければ Inspector の設定から作る。
        /// 値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public DominationSpec CreateSpec(int pointCount)
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec(pointCount);
            }

            return new DominationSpec(pointCount, _captureSeconds, _pointsPerSecond, _scoreLimit, _timeLimitSeconds, _maxCapturers);
        }

        /// <summary>definition の値を Runner 自身の値 (<see cref="CaptureSeconds"/> など) に写す。<see cref="Definition"/> は変えない。</summary>
        public void CopyFrom(DominationDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _captureSeconds = definition.CaptureSeconds;
            _pointsPerSecond = definition.PointsPerSecond;
            _scoreLimit = definition.ScoreLimit;
            _timeLimitSeconds = definition.TimeLimitSeconds;
            _maxCapturers = definition.MaxCapturers;
            _respawnSeconds = definition.RespawnSeconds;
        }

        private void Start()
        {
            if (!IsStarted && !TryBeginFromScene())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            // 終了後に無効→有効にされたら、再開の入力をもう一度受け付ける。
            if (IsStarted && _domination.State == GameModeState.Ended)
            {
                AcquireRestartAction();
            }
        }

        private void OnDisable()
        {
            ReleaseRestartAction();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Update()
        {
            if (!IsStarted)
            {
                return;
            }

            Tick(Time.deltaTime);

            if (_domination.State == GameModeState.Ended && _restartAction != null && _restartAction.WasPressedThisFrame())
            {
                Restart();
            }
        }

        /// <summary>
        /// <see cref="GameBootstrap"/> の生成物・シーンの <see cref="ControlPointZone"/> と <see cref="EnemyAgent"/> を拾い、
        /// <see cref="CreateSpec"/> の値で試合を始める。既に始まっていれば何もせず true。始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBeginFromScene()
        {
            if (IsStarted)
            {
                return true;
            }

            var bootstrap = GetComponent<GameBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = FindAnyObjectByType<GameBootstrap>();
            }

            if (bootstrap == null)
            {
                Debug.LogError($"{LogTag} シーンに GameBootstrap がありません。", this);
                return false;
            }

            var world = bootstrap.World;
            if (world == null)
            {
                Debug.LogError($"{LogTag} GameBootstrap '{bootstrap.name}' がプレイヤーを生成していません。上のエラーを確認してください。", this);
                return false;
            }

            var zones = FindObjectsByType<ControlPointZone>();
            if (zones.Length == 0)
            {
                Debug.LogError($"{LogTag} シーンに ControlPointZone がありません。拠点を 1 つ以上置いてください。", this);
                return false;
            }

            var enemies = FindObjectsByType<EnemyAgent>();

            // Id をシーンを開くたびに同じにするため、見つかった順ではなく名前の順に振る。
            Array.Sort(enemies, (a, b) => string.CompareOrdinal(a.name, b.name));

            // 再開できなくても試合自体は進められるので、解決に失敗してもエラーを出して続ける。
            _restartAction = ResolveRestartAction();

            try
            {
                Begin(world, zones, enemies, CreateSpec(zones.Length));
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} 試合を始められません: {e.Message}", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// world のプレイヤーを Alpha、enemies を並びの順に Bravo として spec の試合に加え、zones を拠点にして試合を始める。
        /// zones の番号は 0 から抜けも重なりもなく、数が spec.PointCount と同じであること。enemies は空でもよい (警告を出す)。
        /// 失敗したら例外を投げ、何も購読しない。
        /// </summary>
        public void Begin(BootstrapWorld world, IReadOnlyList<ControlPointZone> zones, IReadOnlyList<EnemyAgent> enemies, DominationSpec spec)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            if (enemies == null)
            {
                throw new ArgumentNullException(nameof(enemies));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"DominationRunner '{name}' has already started a match");
            }

            if (world.PlayerDamageable == null)
            {
                throw new ArgumentException("world has no player Damageable", nameof(world));
            }

            var respawnSeconds = EffectiveRespawnSeconds;
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(RespawnSeconds), respawnSeconds, "RespawnSeconds must be a finite, non-negative value");
            }

            var map = BuildMap(zones);
            if (map.Count != spec.PointCount)
            {
                throw new ArgumentException(
                    $"the scene has {map.Count} control point(s) but spec.PointCount is {spec.PointCount}", nameof(spec));
            }

            var participants = BuildParticipants(world, enemies, respawnSeconds);
            var domination = new Domination(spec);
            foreach (var participant in participants)
            {
                domination.AddPlayer(participant.Id, participant.Team);
            }

            var sortedZones = new ControlPointZone[map.Count];
            foreach (var zone in zones)
            {
                sortedZones[zone.Index] = zone;
            }

            _world = world;
            _map = map;
            _zones = sortedZones;
            _domination = domination;
            _participants.AddRange(participants);
            foreach (var participant in participants)
            {
                _byDamageable.Add(participant.Damageable, participant);
                _candidates.Add(participant.Damageable);
                participant.Damageable.Died += OnParticipantDied;
                Debug.Log(DominationLog.Joined(participant.Id, participant.Team, participant.Name), this);
            }

            if (enemies.Count == 0)
            {
                Debug.LogWarning($"{LogTag} シーンに EnemyAgent がないため、Bravo は誰もいません。", this);
            }

            _domination.StateChanged += OnStateChanged;
            _domination.Ended += OnEnded;
            _domination.PointCaptured += OnPointCaptured;
            _domination.PointNeutralized += OnPointNeutralized;

            foreach (var zone in _zones)
            {
                zone.ShowOwner(null);
            }

            if (_hideTargetDummies && world.TargetRoot != null)
            {
                world.TargetRoot.SetActive(false);
            }

            ShowOnHud();
            _domination.Start();
            ShowScoreboard();
        }

        /// <summary>
        /// 試合の時間を deltaSeconds 進める。倒れている参加者の待ち時間を進めて戻し、拠点にいる参加者を伝えてから
        /// <see cref="Fps.Core.Domination.Tick"/> する。Play 中は Update から呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"DominationRunner '{name}' has not started a match");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a finite, non-negative value");
            }

            if (_domination.State != GameModeState.Playing)
            {
                return;
            }

            foreach (var participant in _participants)
            {
                if (participant.Countdown.Tick(deltaSeconds))
                {
                    Respawn(participant);
                }
            }

            UpdatePresence();
            _domination.Tick(deltaSeconds);
        }

        /// <summary>
        /// 各拠点に範囲にいる参加者を調べ直させ、生きている参加者がいる拠点を Domination に伝える。倒れている参加者はどこにもいないことにする。
        /// </summary>
        private void UpdatePresence()
        {
            foreach (var zone in _zones)
            {
                zone.Scan(_candidates);
            }

            foreach (var participant in _participants)
            {
                var damageable = participant.Damageable;
                int? point = null;
                if (damageable != null && !damageable.IsDead)
                {
                    point = _map.Locate(damageable.transform.position.ToNumerics());
                }

                _domination.SetPlayerPoint(participant.Id, point);
            }
        }

        /// <summary>今のシーンを読み込み直して最初からやり直す。Play 中にのみ呼べる。</summary>
        [ContextMenu("Restart")]
        public void Restart()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("DominationRunner.Restart can only reload the scene in Play mode");
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError($"{LogTag} シーン '{scene.path}' が Build Settings にないため再読み込みできません。", this);
                return;
            }

            Debug.Log(DominationLog.Restart(), this);
            SceneManager.LoadScene(scene.buildIndex);
        }

        /// <summary>購読を解除して試合から離れる。破棄時にも呼ばれる。</summary>
        public void Stop()
        {
            ReleaseRestartAction();

            foreach (var participant in _participants)
            {
                if (participant.Damageable != null)
                {
                    participant.Damageable.Died -= OnParticipantDied;
                }
            }

            _participants.Clear();
            _byDamageable.Clear();
            _candidates.Clear();

            if (_domination != null)
            {
                _domination.StateChanged -= OnStateChanged;
                _domination.Ended -= OnEnded;
                _domination.PointCaptured -= OnPointCaptured;
                _domination.PointNeutralized -= OnPointNeutralized;
            }

            if (GameUi.IsAlive(_view) && _view.Source == _domination)
            {
                _view.Clear();
            }

            _view = null;
            _zones = Array.Empty<ControlPointZone>();
            _map = null;
            _world = null;
            _domination = null;
        }

        private static ControlPointMap BuildMap(IReadOnlyList<ControlPointZone> zones)
        {
            if (zones.Count == 0)
            {
                throw new ArgumentException("at least one ControlPointZone is required", nameof(zones));
            }

            var areas = new ControlPointArea[zones.Count];
            for (var i = 0; i < areas.Length; i++)
            {
                var zone = zones[i];
                if (zone == null)
                {
                    throw new ArgumentException($"zone #{i} is null", nameof(zones));
                }

                try
                {
                    areas[i] = zone.Area;
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new ArgumentException($"ControlPointZone '{zone.name}' has an invalid area: {e.Message}", nameof(zones), e);
                }
            }

            return new ControlPointMap(areas);
        }

        private static List<Participant> BuildParticipants(BootstrapWorld world, IReadOnlyList<EnemyAgent> enemies, float respawnSeconds)
        {
            var player = world.PlayerDamageable;
            var participants = new List<Participant>
            {
                new Participant(PlayerId, Team.Alpha, player, null, FeetOf(player.transform, world.Controller), player.Id, respawnSeconds),
            };

            var seen = new HashSet<Damageable> { player };
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var damageable = enemy != null ? enemy.GetComponent<Damageable>() : null;
                if (damageable == null)
                {
                    throw new ArgumentException($"enemy #{i} has no Damageable", nameof(enemies));
                }

                if (!seen.Add(damageable))
                {
                    throw new ArgumentException($"enemy '{enemy.name}' is listed more than once", nameof(enemies));
                }

                participants.Add(new Participant(
                    PlayerId + i + 1, Team.Bravo, damageable, enemy, enemy.GroundPosition, enemy.Id, respawnSeconds));
            }

            return participants;
        }

        private void OnParticipantDied(Damageable damageable)
        {
            if (_domination == null || !_byDamageable.TryGetValue(damageable, out var victim))
            {
                return;
            }

            if (victim.Enemy == null)
            {
                SetPlayerControl(false);
            }

            if (_domination.State != GameModeState.Playing)
            {
                return;
            }

            var killer = FindKiller();
            if (killer != null && killer != victim)
            {
                _domination.ReportKill(killer.Id, victim.Id);
                Debug.Log(DominationLog.Kill(killer.Id, victim.Id), this);
            }
            else
            {
                _domination.ReportDeath(victim.Id);
                Debug.Log(DominationLog.Death(victim.Id), this);
            }

            victim.Countdown.Start();
            ShowScoreboard();
        }

        /// <summary>今倒れた者を倒した参加者。帰属が無いか参加者でなければ null。</summary>
        private Participant FindKiller()
        {
            var attribution = CombatEvents.CurrentAttribution;
            if (!attribution.HasValue)
            {
                return null;
            }

            foreach (var participant in _participants)
            {
                if (participant.Damageable != null
                    && CombatEvents.OfflineId(participant.Damageable.gameObject) == attribution.Value.KillerId)
                {
                    return participant;
                }
            }

            return null;
        }

        private void Respawn(Participant participant)
        {
            var damageable = participant.Damageable;
            if (damageable == null)
            {
                return;
            }

            var ground = SelectSpawn(participant);
            if (participant.Enemy != null)
            {
                if (!participant.Enemy.Respawn(ground, participant.HomeRotation))
                {
                    Debug.LogWarning($"{LogTag} 敵 '{participant.Name}' を戻せませんでした (倒れていません)。", participant.Enemy);
                    return;
                }
            }
            else
            {
                RespawnPlayer(ground);
            }

            Debug.Log(DominationLog.Respawn(participant.Id, participant.Team, ground.ToNumerics()), this);
        }

        private void RespawnPlayer(Vector3 ground)
        {
            var player = _world.PlayerDamageable;
            var controller = _world.Controller;

            // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
            var wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            player.transform.position = ground + (Vector3.up * FeetToRoot(controller));

            if (wasEnabled)
            {
                controller.enabled = true;
            }

            player.ResetHealth();

            // ルールから武器を作り直して弾を満たす。Edit Mode では武器が初期化されていないので作れない。
            if (_world.Weapon != null && _world.Weapon.IsInitialized)
            {
                _world.Weapon.ApplyRules(_world.Rules);
            }

            SetPlayerControl(_domination.State == GameModeState.Playing);
        }

        /// <summary>
        /// participant のチームの出現点から、生きている相手からいちばん遠い点を選ぶ (足元の位置)。
        /// 出現点を設定していなければ、試合開始時にそのチームの参加者が立っていた位置から選ぶ。
        /// </summary>
        private Vector3 SelectSpawn(Participant participant)
        {
            var points = new List<SpawnPoint>();
            foreach (var transformPoint in SpawnPoints(participant.Team))
            {
                if (transformPoint != null)
                {
                    points.Add(new SpawnPoint(transformPoint.position.ToNumerics(), participant.Team));
                }
            }

            if (points.Count == 0)
            {
                foreach (var other in _participants)
                {
                    if (other.Team == participant.Team)
                    {
                        points.Add(new SpawnPoint(other.HomeGround.ToNumerics(), participant.Team));
                    }
                }
            }

            var enemies = new List<System.Numerics.Vector3>();
            var allies = new List<System.Numerics.Vector3>();
            foreach (var other in _participants)
            {
                if (other == participant || other.Damageable == null || other.Damageable.IsDead)
                {
                    continue;
                }

                (other.Team == participant.Team ? allies : enemies).Add(other.Damageable.transform.position.ToNumerics());
            }

            return _spawnSelector.Select(points, enemies, allies, participant.Team).Position.ToUnity();
        }

        private void OnStateChanged(GameModeState state)
        {
            Debug.Log(DominationLog.State(_domination), this);
        }

        private void OnEnded(Team? winner)
        {
            Debug.Log(DominationLog.Ended(_domination), this);

            foreach (var participant in _participants)
            {
                participant.Countdown.Cancel();
            }

            SetPlayerControl(false);
            if (GameUi.IsAlive(_view))
            {
                _view.Refresh();
            }

            AcquireRestartAction();
        }

        private void OnPointCaptured(int pointIndex, Team team)
        {
            Debug.Log(DominationLog.Captured(pointIndex, team), this);
            _zones[pointIndex].ShowOwner(team);
        }

        private void OnPointNeutralized(int pointIndex, Team previousOwner)
        {
            Debug.Log(DominationLog.Neutralized(pointIndex, previousOwner), this);
            _zones[pointIndex].ShowOwner(null);
        }

        private void SetPlayerControl(bool active)
        {
            // 武器を先に止める。止めると Reload アクションが無効に戻ることがあるので、再開の入力はその後で有効にする。
            if (_world.Weapon != null)
            {
                _world.Weapon.enabled = active;
            }

            if (_world.Mover != null)
            {
                _world.Mover.enabled = active;
            }

            if (_world.Grenades != null)
            {
                _world.Grenades.enabled = active;
            }
        }

        /// <summary>HUD (専用サーバーでは無い) に拠点制圧の表示 (<see cref="IHud.AttachDomination"/>) を付けて試合を出す。</summary>
        private void ShowOnHud()
        {
            var hud = _world.Hud;
            if (!GameUi.IsAlive(hud))
            {
                return;
            }

            hud.UseTeamPalette(TeamColors.OfflineViewer);
            _view = hud.AttachDomination();
            if (_view != null)
            {
                _view.Show(_domination);
            }
        }

        /// <summary>自分 (Alpha) と敵 (Bravo) のキル数・デス数を HUD のスコアボードに渡す。HUD が無ければ何もしない。</summary>
        private void ShowScoreboard()
        {
            var scoreboard = _world != null && GameUi.IsAlive(_world.Hud) ? _world.Hud.Scoreboard : null;
            if (!GameUi.IsAlive(scoreboard) || _domination == null)
            {
                return;
            }

            _scoreboardEntries.Clear();
            foreach (var participant in _participants)
            {
                var isLocal = participant.Enemy == null;
                _scoreboardEntries.Add(new ScoreboardEntry(
                    participant.Id,
                    isLocal ? ScoreboardRows.LocalPlayerName : LocalizedText.Literal(participant.Name ?? string.Empty),
                    participant.Team,
                    _domination.GetKills(participant.Id),
                    _domination.GetDeaths(participant.Id),
                    ScoreboardConnection.None,
                    isLocal));
            }

            scoreboard.SetEntries(_scoreboardEntries, Team.Alpha);
        }

        private InputAction ResolveRestartAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていないため、R で再開できません (Project Settings > Input System Package)。",
                    this);
                return null;
            }

            var action = actions.FindAction(RestartActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{RestartActionPath}' がないため、R で再開できません。", this);
            }

            return action;
        }

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。</summary>
        private void AcquireRestartAction()
        {
            // Edit Mode では OnDisable が呼ばれず無効に戻せないので、Play 中だけ有効にする。
            if (_restartAction == null || !Application.isPlaying || !isActiveAndEnabled || _restartAction.enabled)
            {
                return;
            }

            _restartAction.Enable();
            _enabledRestartAction = true;
        }

        private void ReleaseRestartAction()
        {
            if (_enabledRestartAction && _restartAction != null)
            {
                _restartAction.Disable();
            }

            _enabledRestartAction = false;
        }

        /// <summary>CharacterController のカプセルの底 (足元) から Transform の位置までの高さ。</summary>
        private static float FeetToRoot(CharacterController controller)
        {
            return controller != null
                ? (controller.height * 0.5f) - controller.center.y
                : GameBootstrap.PlayerHeight * 0.5f;
        }

        private static Vector3 FeetOf(Transform root, CharacterController controller)
        {
            return root.position - (Vector3.up * FeetToRoot(controller));
        }

        private sealed class Participant
        {
            public int Id { get; }
            public Team Team { get; }
            public Damageable Damageable { get; }

            /// <summary>敵なら その EnemyAgent。プレイヤーなら null。</summary>
            public EnemyAgent Enemy { get; }

            /// <summary>試合を始めたときに立っていた足元の位置。出現点を設定していないチームはここから戻る。</summary>
            public Vector3 HomeGround { get; }

            /// <summary>試合を始めたときの向き。敵を戻すときに使う (プレイヤーの向きは FirstPersonLook が持つので変えない)。</summary>
            public Quaternion HomeRotation { get; }

            public string Name { get; }
            public RespawnCountdown Countdown { get; }

            public Participant(
                int id, Team team, Damageable damageable, EnemyAgent enemy, Vector3 homeGround, string name, float respawnSeconds)
            {
                Id = id;
                Team = team;
                Damageable = damageable;
                Enemy = enemy;
                HomeGround = homeGround;
                HomeRotation = damageable.transform.rotation;
                Name = name;
                Countdown = new RespawnCountdown(respawnSeconds);
            }
        }
    }
}
