using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    /// <summary>
    /// PvE の波で敵を出す。波の進め方は <see cref="WaveSchedule"/> に任せ、ここでは出してよい数 (<see cref="WaveSchedule.SpawnBudget"/>)
    /// だけ敵の雛形 (<see cref="EnemyTemplate"/>) を複製して出現点に置き、倒されたら報告する。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>波の値は、<see cref="UsesTable"/> なら Inspector の表、そうでなければ式 (<see cref="WaveFormula"/>) から決める。</item>
    /// <item>出す敵は雛形の複製で、<see cref="EnemySide"/> の <see cref="SideMember"/> を付け、体力と判断の設定を波の強さの倍率で強くする
    /// (<see cref="WaveStrength"/>)。複製は有効にする前に設定するので、雛形は無効の GameObject (シーンに置いたもの) か Prefab にする。
    /// シーンに有効なまま置かれていれば、始めるときに無効にする。</item>
    /// <item>出現点は <see cref="SpawnPoints"/>。空ならシーンにある <see cref="EnemySide"/> の <see cref="SideSpawnPoint"/> を使う。
    /// 順に回して使い、敵対する陣営の誰かから <see cref="MinSpawnDistance"/> 以内の点はほかに使える点があれば飛ばす。</item>
    /// <item>出した敵は視界の外にいることが多いので、出した直後と <see cref="AlertInterval"/> ごとに、Idle か Patrol の敵へいちばん近い
    /// 敵対者の位置を物音として渡し (<see cref="EnemyAgent.Alert"/>)、そこへ向かわせる。</item>
    /// <item><see cref="GameBootstrap"/> のプレイヤーに SideMember が無ければ <see cref="PlayerSide"/> で付ける (無いと敵が狙えない)。</item>
    /// <item>倒された敵は <see cref="CorpseSeconds"/> 後に消す。倒されずに消えた敵も倒されたとして数え、波が止まらないようにする。</item>
    /// <item>波の始まり・敵を出したとき・倒されたとき・全滅・休憩の終わり・全波の終わりに <c>[Wave]</c> のログ (<see cref="WaveLog"/>) を出す。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WaveSpawner : MonoBehaviour
    {
        public const string LogTag = WaveLog.Tag;

        public const int DefaultEnemySideId = 1;
        public const int DefaultFirstEnemyCount = 3;
        public const float DefaultEnemiesPerWave = 2f;
        public const float DefaultFirstStrength = 1f;
        public const float DefaultStrengthPerWave = 0.25f;
        public const float DefaultMaxStrength = 3f;
        public const int DefaultMaxAlive = 4;
        public const float DefaultRestSeconds = 5f;
        public const int DefaultWaveCount = 5;
        public const float DefaultAlertInterval = 3f;
        public const float DefaultMinSpawnDistance = 8f;
        public const float DefaultCorpseSeconds = 5f;

        /// <summary>出現点の近くの NavMesh を探す半径 (メートル)。<see cref="EnemyAgent.OffMeshSnapRadius"/> と同じ。</summary>
        public const float SpawnSnapRadius = EnemyAgent.OffMeshSnapRadius;

        /// <summary>表で決める 1 つの波。Inspector で並べる。</summary>
        [Serializable]
        public struct WaveEntry
        {
            [Min(1), Tooltip("この波で出す敵の数")]
            public int EnemyCount;

            [Min(0.01f), Tooltip("敵の強さの倍率。1 なら雛形のまま")]
            public float Strength;

            [Min(1), Tooltip("同時に出しておく敵の上限")]
            public int MaxAlive;

            [Min(0f), Tooltip("全滅させてから次の波までの休憩 (秒)")]
            public float RestSeconds;

            public WaveEntry(int enemyCount, float strength, int maxAlive, float restSeconds)
            {
                EnemyCount = enemyCount;
                Strength = strength;
                MaxAlive = maxAlive;
                RestSeconds = restSeconds;
            }

            /// <summary>値が不正なら <see cref="ArgumentOutOfRangeException"/>。</summary>
            public Wave ToWave() => new Wave(EnemyCount, Strength, MaxAlive, RestSeconds);
        }

        [SerializeField, Tooltip("複製して出す敵。無効の GameObject (シーンに置いたもの) か Prefab。シーン生成スクリプトが配線する")]
        private EnemyAgent _enemyTemplate;

        [SerializeField, Tooltip("出現点。空ならシーンにある敵陣営の SideSpawnPoint を使う")]
        private SideSpawnPoint[] _spawnPoints = Array.Empty<SideSpawnPoint>();

        [SerializeField, Min(0), Tooltip("出す敵の陣営の ID")]
        private int _enemySideId = DefaultEnemySideId;

        [SerializeField, Min(0), Tooltip("GameBootstrap のプレイヤーに SideMember が無いとき、付ける陣営の ID")]
        private int _playerSideId;

        [SerializeField, Tooltip("Start で 1 波目を始めるか。モードの Runner から始めるときは切る")]
        private bool _beginOnStart = true;

        [Header("Waves")]
        [SerializeField, Tooltip("オンなら下の表、オフなら式で波を決める")]
        private bool _useTable;

        [SerializeField, Tooltip("表で決める波。1 波目から順に使い、最後の波を全滅させたら終わる")]
        private WaveEntry[] _table = Array.Empty<WaveEntry>();

        [SerializeField, Min(1), Tooltip("式: 1 波目の敵の数")]
        private int _firstEnemyCount = DefaultFirstEnemyCount;

        [SerializeField, Min(0f), Tooltip("式: 波ごとに増える敵の数")]
        private float _enemiesPerWave = DefaultEnemiesPerWave;

        [SerializeField, Min(0.01f), Tooltip("式: 1 波目の強さの倍率")]
        private float _firstStrength = DefaultFirstStrength;

        [SerializeField, Min(0f), Tooltip("式: 波ごとに増える強さの倍率")]
        private float _strengthPerWave = DefaultStrengthPerWave;

        [SerializeField, Min(0.01f), Tooltip("式: 強さの倍率の上限")]
        private float _maxStrength = DefaultMaxStrength;

        [SerializeField, Min(1), Tooltip("式: 同時に出しておく敵の上限")]
        private int _maxAlive = DefaultMaxAlive;

        [SerializeField, Min(0f), Tooltip("式: 波の間の休憩 (秒)")]
        private float _restSeconds = DefaultRestSeconds;

        [SerializeField, Min(0), Tooltip("式: 波の数。0 なら終わりが無い")]
        private int _waveCount = DefaultWaveCount;

        [Header("Enemies")]
        [SerializeField, Min(0.1f), Tooltip("Idle・Patrol の敵をいちばん近い敵対者へ向かわせる間隔 (秒)")]
        private float _alertInterval = DefaultAlertInterval;

        [SerializeField, Min(0f), Tooltip("敵対者からこの距離以内の出現点は、ほかに使える点があれば使わない (m)")]
        private float _minSpawnDistance = DefaultMinSpawnDistance;

        [SerializeField, Min(0f), Tooltip("倒された敵を消すまでの秒数。0 なら消さない")]
        private float _corpseSeconds = DefaultCorpseSeconds;

        private readonly List<SideSpawnPoint> _points = new List<SideSpawnPoint>();
        private readonly Dictionary<Damageable, EnemyAgent> _alive = new Dictionary<Damageable, EnemyAgent>();
        private readonly List<Damageable> _vanished = new List<Damageable>();
        private WaveSchedule _schedule;
        private Transform _staging;
        private EnemySpec _templateSpec;
        private int _templateMaxHealth;
        private int _nextPoint;
        private float _untilAlert;
        private bool _warnedNoHostile;

        /// <summary>敵を 1 体出して有効にした直後に呼ばれる。</summary>
        public event Action<EnemyAgent> EnemySpawned;

        /// <summary>進めている波の予定。開始前や開始に失敗したときは null。波の始まりや全滅はこれのイベントで受ける。</summary>
        public WaveSchedule Schedule => _schedule;

        public bool IsStarted => _schedule != null;

        /// <summary>出ていてまだ倒されていない敵。</summary>
        public IReadOnlyCollection<EnemyAgent> AliveEnemies => _alive.Values;

        /// <summary>複製して出す敵。始める前に設定する。</summary>
        public EnemyAgent EnemyTemplate
        {
            get => _enemyTemplate;
            set => _enemyTemplate = value;
        }

        /// <summary>出現点。空ならシーンにある <see cref="EnemySide"/> の <see cref="SideSpawnPoint"/> を使う。始める前に設定する。</summary>
        public IReadOnlyList<SideSpawnPoint> SpawnPoints
        {
            get => _spawnPoints;
            set => _spawnPoints = value != null ? CopyOf(value) : Array.Empty<SideSpawnPoint>();
        }

        /// <summary>出す敵の陣営。</summary>
        public Side EnemySide
        {
            get => new Side(Math.Max(0, _enemySideId));
            set => _enemySideId = value.Id;
        }

        /// <summary><see cref="GameBootstrap"/> のプレイヤーに SideMember が無いとき、付ける陣営。</summary>
        public Side PlayerSide
        {
            get => new Side(Math.Max(0, _playerSideId));
            set => _playerSideId = value.Id;
        }

        /// <summary>Start で 1 波目を始めるか。</summary>
        public bool BeginOnStart
        {
            get => _beginOnStart;
            set => _beginOnStart = value;
        }

        /// <summary>true なら <see cref="Table"/>、false なら式で波を決める。</summary>
        public bool UsesTable => _useTable;

        /// <summary>表で決める波。</summary>
        public IReadOnlyList<WaveEntry> Table => _table;

        public float AlertInterval => _alertInterval;
        public float MinSpawnDistance => _minSpawnDistance;
        public float CorpseSeconds => _corpseSeconds;

        /// <summary>表で波を決めるようにする。始める前に呼ぶ。</summary>
        public void SetTable(IReadOnlyList<WaveEntry> table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var copy = new WaveEntry[table.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = table[i];
            }

            _table = copy;
            _useTable = true;
        }

        /// <summary>式で波を決めるようにする。始める前に呼ぶ。値の検査は <see cref="WaveFormula"/> が始めるときに行う。</summary>
        public void SetFormula(
            int firstEnemyCount,
            float enemiesPerWave,
            float firstStrength,
            float strengthPerWave,
            float maxStrength,
            int maxAlive,
            float restSeconds,
            int waveCount)
        {
            _firstEnemyCount = firstEnemyCount;
            _enemiesPerWave = enemiesPerWave;
            _firstStrength = firstStrength;
            _strengthPerWave = strengthPerWave;
            _maxStrength = maxStrength;
            _maxAlive = maxAlive;
            _restSeconds = restSeconds;
            _waveCount = waveCount;
            _useTable = false;
        }

        /// <summary>Inspector の値から波の予定を作る。値が不正なら <see cref="ArgumentException"/>。</summary>
        public WaveSchedule CreateSchedule()
        {
            if (_useTable)
            {
                var waves = new Wave[_table != null ? _table.Length : 0];
                for (var i = 0; i < waves.Length; i++)
                {
                    try
                    {
                        waves[i] = _table[i].ToWave();
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        throw new ArgumentException($"wave {i + 1} in the table is invalid: {e.Message}", e);
                    }
                }

                return WaveSchedule.FromTable(waves);
            }

            var formula = new WaveFormula(
                _firstEnemyCount, _enemiesPerWave, _firstStrength, _strengthPerWave, _maxAlive, _restSeconds, _maxStrength);
            return WaveSchedule.FromFormula(formula, _waveCount);
        }

        private void Start()
        {
            if (_beginOnStart && !IsStarted && !TryBegin())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Update()
        {
            if (IsStarted)
            {
                Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// 雛形・出現点・波の予定を確かめて 1 波目を始める。既に始まっていれば何もせず true。
        /// 始められなければ理由を Debug.LogError に出して false を返す。
        /// </summary>
        public bool TryBegin()
        {
            if (IsStarted)
            {
                return true;
            }

            if (!TryPrepareTemplate())
            {
                return false;
            }

            CollectSpawnPoints();
            if (_points.Count == 0)
            {
                Debug.LogError($"{LogTag} 陣営 {EnemySide.Id} の出現点がありません。SpawnPoints を配線するか SideSpawnPoint を置いてください。", this);
                return false;
            }

            WaveSchedule schedule;
            try
            {
                schedule = CreateSchedule();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} 波の設定が不正です: {e.Message}", this);
                return false;
            }

            EnsurePlayerSide();
            Begin(schedule);
            return true;
        }

        /// <summary>
        /// schedule を購読して始める。schedule は開始前 (Waiting) であること。雛形と出現点は <see cref="TryBegin"/> と同じく確かめる。
        /// 既に始まっていれば <see cref="InvalidOperationException"/>、雛形か出現点が使えなければ <see cref="InvalidOperationException"/>。
        /// </summary>
        public void Begin(WaveSchedule schedule)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"WaveSpawner '{name}' has already started");
            }

            if (schedule.Phase != WavePhase.Waiting)
            {
                throw new ArgumentException($"schedule must be Waiting, but is {schedule.Phase}", nameof(schedule));
            }

            if (_templateSpec == null && !TryPrepareTemplate())
            {
                throw new InvalidOperationException($"WaveSpawner '{name}' has no usable enemy template");
            }

            if (_points.Count == 0)
            {
                CollectSpawnPoints();
                if (_points.Count == 0)
                {
                    throw new InvalidOperationException($"WaveSpawner '{name}' has no spawn point for side {EnemySide.Id}");
                }
            }

            if (_staging == null)
            {
                // 無効の親の下で複製すると Awake が走らないので、有効にする前に体力と判断の設定を変えられる。
                var staging = new GameObject(name + "-Staging");
                staging.SetActive(false);
                staging.transform.SetParent(transform, false);
                _staging = staging.transform;
            }

            _schedule = schedule;
            _schedule.WaveStarted += OnWaveStarted;
            _schedule.WaveCleared += OnWaveCleared;
            _schedule.RestEnded += OnRestEnded;
            _schedule.Completed += OnCompleted;
            _untilAlert = _alertInterval;
            _schedule.Start();
            SpawnDue();
        }

        /// <summary>
        /// 時間を deltaSeconds 進め、倒されずに消えた敵を倒されたとして数え、出してよい数だけ敵を出し、間隔ごとに敵を敵対者へ向かわせる。
        /// Play 中は Update から呼ぶ。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException($"WaveSpawner '{name}' has not started");
            }

            CountVanished();
            _schedule.Tick(deltaSeconds);
            SpawnDue();

            _untilAlert -= deltaSeconds;
            if (_untilAlert <= 0f)
            {
                _untilAlert = _alertInterval;
                AlertIdleEnemies();
            }
        }

        /// <summary>購読を解除して止める。出ている敵はそのまま残す。破棄時にも呼ばれる。</summary>
        public void Stop()
        {
            foreach (var damageable in _alive.Keys)
            {
                if (damageable != null)
                {
                    damageable.Died -= OnEnemyDied;
                }
            }

            if (_schedule != null)
            {
                _schedule.WaveStarted -= OnWaveStarted;
                _schedule.WaveCleared -= OnWaveCleared;
                _schedule.RestEnded -= OnRestEnded;
                _schedule.Completed -= OnCompleted;
            }

            _alive.Clear();
            _schedule = null;
        }

        private bool TryPrepareTemplate()
        {
            if (_enemyTemplate == null)
            {
                Debug.LogError($"{LogTag} 敵の雛形 (EnemyTemplate) が設定されていません。シーン生成スクリプトで配線してください。", this);
                return false;
            }

            var templateObject = _enemyTemplate.gameObject;
            var damageable = _enemyTemplate.GetComponent<Damageable>();
            if (damageable == null || _enemyTemplate.GetComponent<NavMeshAgent>() == null)
            {
                Debug.LogError($"{LogTag} 敵の雛形 '{templateObject.name}' には NavMeshAgent と Damageable が必要です。", _enemyTemplate);
                return false;
            }

            if (_enemyTemplate.WeaponDefinition == null)
            {
                Debug.LogError($"{LogTag} 敵の雛形 '{templateObject.name}' に WeaponDefinition が設定されていません。", _enemyTemplate);
                return false;
            }

            EnemySpec spec;
            try
            {
                spec = _enemyTemplate.CreateSpec();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{LogTag} 敵の雛形 '{templateObject.name}' の設定が不正です: {e.Message}", _enemyTemplate);
                return false;
            }

            // シーンに有効なまま置かれた雛形は、それ自体が 1 体の敵として動いてしまい、波の数と合わなくなる。
            if (templateObject.scene.IsValid() && templateObject.activeSelf)
            {
                Debug.LogWarning($"{LogTag} 敵の雛形 '{templateObject.name}' が有効だったので無効にしました。雛形は無効にして置いてください。", _enemyTemplate);
                templateObject.SetActive(false);
            }

            _templateSpec = spec;
            _templateMaxHealth = Math.Max(1, damageable.MaxHealth);
            return true;
        }

        private void CollectSpawnPoints()
        {
            _points.Clear();
            var side = EnemySide;
            var fromScene = _spawnPoints == null || _spawnPoints.Length == 0;
            var source = fromScene ? SideSpawnPoint.FindAll() : _spawnPoints;
            foreach (var point in source)
            {
                if (point != null && (!fromScene || point.Side == side))
                {
                    _points.Add(point);
                }
            }

            // FindObjectsByType の順は決まっていないので、出る順が Play ごとに変わらないよう名前で並べる。
            if (fromScene)
            {
                _points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        private void EnsurePlayerSide()
        {
            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            var player = bootstrap != null && bootstrap.World != null ? bootstrap.World.Player : null;
            if (player == null || player.GetComponent<SideMember>() != null)
            {
                return;
            }

            var member = player.AddComponent<SideMember>();
            member.Side = PlayerSide;
            if (member.Side == EnemySide)
            {
                Debug.LogWarning($"{LogTag} プレイヤーの陣営が敵と同じ {EnemySide.Id} です。敵はプレイヤーを狙いません。", this);
            }
        }

        private void SpawnDue()
        {
            while (_schedule != null && _schedule.SpawnBudget > 0)
            {
                try
                {
                    SpawnOne();
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError($"{LogTag} 敵を出せないので波を止めます: {e.Message}", this);
                    Stop();
                    enabled = false;
                    return;
                }
            }
        }

        private void SpawnOne()
        {
            var waveNumber = _schedule.WaveNumber;
            var wave = _schedule.CurrentWave;
            var point = NextPoint();
            var index = _schedule.Spawned + 1;
            var id = $"wave{waveNumber}-{index}";

            var clone = Instantiate(_enemyTemplate.gameObject, _staging, false);
            clone.name = $"Wave{waveNumber}-Enemy{index}";

            // 雛形が無効でも、複製は自分だけ有効にしておかないと、親から外したときに有効にならない。
            clone.SetActive(true);

            var navAgent = clone.GetComponent<NavMeshAgent>();
            var ground = NavMesh.SamplePosition(point.Position, out var hit, SpawnSnapRadius, NavMesh.AllAreas) ? hit.position : point.Position;
            var offset = navAgent != null ? navAgent.baseOffset : 0f;
            clone.transform.SetPositionAndRotation(ground + (Vector3.up * offset), point.Rotation);

            var agent = clone.GetComponent<EnemyAgent>();
            var damageable = clone.GetComponent<Damageable>();
            agent.Id = id;
            agent.ApplySpec(WaveStrength.Apply(_templateSpec, wave.StrengthMultiplier));
            damageable.Initialize(id, WaveStrength.Health(_templateMaxHealth, wave.StrengthMultiplier));

            var member = clone.GetComponent<SideMember>();
            if (member == null)
            {
                member = clone.AddComponent<SideMember>();
            }

            member.Side = EnemySide;

            _schedule.ReportSpawned();
            _alive.Add(damageable, agent);
            damageable.Died += OnEnemyDied;

            clone.transform.SetParent(null, true);
            Debug.Log(WaveLog.Spawned(_schedule, id, point.name), clone);

            AlertTowardHostile(agent, member);
            EnemySpawned?.Invoke(agent);
        }

        private SideSpawnPoint NextPoint()
        {
            var hostiles = HostilePositions();
            var minSqr = _minSpawnDistance * _minSpawnDistance;
            for (var tried = 0; tried < _points.Count; tried++)
            {
                var candidate = _points[(_nextPoint + tried) % _points.Count];
                if (candidate != null && !IsNear(candidate.Position, hostiles, minSqr))
                {
                    _nextPoint = (_nextPoint + tried + 1) % _points.Count;
                    return candidate;
                }
            }

            // どの点も近ければ、出さずに待つより順番どおりに出す方が波が止まらない。
            for (var tried = 0; tried < _points.Count; tried++)
            {
                var candidate = _points[(_nextPoint + tried) % _points.Count];
                if (candidate != null)
                {
                    _nextPoint = (_nextPoint + tried + 1) % _points.Count;
                    return candidate;
                }
            }

            throw new InvalidOperationException($"all spawn points of WaveSpawner '{name}' have been destroyed");
        }

        private List<Vector3> HostilePositions()
        {
            var positions = new List<Vector3>();
            var side = EnemySide;
            foreach (var member in SideMember.FindAll())
            {
                if (member != null && member.Side != side && member.IsAlive)
                {
                    positions.Add(member.GroundPosition);
                }
            }

            return positions;
        }

        private static bool IsNear(Vector3 position, List<Vector3> others, float sqrDistance)
        {
            foreach (var other in others)
            {
                if ((other - position).sqrMagnitude <= sqrDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private void AlertIdleEnemies()
        {
            foreach (var pair in _alive)
            {
                var agent = pair.Value;
                if (agent == null || !agent.IsInitialized)
                {
                    continue;
                }

                if (agent.State == EnemyState.Idle || agent.State == EnemyState.Patrol)
                {
                    AlertTowardHostile(agent, agent.GetComponent<SideMember>());
                }
            }
        }

        private void AlertTowardHostile(EnemyAgent agent, SideMember member)
        {
            if (member == null)
            {
                return;
            }

            var hostile = SideMember.NearestHostile(member.Side, member.GroundPosition, SideMember.FindAll());
            if (hostile == null)
            {
                if (!_warnedNoHostile)
                {
                    _warnedNoHostile = true;
                    Debug.LogWarning($"{LogTag} 陣営 {member.Side.Id} の敵が狙える相手 (違う陣営の SideMember) がいません。", this);
                }

                return;
            }

            agent.Alert(hostile.GroundPosition);
        }

        private void CountVanished()
        {
            _vanished.Clear();
            foreach (var pair in _alive)
            {
                if (pair.Key == null)
                {
                    _vanished.Add(pair.Key);
                }
            }

            foreach (var damageable in _vanished)
            {
                _alive.Remove(damageable);
                Debug.LogWarning($"{LogTag} 倒されずに消えた敵を倒されたとして数えます (wave={_schedule.WaveNumber})。", this);
                if (_schedule.Phase == WavePhase.Fighting && _schedule.Alive > 0)
                {
                    ReportKilled("vanished", this);
                }
            }
        }

        private void OnEnemyDied(Damageable damageable)
        {
            damageable.Died -= OnEnemyDied;
            if (_schedule == null || !_alive.Remove(damageable))
            {
                return;
            }

            if (_corpseSeconds > 0f)
            {
                Destroy(damageable.gameObject, _corpseSeconds);
            }

            ReportKilled(damageable.Id, damageable);
        }

        // 全滅の報告で WaveCleared のログが出るので、倒されたログを先に出す。
        private void ReportKilled(string enemyId, UnityEngine.Object context)
        {
            Debug.Log(WaveLog.Killed(_schedule.WaveNumber, enemyId, _schedule.RemainingEnemies - 1), context);
            _schedule.ReportKilled();
        }

        private void OnWaveStarted(int number, Wave wave)
        {
            _warnedNoHostile = false;
            Debug.Log(WaveLog.Started(number, _schedule.WaveCount, wave), this);
        }

        private void OnWaveCleared(int number)
        {
            var rest = _schedule.Phase == WavePhase.Resting ? _schedule.RestRemaining : (float?)null;
            Debug.Log(WaveLog.Cleared(number, rest), this);
        }

        private void OnRestEnded(int nextNumber)
        {
            Debug.Log(WaveLog.RestEnded(nextNumber), this);
        }

        private void OnCompleted()
        {
            Debug.Log(WaveLog.Completed(_schedule.WaveNumber), this);
        }

        private static SideSpawnPoint[] CopyOf(IReadOnlyList<SideSpawnPoint> points)
        {
            var copy = new SideSpawnPoint[points.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = points[i];
            }

            return copy;
        }
    }
}
