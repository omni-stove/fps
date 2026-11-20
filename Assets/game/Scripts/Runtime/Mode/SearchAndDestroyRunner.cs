using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed class SearchAndDestroyRunner : ModeRunnerBase
    {
        public const string InteractActionPath = InputActionPaths.Interact;

        // 段差の上下で爆弾を拾ったり解除したりしないよう、上下の差も見る。
        public const float ReachHeight = 2f;

        [Header("Rules")]
        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の Rules の値の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private SearchAndDestroyDefinition _definition;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。設置すると止まる")]
        private float _roundSeconds = SearchAndDestroySpec.DefaultRoundSeconds;

        [SerializeField, Min(0.1f), Tooltip("設置し終えるまで長押しを続ける秒数")]
        private float _plantSeconds = SearchAndDestroySpec.DefaultPlantSeconds;

        [SerializeField, Min(0.1f), Tooltip("解除し終えるまで長押しを続ける秒数")]
        private float _defuseSeconds = SearchAndDestroySpec.DefaultDefuseSeconds;

        [SerializeField, Min(1f), Tooltip("設置してから起爆するまでの秒数")]
        private float _fuseSeconds = SearchAndDestroySpec.DefaultFuseSeconds;

        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = SearchAndDestroySpec.DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("各ラウンドの開始前の待ち (秒)。この間は動けない")]
        private float _preparationSeconds = SearchAndDestroySpec.DefaultPreparationSeconds;

        [SerializeField, Min(0), Tooltip("何ラウンドごとに攻守を入れ替えるか。0 なら入れ替えない")]
        private int _swapEveryRounds = SearchAndDestroySpec.DefaultSwapEveryRounds;

        [SerializeField, Min(0.1f), Tooltip("攻撃側が落ちた爆弾を拾う距離 (m、水平)")]
        private float _pickupRadius = SearchAndDestroySpec.DefaultBombPickupRadius;

        [SerializeField, Min(0.1f), Tooltip("防衛側が設置された爆弾を解除できる距離 (m、水平)")]
        private float _defuseRadius = SearchAndDestroySpec.DefaultDefuseRadius;

        [SerializeField, Tooltip("ラウンド制の購入経済。空なら経済なし (お金も購入も無く、持っている武器のまま)。上の Definition を配線したときは、その Definition の Economy を使う")]
        private EconomyDefinition _economy;

        [Header("Objects")]
        [SerializeField, Tooltip("設置地点。番号は 0 から抜けなく 1 つずつ")]
        private BombSite[] _sites = Array.Empty<BombSite>();

        [SerializeField, Tooltip("爆弾の見た目。無くても進むが、落ちた爆弾が見えない")]
        private BombObject _bomb;

        private readonly Dictionary<EnemyAgent, Vector3[]> _homeRoutes = new Dictionary<EnemyAgent, Vector3[]>();
        private SearchAndDestroy _game;
        private BombSite[] _sortedSites = Array.Empty<BombSite>();
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private Vector3 _bombGround;
        private bool _hasBombGround;
        private RoundEconomy _roundEconomy;
        private EconomyRules _economyRules;
        private EconomyDefinition _economySource;

        public SearchAndDestroy Game => _game;

        public RoundEconomy RoundEconomy => _roundEconomy;

        public EconomyDefinition Economy
        {
            get => _economy;
            set => _economy = value;
        }

        // 調整値と同じく、Definition (自分のものか入口のもの) があればその Economy を読み、Rules の値の代わりにする。
        public EconomyDefinition EffectiveEconomy
        {
            get
            {
                var definition = EffectiveDefinition;
                return definition != null ? definition.Economy : _economy;
            }
        }

        public Vector3? BombGround => _hasBombGround ? _bombGround : (Vector3?)null;

        // Edit Mode のテストと自動の確認で、プレイヤーが Interact を押さなくても長押ししたことにするため。
        public bool SimulatedHold { get; set; }

        public InputAction InteractAction => _interactAction;

        public SearchAndDestroyDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public SearchAndDestroyDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.SearchAndDestroy);

        public float RoundSeconds
        {
            get => _roundSeconds;
            set => _roundSeconds = value;
        }

        public float PlantSeconds
        {
            get => _plantSeconds;
            set => _plantSeconds = value;
        }

        public float DefuseSeconds
        {
            get => _defuseSeconds;
            set => _defuseSeconds = value;
        }

        public float FuseSeconds
        {
            get => _fuseSeconds;
            set => _fuseSeconds = value;
        }

        public int WinsToWin
        {
            get => _winsToWin;
            set => _winsToWin = value;
        }

        public float PreparationSeconds
        {
            get => _preparationSeconds;
            set => _preparationSeconds = value;
        }

        public int SwapEveryRounds
        {
            get => _swapEveryRounds;
            set => _swapEveryRounds = value;
        }

        public float PickupRadius
        {
            get => _pickupRadius;
            set => _pickupRadius = value;
        }

        public float DefuseRadius
        {
            get => _defuseRadius;
            set => _defuseRadius = value;
        }

        public IReadOnlyList<BombSite> Sites => _sites ?? Array.Empty<BombSite>();

        public BombObject Bomb
        {
            get => _bomb;
            set => _bomb = value;
        }

        protected override bool EndsMatchWithSession => false;

        protected override string SessionEndedLog(Side? winner)
        {
            // ModeSession が終わるのはラウンドの終わり。試合の終わりは EndMatch が出す。
            return null;
        }

        public void SetSites(IReadOnlyList<BombSite> sites)
        {
            var copy = new BombSite[sites != null ? sites.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = sites[i];
            }

            _sites = copy;
        }

        public SearchAndDestroySpec CreateSpec(int siteCount)
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec(siteCount);
            }

            return new SearchAndDestroySpec(
                siteCount, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, _winsToWin, _preparationSeconds, _swapEveryRounds,
                _pickupRadius, _defuseRadius);
        }

        public static BombSite[] SortSites(IReadOnlyList<BombSite> sites)
        {
            if (sites == null || sites.Count == 0)
            {
                throw new InvalidOperationException("no BombSite is wired to the SearchAndDestroyRunner");
            }

            var sorted = new BombSite[sites.Count];
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site == null)
                {
                    throw new InvalidOperationException($"BombSite #{i} is missing");
                }

                if (site.Index >= sorted.Length || sorted[site.Index] != null)
                {
                    throw new InvalidOperationException(
                        $"BombSite '{site.name}' has index {site.Index}; indexes must run from 0 to {sorted.Length - 1} without gaps or repeats");
                }

                try
                {
                    _ = site.Area;
                }
                catch (ArgumentOutOfRangeException e)
                {
                    throw new InvalidOperationException($"BombSite '{site.name}' has an invalid area: {e.Message}", e);
                }

                sorted[site.Index] = site;
            }

            return sorted;
        }

        public void Begin(
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException($"SearchAndDestroyRunner '{name}' has already started a mode");
            }

            base.Begin(CreateSession(), members, spawnPoints, localPlayer);
        }

        protected override ModeSession CreateSession()
        {
            UnsubscribeGame();

            _sortedSites = SortSites(_sites);
            var spec = CreateSpec(_sortedSites.Length);
            // JSON が壊れていたら、試合を始める前に TryBegin のエラーとして止める。
            var economy = EffectiveEconomy;
            _economyRules = economy != null ? economy.ToRules() : null;
            _economySource = economy;
            _game = new SearchAndDestroy(spec);
            _game.PreparationStarted += OnPreparationStarted;
            _game.RoundStarted += OnRoundStarted;
            _game.RoundEnded += OnRoundEnded;
            _game.SidesSwapped += OnSidesSwapped;
            _game.Ended += OnGameEnded;
            _game.BombPickedUp += OnBombPickedUp;
            _game.BombDropped += OnBombDropped;
            _game.BombPlanted += OnBombPlanted;
            _game.BombDefused += OnBombDefused;
            _game.BombDetonated += OnBombDetonated;

            if (_bomb == null)
            {
                Debug.LogWarning($"{SearchAndDestroyLog.Tag} BombObject が配線されていないため、爆弾は見えません。", this);
            }

            return _game.Mode;
        }

        protected override void StartSession(ModeSession session)
        {
            if (_game == null || session != _game.Mode)
            {
                throw new InvalidOperationException("SearchAndDestroyRunner can only start the session made by its own CreateSession");
            }

            _homeRoutes.Clear();
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent != null && !_homeRoutes.ContainsKey(agent))
                {
                    _homeRoutes.Add(agent, CopyRoute(agent.PatrolRoute));
                }
            }

            AcquireInteractAction();
            AttachEconomy();
            _game.Start();
        }

        // 最初のラウンドの準備 (PreparationStarted) から受けるよう、_game.Start より先に付ける。
        private void AttachEconomy()
        {
            DetachEconomy();
            if (_economyRules == null || _economySource == null)
            {
                return;
            }

            _roundEconomy = new RoundEconomy(
                _economyRules,
                _game.Rounds,
                Members,
                LocalPlayer != null ? LocalPlayerId : (int?)null,
                _economySource.FindWeapon,
                this);
        }

        private void DetachEconomy()
        {
            _roundEconomy?.Dispose();
            _roundEconomy = null;
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            _roundEconomy?.Killed(killer, victim);
        }

        protected override void TickSession(ModeSession session, float deltaSeconds)
        {
            _game.Tick(deltaSeconds);
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            UpdateObjectiveInput();
        }

        protected override bool OffersInteract
        {
            get
            {
                var local = LocalPlayer;
                if (_game == null || local == null
                    || _game.Phase != RoundPhase.Live || _game.Mode.State != GameModeState.Playing)
                {
                    return false;
                }

                var id = IdOf(local);
                if (id < 0 || !_game.Mode.HasPlayer(id) || !local.IsAlive || _game.Mode.IsDown(id))
                {
                    return false;
                }

                var ground = local.GroundPosition;
                return _game.IsAttacker(id)
                    ? _game.CarrierId == id && SiteAt(ground).HasValue
                    : _game.Bomb == BombState.Planted && IsNearBomb(ground, _game.Spec.DefuseRadius);
            }
        }

        protected override object HudDetail => _game != null ? new SearchAndDestroyStatus(_game, LocalPlayer != null ? LocalPlayerId : (int?)null) : null;

        protected override void OnStop()
        {
            ReleaseInteractAction();
            DetachEconomy();
            _economyRules = null;
            _economySource = null;
            UnsubscribeGame();
            _homeRoutes.Clear();
            _hasBombGround = false;
            _sortedSites = Array.Empty<BombSite>();
        }

        public void UpdateObjectiveInput()
        {
            if (_game == null || _game.Phase != RoundPhase.Live || _game.Mode.State != GameModeState.Playing)
            {
                return;
            }

            var localHold = IsLocalHolding();
            var pickupRadius = _game.Spec.BombPickupRadius;
            var defuseRadius = _game.Spec.DefuseRadius;
            for (var id = 0; id < Members.Count; id++)
            {
                var member = Members[id];
                if (member == null || !_game.Mode.HasPlayer(id))
                {
                    continue;
                }

                if (!member.IsAlive || _game.Mode.IsDown(id))
                {
                    _game.SetPlanting(id, null);
                    _game.SetDefusing(id, false);
                    continue;
                }

                var hold = member != LocalPlayer || localHold;
                var ground = member.GroundPosition;
                if (_game.IsAttacker(id))
                {
                    if (_game.Bomb == BombState.Dropped && IsNearBomb(ground, pickupRadius))
                    {
                        _game.PickUpBomb(id);
                    }

                    _game.SetPlanting(id, _game.CarrierId == id && hold ? SiteAt(ground) : null);
                    _game.SetDefusing(id, false);
                }
                else
                {
                    _game.SetPlanting(id, null);
                    _game.SetDefusing(id, hold && _game.Bomb == BombState.Planted && IsNearBomb(ground, defuseRadius));
                }
            }
        }

        public int? SiteAt(Vector3 ground)
        {
            foreach (var site in _sortedSites)
            {
                if (site != null && site.Contains(ground))
                {
                    return site.Index;
                }
            }

            return null;
        }

        private bool IsLocalHolding()
        {
            return SimulatedHold || (_interactAction != null && _interactAction.enabled && _interactAction.IsPressed());
        }

        private bool IsNearBomb(Vector3 ground, float radius)
        {
            if (!_hasBombGround)
            {
                return false;
            }

            var offset = ground - _bombGround;
            return Mathf.Abs(offset.y) <= ReachHeight && ((offset.x * offset.x) + (offset.z * offset.z)) <= radius * radius;
        }

        private void OnPreparationStarted(int round)
        {
            Debug.Log(SearchAndDestroyLog.RoundLog(round, _game.Attacker, _game.Defender), this);

            _hasBombGround = false;
            foreach (var site in _sortedSites)
            {
                if (site != null)
                {
                    site.ShowPlanted(false);
                }
            }

            PlaceForRound();
            RestoreRoutes();
            RouteCarrierToSite();

            foreach (var member in Members)
            {
                if (member != null)
                {
                    member.SetActing(false);
                }
            }

            ShowBomb();
        }

        private void OnRoundStarted(int round)
        {
            Debug.Log(SearchAndDestroyLog.LiveLog(round), this);
            if (IsMatchOver)
            {
                return;
            }

            foreach (var member in Members)
            {
                if (member != null && member.IsAlive)
                {
                    member.SetActing(true);
                }
            }
        }

        private void OnRoundEnded(Side? winner, RoundEndReason reason)
        {
            Debug.Log(
                SearchAndDestroyLog.RoundEndLog(
                    _game.RoundNumber, winner, reason, _game.GetRoundWins(Side.Alpha), _game.GetRoundWins(Side.Bravo)),
                this);
        }

        private void OnSidesSwapped()
        {
            Debug.Log(SearchAndDestroyLog.SwapLog(_game.Attacker, _game.Defender), this);
        }

        private void OnGameEnded(Side winner)
        {
            Debug.Log(SearchAndDestroyLog.EndedLog(winner), this);
            EndMatch(winner);
        }

        private void OnBombPickedUp(int playerId)
        {
            Debug.Log(SearchAndDestroyLog.PickUpLog(playerId), this);
            _hasBombGround = false;
            RestoreRoutes();
            RouteCarrierToSite();
            ShowBomb();
        }

        private void OnBombDropped(int playerId)
        {
            Debug.Log(SearchAndDestroyLog.DropLog(playerId), this);
            var carrier = MemberOf(playerId);
            if (carrier != null)
            {
                _bombGround = carrier.GroundPosition;
                _hasBombGround = true;
                RouteSide(_game.Attacker, _bombGround);
            }

            ShowBomb();
        }

        private void OnBombPlanted(int playerId, int site)
        {
            Debug.Log(SearchAndDestroyLog.PlantedLog(playerId, site), this);
            var planter = MemberOf(playerId);
            _bombGround = planter != null ? planter.GroundPosition : _sortedSites[site].transform.position;
            _hasBombGround = true;
            _sortedSites[site].ShowPlanted(true);
            RouteSide(_game.Attacker, _bombGround);
            RouteSide(_game.Defender, _bombGround);
            ShowBomb();
        }

        private void OnBombDefused(int playerId)
        {
            Debug.Log(SearchAndDestroyLog.DefusedLog(playerId), this);
            ShowBomb();
        }

        private void OnBombDetonated(int site)
        {
            Debug.Log(SearchAndDestroyLog.DetonatedLog(site), this);
            ShowBomb();
        }

        private void PlaceForRound()
        {
            var bySide = new Dictionary<Side, List<SideSpawnPoint>>();
            foreach (var point in SpawnPoints)
            {
                if (point == null)
                {
                    continue;
                }

                if (!bySide.TryGetValue(point.Side, out var list))
                {
                    list = new List<SideSpawnPoint>();
                    bySide.Add(point.Side, list);
                }

                list.Add(point);
            }

            foreach (var list in bySide.Values)
            {
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }

            var used = new Dictionary<Side, int>();
            foreach (var member in Members)
            {
                if (member == null)
                {
                    continue;
                }

                // Awake で初期化に失敗した bot は動かせない (自分で止まっている)。シーンに置かれた場所に立たせたままにする。
                var agent = member.GetComponent<EnemyAgent>();
                if (agent != null && !agent.IsInitialized)
                {
                    continue;
                }

                // 自陣営の出現点が無ければ、土台の Respawn が警告を出してその場で体力を満たす。
                if (!bySide.TryGetValue(member.Side, out var points))
                {
                    Respawn(member);
                    continue;
                }

                used.TryGetValue(member.Side, out var count);
                used[member.Side] = count + 1;
                RespawnAt(member, points[count % points.Count]);
            }
        }

        private void RestoreRoutes()
        {
            foreach (var pair in _homeRoutes)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetPatrolRoute(pair.Value);
                }
            }
        }

        private void RouteCarrierToSite()
        {
            if (_game.Bomb != BombState.Carried || !_game.CarrierId.HasValue || _sortedSites.Length == 0)
            {
                return;
            }

            var carrier = MemberOf(_game.CarrierId.Value);
            var agent = carrier != null ? carrier.GetComponent<EnemyAgent>() : null;
            if (agent == null)
            {
                return;
            }

            var round = Math.Max(1, _game.RoundNumber);
            var site = _sortedSites[(round - 1) % _sortedSites.Length];
            agent.SetPatrolRoute(new[] { site.transform.position });
        }

        private void RouteSide(Side side, Vector3 target)
        {
            var route = new[] { target };
            foreach (var member in Members)
            {
                if (member == null || member.Side != side)
                {
                    continue;
                }

                var agent = member.GetComponent<EnemyAgent>();
                if (agent != null)
                {
                    agent.SetPatrolRoute(route);
                }
            }
        }

        private void ShowBomb()
        {
            if (_bomb == null || _game == null)
            {
                return;
            }

            switch (_game.Bomb)
            {
                case BombState.Carried:
                    var carrier = _game.CarrierId.HasValue ? MemberOf(_game.CarrierId.Value) : null;
                    _bomb.ShowCarried(carrier != null ? carrier.transform : null);
                    break;
                default:
                    if (_hasBombGround)
                    {
                        _bomb.ShowOnGround(_bombGround, _game.Bomb);
                    }
                    else
                    {
                        _bomb.Hide();
                    }

                    break;
            }
        }

        private void AcquireInteractAction()
        {
            if (_interactAction == null)
            {
                var actions = InputSystem.actions;
                if (actions == null)
                {
                    Debug.LogError(
                        $"{SearchAndDestroyLog.Tag} プロジェクト全体の Input Actions が設定されていないため、設置も解除もできません (Project Settings > Input System Package)。",
                        this);
                    return;
                }

                _interactAction = actions.FindAction(InteractActionPath);
                if (_interactAction == null)
                {
                    Debug.LogError($"{SearchAndDestroyLog.Tag} Input Actions '{actions.name}' に '{InteractActionPath}' がないため、設置も解除もできません。", this);
                    return;
                }
            }

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。Edit Mode では戻す機会が無いので有効にしない。
            if (Application.isPlaying && !_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
        {
            if (_enabledInteractAction && _interactAction != null)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
        }

        private void UnsubscribeGame()
        {
            if (_game == null)
            {
                return;
            }

            _game.PreparationStarted -= OnPreparationStarted;
            _game.RoundStarted -= OnRoundStarted;
            _game.RoundEnded -= OnRoundEnded;
            _game.SidesSwapped -= OnSidesSwapped;
            _game.Ended -= OnGameEnded;
            _game.BombPickedUp -= OnBombPickedUp;
            _game.BombDropped -= OnBombDropped;
            _game.BombPlanted -= OnBombPlanted;
            _game.BombDefused -= OnBombDefused;
            _game.BombDetonated -= OnBombDetonated;
            _game = null;
        }

        private static Vector3[] CopyRoute(IReadOnlyList<Vector3> route)
        {
            var copy = new Vector3[route != null ? route.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            return copy;
        }
    }
}
