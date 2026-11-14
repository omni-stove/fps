using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) をシーンで進める <see cref="ModeRunnerBase"/> の派生。設置地点 (<see cref="BombSite"/>) と
    /// 爆弾の見た目 (<see cref="BombObject"/>) は Inspector (シーンの生成スクリプト) で配線する。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>各ラウンドの待ちに入るとき、参加者を自陣営の出現点 (<see cref="SideSpawnPoint"/>、名前の順) に ID の順で 1 人ずつ並べて体力を満たし、
    /// 待ちの間は全員の操作と行動を止める。ラウンドが始まったら戻す。陣営の出現点は攻守が替わっても同じ。</item>
    /// <item>ラウンド中は毎フレーム、生きている参加者の足元から、爆弾を持つ攻撃側が設置地点にいるか・攻撃側が落ちた爆弾の
    /// <see cref="PickupRadius"/> の中にいるか (いれば拾う)・防衛側が設置された爆弾の <see cref="DefuseRadius"/> の中にいるかを調べて
    /// <see cref="SearchAndDestroy"/> に伝える。プレイヤーは Interact (<see cref="InteractActionPath"/>、キーボードは E) を押している間だけ長押しとみなし、
    /// bot はその場にいれば長押ししているとみなす。</item>
    /// <item>bot は、爆弾を持てば設置地点 (ラウンドの番号で A, B, ... を順に選ぶ) へ、爆弾が落ちれば攻撃側がそこへ、設置されれば両陣営がそこへ向かうよう
    /// 巡回路 (<see cref="EnemyAgent.SetPatrolRoute"/>) を差し替える。ラウンドの頭でシーンの巡回路に戻す。</item>
    /// <item>HUD の時計は待ち・ラウンドの残り・起爆までの秒数を、得点の欄はラウンドの勝ち数と役割を、その下に爆弾の様子
    /// (設置後は「起爆まで n 秒」) を出す (<see cref="SearchAndDestroyText"/>)。ラウンドと爆弾の出来事は <c>[SearchAndDestroy]</c> のログに出す。</item>
    /// <item>どちらかが先取数に届いたら試合を終える (<see cref="ModeRunnerBase.EndMatch"/>)。R で読み直して最初から。</item>
    /// </list>
    /// </remarks>
    public sealed class SearchAndDestroyRunner : ModeRunnerBase
    {
        /// <summary>設置と解除の長押しに使うアクション。<see cref="InteractObjective"/> と同じ。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        public const float DefaultPickupRadius = 1.5f;
        public const float DefaultDefuseRadius = 2f;

        /// <summary>落ちた爆弾・設置された爆弾のそばにいるとみなす上下の差 (m)。段差の上下で拾ったり解除したりしないように。</summary>
        public const float ReachHeight = 2f;

        [Header("Rules")]
        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の Rules の値の代わりにこれを読む")]
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

        [Header("Objects")]
        [SerializeField, Tooltip("設置地点。番号は 0 から抜けなく 1 つずつ")]
        private BombSite[] _sites = Array.Empty<BombSite>();

        [SerializeField, Tooltip("爆弾の見た目。無くても進むが、落ちた爆弾が見えない")]
        private BombObject _bomb;

        [SerializeField, Min(0.1f), Tooltip("攻撃側が落ちた爆弾を拾う距離 (m、水平)")]
        private float _pickupRadius = DefaultPickupRadius;

        [SerializeField, Min(0.1f), Tooltip("防衛側が設置された爆弾を解除できる距離 (m、水平)")]
        private float _defuseRadius = DefaultDefuseRadius;

        private readonly Dictionary<EnemyAgent, Vector3[]> _homeRoutes = new Dictionary<EnemyAgent, Vector3[]>();
        private readonly List<string> _textLines = new List<string>();
        private SearchAndDestroy _game;
        private BombSite[] _sortedSites = Array.Empty<BombSite>();
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private Vector3 _bombGround;
        private bool _hasBombGround;

        /// <summary>進めている試合。開始前や開始に失敗したときは null。</summary>
        public SearchAndDestroy Game => _game;

        /// <summary>
        /// 落ちているか設置された爆弾の足元。持たれているときと、持ち主のいないまま始まったときは null。
        /// </summary>
        public Vector3? BombGround => _hasBombGround ? _bombGround : (Vector3?)null;

        /// <summary>プレイヤーが Interact を押していなくても長押ししていることにする (Edit Mode のテストと自動の確認用)。</summary>
        public bool SimulatedHold { get; set; }

        /// <summary>設置と解除に使う Interact アクション。解決できなければ null (プレイヤーは設置も解除もできない)。</summary>
        public InputAction InteractAction => _interactAction;

        /// <summary>調整値のアセット。null なら Inspector の値 (既定は <see cref="SearchAndDestroySpec"/> の既定値) で始める。</summary>
        public SearchAndDestroyDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

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

        /// <summary>配線した設置地点 (配線した順)。</summary>
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

        /// <summary>設置地点を配線する。番号は 0 から抜けなく 1 つずつであること (始めるときに確かめる)。</summary>
        public void SetSites(IReadOnlyList<BombSite> sites)
        {
            var copy = new BombSite[sites != null ? sites.Count : 0];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = sites[i];
            }

            _sites = copy;
        }

        /// <summary>
        /// 地点が siteCount 個の <see cref="SearchAndDestroySpec"/> を、<see cref="Definition"/> があればその値で、無ければ Inspector の値で作る。
        /// アセットの値が不正なら <see cref="InvalidOperationException"/>、Inspector の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public SearchAndDestroySpec CreateSpec(int siteCount)
        {
            if (_definition != null)
            {
                return _definition.ToSpec(siteCount);
            }

            return new SearchAndDestroySpec(
                siteCount, _roundSeconds, _plantSeconds, _defuseSeconds, _fuseSeconds, _winsToWin, _preparationSeconds, _swapEveryRounds);
        }

        /// <summary>
        /// sites を番号の順に並べて返す。空・null の要素・番号の抜けや重なり・範囲が不正なものがあれば <see cref="InvalidOperationException"/>。
        /// </summary>
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

        /// <summary>
        /// シーンを探さずに、配線した設置地点と members・spawnPoints でこの試合を始める (<see cref="ModeRunnerBase.Begin"/>)。
        /// 設置地点や設定が不正なら <see cref="InvalidOperationException"/> か <see cref="ArgumentException"/> を投げる。
        /// </summary>
        public void Begin(
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            IModeHud hudView = null)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException($"SearchAndDestroyRunner '{name}' has already started a mode");
            }

            base.Begin(CreateSession(), members, spawnPoints, localPlayer, hudView);
        }

        protected override ModeSession CreateSession()
        {
            UnsubscribeGame();

            _sortedSites = SortSites(_sites);
            _game = new SearchAndDestroy(CreateSpec(_sortedSites.Length));
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
                Debug.LogWarning($"{SearchAndDestroyText.Tag} BombObject が配線されていないため、爆弾は見えません。", this);
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
            _game.Start();
        }

        protected override void TickSession(ModeSession session, float deltaSeconds)
        {
            _game.Tick(deltaSeconds);
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            UpdateObjectiveInput();
        }

        protected override LocalizedText HudTime(ModeSession session) => SearchAndDestroyText.Clock(_game);

        protected override IReadOnlyList<ModeHudScoreLine> HudScores(ModeSession session, Side? ownSide) => SearchAndDestroyText.Scores(_game, ownSide);

        protected override LocalizedText HudResult(ModeSession session) => SearchAndDestroyText.Result(_game);

        protected override void AppendHudTexts(List<LocalizedText> lines)
        {
            lines.AddRange(SearchAndDestroyText.Lines(_game, LocalPlayer != null ? LocalPlayerId : (int?)null));
        }

        protected override void OnStop()
        {
            ReleaseInteractAction();
            UnsubscribeGame();
            _homeRoutes.Clear();
            _hasBombGround = false;
            _sortedSites = Array.Empty<BombSite>();
        }

        /// <summary>
        /// 生きている参加者の位置と長押しから、設置・拾う・解除を <see cref="SearchAndDestroy"/> に伝える。ラウンド中以外は何もしない。
        /// Play 中は <see cref="ModeRunnerBase.Tick"/> の頭で呼ばれる。
        /// </summary>
        public void UpdateObjectiveInput()
        {
            if (_game == null || _game.Phase != RoundPhase.Live || _game.Mode.State != GameModeState.Playing)
            {
                return;
            }

            var localHold = IsLocalHolding();
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
                    if (_game.Bomb == BombState.Dropped && IsNearBomb(ground, _pickupRadius))
                    {
                        _game.PickUpBomb(id);
                    }

                    _game.SetPlanting(id, _game.CarrierId == id && hold ? SiteAt(ground) : null);
                    _game.SetDefusing(id, false);
                }
                else
                {
                    _game.SetPlanting(id, null);
                    _game.SetDefusing(id, hold && _game.Bomb == BombState.Planted && IsNearBomb(ground, _defuseRadius));
                }
            }
        }

        /// <summary>ground がどれかの設置地点の中なら、その番号。どこでもなければ null。</summary>
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
            Debug.Log(SearchAndDestroyText.RoundLog(round, _game.Attacker, _game.Defender), this);

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
            Debug.Log(SearchAndDestroyText.LiveLog(round), this);
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
                SearchAndDestroyText.RoundEndLog(
                    _game.RoundNumber, winner, reason, _game.GetRoundWins(Side.Alpha), _game.GetRoundWins(Side.Bravo)),
                this);
        }

        private void OnSidesSwapped()
        {
            Debug.Log(SearchAndDestroyText.SwapLog(_game.Attacker, _game.Defender), this);
        }

        private void OnGameEnded(Side winner)
        {
            Debug.Log(SearchAndDestroyText.EndedLog(winner), this);
            EndMatch(winner);
        }

        private void OnBombPickedUp(int playerId)
        {
            Debug.Log(SearchAndDestroyText.PickUpLog(playerId), this);
            _hasBombGround = false;
            RestoreRoutes();
            RouteCarrierToSite();
            ShowBomb();
        }

        private void OnBombDropped(int playerId)
        {
            Debug.Log(SearchAndDestroyText.DropLog(playerId), this);
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
            Debug.Log(SearchAndDestroyText.PlantedLog(playerId, site), this);
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
            Debug.Log(SearchAndDestroyText.DefusedLog(playerId), this);
            ShowBomb();
        }

        private void OnBombDetonated(int site)
        {
            Debug.Log(SearchAndDestroyText.DetonatedLog(site), this);
            ShowBomb();
        }

        /// <summary>陣営ごとに、出現点を名前の順に、参加者を ID の順に組にして並べる。出現点が足りなければ先頭から使い回す。</summary>
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
                        $"{SearchAndDestroyText.Tag} プロジェクト全体の Input Actions が設定されていないため、設置も解除もできません (Project Settings > Input System Package)。",
                        this);
                    return;
                }

                _interactAction = actions.FindAction(InteractActionPath);
                if (_interactAction == null)
                {
                    Debug.LogError($"{SearchAndDestroyText.Tag} Input Actions '{actions.name}' に '{InteractActionPath}' がないため、設置も解除もできません。", this);
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
