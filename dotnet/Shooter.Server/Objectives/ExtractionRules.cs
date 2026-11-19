using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;
using RaidOutcome = Shooter.Core.ExtractionOutcome;
using StashOutcome = Shooter.Protocol.ExtractionOutcome;

namespace Shooter.Server
{
    // GameRules.Objective が Extraction のとき、出現した人ごとに Core の ExtractionSession で出撃させ、開いた脱出地点で待ち切った人の持ち物を
    // その人のアカウントの持ち物庫 (ServerStashStore) に入れる。出撃の時間・脱出・持ち帰りの数え方はオフラインの ExtractionRunner と同じ Core に任せる。
    // 全員が敵なので、Session (TeamDeathmatchSession) は味方撃ちを通し、倒れた人を試合の終わりまで出直させない (respawnsByRound)。
    // 全員の出撃が終わるか、出撃の時間が切れたら試合を終える
    internal sealed class ExtractionRules : IMatchObjectiveRules, ISnapshotContributor
    {
        // Bot は接続の鍵を持たないので、人の鍵 (RejoinKey は . を含まない) と重ならない名前で持ち物庫を分ける
        public const string BotAccountPrefix = "bot.";

        private static readonly IReadOnlyList<WeaponSpec> _noWeapons = Array.Empty<WeaponSpec>();
        private static readonly IReadOnlyList<string> _noPoints = Array.Empty<string>();

        private readonly IMatchObjectiveHost _host;
        private readonly MatchRulesModule _rules;
        private readonly ServerLoot _loot;
        private readonly ServerStashStore _store;
        private readonly System.Random _random;
        private readonly Dictionary<int, Raider> _raiders = new Dictionary<int, Raider>();
        private readonly List<Raider> _ticking = new List<Raider>();
        private readonly StashStateMessage _state = new StashStateMessage();
        private readonly Dictionary<string, int> _merged = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _mergedOrder = new List<string>();
        private ExtractionSpec _spec = ExtractionSpec.Default;
        private StashSpec _stashSpec = StashSpec.Default;
        private ItemCatalog _items = ItemCatalog.Empty;
        private NetMapExtractionPoint[] _points;
        private IReadOnlyList<string> _openPointIds = _noPoints;
        private float _elapsed;
        private int _raidsThisMatch;
        private bool _started;
        private bool _loggedAllOver;

        // 種を渡さなければ起動の時刻から決める。同じ種なら同じ地点が開く
        public ExtractionRules(IMatchObjectiveHost host, MatchRulesModule rules, ServerLoot loot, ServerStashStore store, int? seed = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _store = store ?? throw new ArgumentNullException(
                nameof(store), "the " + MatchObjective.Extraction + " rules keep each account's stash on disk; start the server with " + ServerLaunch.StashDirectoryFlag);
            _random = new System.Random(seed ?? Environment.TickCount);
        }

        public MatchObjective Objective => MatchObjective.Extraction;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => _points != null ? Math.Max(0f, _spec.RaidSeconds - _elapsed) : (float?)null;

        public float ElapsedSeconds => _elapsed;

        public bool PointsOpen => _elapsed >= _spec.OpenAfterSeconds;

        // UseMap の前は空。Id の順に並べる
        public IReadOnlyList<NetMapExtractionPoint> Points => _points ?? Array.Empty<NetMapExtractionPoint>();

        public IReadOnlyList<string> OpenPointIds => _openPointIds;

        public ExtractionSpec Spec => _spec;

        // 試合に入っていない人・まだ出撃していない人は null
        public ExtractionSession RaidOf(int playerId)
        {
            return _raiders.TryGetValue(playerId, out var raider) ? raider.Session : null;
        }

        public Stash StashOf(int playerId)
        {
            return _raiders.TryGetValue(playerId, out var raider) ? raider.Stash : null;
        }

        // 持ち物庫を置けない人 (鍵の無い人) は null
        public string AccountOf(int playerId)
        {
            return _raiders.TryGetValue(playerId, out var raider) ? raider.AccountId : ResolveAccount(playerId);
        }

        // 全員を敵にするので、Session のチームは出現地点を分けるためだけに使う (FreeForAllRules と同じ)
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(settings);
            var sessionRules = rules
                .WithFriendlyFire(FriendlyFire.On)
                .WithActions(rules.Actions.WithDowned(Downed.Off));
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, sessionRules, respawnsByRound: true);
        }

        public bool AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            return a != b;
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            Build(points, "cannot change to map " + map.Name + " for the " + Objective + " match: ");
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var built = Build(points, "cannot start the " + Objective + " match on map " + map.Name + ": ");
            ReadTuning();
            _points = built;
            PickOpenPoints();
        }

        public void Start()
        {
            if (_points == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            ReadTuning();
            _loot.Given += HandleGiven;
            _host.Log.WriteLine(ExtractionRulesLog.Started(_spec, _points, _openPointIds, _store.Directory));
        }

        // 止まる (サーバーを閉じる・ルールを替える) ときに出撃中の人は、持ち込んだ物を持ち物庫に戻す。
        // 出撃を終えずに試合ごと無くなるので、持ち込んだ物を失わせると、遊んだ人の落ち度でなく物を失うため
        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _loot.Given -= HandleGiven;
            foreach (var raider in _raiders.Values)
            {
                if (raider.IsRaiding)
                {
                    CallOff(raider);
                }
            }

            _raiders.Clear();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!_started || !matchPlaying)
            {
                return false;
            }

            _elapsed = Math.Min(_spec.RaidSeconds, _elapsed + deltaSeconds);
            _ticking.Clear();
            _ticking.AddRange(_raiders.Values);
            foreach (var raider in _ticking)
            {
                if (!raider.IsRaiding)
                {
                    continue;
                }

                var standing = StandingPointOf(raider.PlayerId);
                if (standing != null && raider.Session.IsOpen(standing))
                {
                    // 脱出が決まる Tick の前に、持っている物を今の持ち物に揃える。使った物と、出撃の外で配られた物 (最初の持ち物) を持ち帰らせないため
                    MatchCarried(raider);
                }

                raider.Session.Tick(deltaSeconds, standing);
            }

            OpenBoxesForBots();
            if (_raidsThisMatch > 0 && !AnyRaiding())
            {
                if (!_loggedAllOver)
                {
                    _loggedAllOver = true;
                    _host.Log.WriteLine(ExtractionRulesLog.AllOver(_raidsThisMatch));
                }

                return true;
            }

            return _elapsed >= _spec.RaidSeconds;
        }

        public void PlayerJoined(int playerId)
        {
            var raider = Enroll(playerId);
            if (raider != null)
            {
                SendState(raider);
            }
        }

        // 抜けた人は倒れたのと同じく持っている物を失い、居た所に箱を残す。つなぎ直した人は持ち物庫から新しく出撃する
        public void PlayerLeft(int playerId)
        {
            if (!_raiders.TryGetValue(playerId, out var raider))
            {
                return;
            }

            if (raider.IsRaiding)
            {
                Fall(raider);
            }

            _raiders.Remove(playerId);
        }

        public void PlayerKilled(int killerId, int victimId)
        {
            if (_raiders.TryGetValue(victimId, out var raider) && raider.IsRaiding)
            {
                Fall(raider);
            }
        }

        // 出撃の始まり。持ち込む武器を持ち物庫から取り、続く持ち替え (FilterWeapons) でその武器だけを配らせる
        public void PlayerSpawning(int playerId)
        {
            var raider = Enroll(playerId);
            if (raider == null || raider.IsRaiding)
            {
                return;
            }

            if (_points == null)
            {
                return;
            }

            if (_elapsed >= _spec.RaidSeconds)
            {
                raider.Allowed.Clear();
                _host.Log.WriteLine(ExtractionRulesLog.NoRaid(playerId, _elapsed));
                return;
            }

            var broughtIn = ChooseBroughtIn(raider);
            var session = new ExtractionSession(_spec, raider.Stash, _openPointIds, broughtIn);
            raider.Session = session;
            raider.Picked = false;
            raider.GoalBoxId = null;
            raider.GoalBoxPosition = null;
            raider.GoalPointIndex = null;
            session.Ended += _ => HandleEnded(raider, session);

            // 時計を試合と揃える。途中から入った人の地点も、試合の時間で開く
            if (_elapsed > 0f)
            {
                session.Tick(_elapsed, null);
            }

            _raidsThisMatch++;
            _loggedAllOver = false;
            _host.Log.WriteLine(ExtractionRulesLog.RaidStarted(playerId, session));
            Save(raider);
            SendState(raider);
        }

        public IReadOnlyList<WeaponSpec> FilterWeapons(int playerId, IReadOnlyList<WeaponSpec> weapons)
        {
            ArgumentNullException.ThrowIfNull(weapons);
            if (!_raiders.TryGetValue(playerId, out var raider) || !raider.IsRaiding || raider.Allowed.Count == 0)
            {
                return _noWeapons;
            }

            var kept = new List<WeaponSpec>(raider.Allowed.Count);
            foreach (var spec in weapons)
            {
                if (raider.Allowed.Contains(spec.Id))
                {
                    kept.Add(spec);
                }
            }

            return kept;
        }

        public InteractVerdict Interact(int playerId, int itemId)
        {
            return _raiders.TryGetValue(playerId, out var raider) && raider.IsRaiding
                ? InteractVerdict.Accepted
                : InteractVerdict.Refused(InteractRefusal.NotInteractable);
        }

        // 何かを拾ったら開いている脱出地点へ、拾う前は近い箱へ向かわせる。箱が残っていなければ脱出地点へ。
        // 同じ先の間は同じ値を返すよう、選んだ箱と地点を開いている・開かれていない間は持ち続ける
        public Vector3? BotGoal(int playerId)
        {
            if (!_raiders.TryGetValue(playerId, out var raider) || !raider.IsRaiding || !_host.Bodies.TryGet(playerId, out var body))
            {
                return null;
            }

            var position = body.Position;
            if (!raider.Picked)
            {
                if (raider.GoalBoxId.HasValue && _loot.TryGetContents(raider.GoalBoxId.Value, out _, out _) && !_loot.IsOpened(raider.GoalBoxId.Value)
                    && raider.GoalBoxPosition.HasValue)
                {
                    return raider.GoalBoxPosition;
                }

                if (_loot.TryFindClosedBox(position, out var boxId, out var boxPosition))
                {
                    raider.GoalBoxId = boxId;
                    raider.GoalBoxPosition = boxPosition;
                    return boxPosition;
                }
            }

            raider.GoalBoxId = null;
            raider.GoalBoxPosition = null;
            if (raider.GoalPointIndex.HasValue && raider.Session.IsOpen(_points[raider.GoalPointIndex.Value].Id))
            {
                return _points[raider.GoalPointIndex.Value].Center;
            }

            var nearest = NearestOpenPoint(raider.Session, position);
            raider.GoalPointIndex = nearest;
            return nearest.HasValue ? _points[nearest.Value].Center : (Vector3?)null;
        }

        // 試合の続きの出撃を終わらせる。時間切れと同じく持っている物を失う
        public void MatchEnded()
        {
            _ticking.Clear();
            _ticking.AddRange(_raiders.Values);
            foreach (var raider in _ticking)
            {
                if (raider.IsRaiding)
                {
                    raider.Session.Tick(raider.Session.RemainingSeconds, null);
                }
            }
        }

        public void MatchRestarted()
        {
            _elapsed = 0f;
            _raidsThisMatch = 0;
            _loggedAllOver = false;
            foreach (var raider in _raiders.Values)
            {
                if (raider.IsRaiding)
                {
                    raider.Session.Tick(raider.Session.RemainingSeconds, null);
                }

                raider.Session = null;
                raider.Allowed.Clear();
                raider.Picked = false;
                raider.GoalBoxId = null;
                raider.GoalBoxPosition = null;
                raider.GoalPointIndex = null;
            }

            if (_points != null)
            {
                PickOpenPoints();
            }

            _host.Log.WriteLine(ExtractionRulesLog.Restart(_openPointIds));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // チームでは勝ち負けを決めない。勝ちは脱出できた人に数える (IsWinningPlayer)
        public Team? DecideWinner(Team? killWinner) => null;

        public bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            return _raiders.TryGetValue(playerId, out var raider) && raider.Session != null && raider.Session.Outcome == RaidOutcome.Extracted;
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = 0;
            snapshot.BravoPoints = 0;
            snapshot.HasActivePoint = false;
            snapshot.ActivePointIndex = 0;
            snapshot.ObjectiveRemainingSeconds = 0;
            snapshot.PayloadProgress = 0f;
            snapshot.PayloadCheckpointsPassed = 0;
            snapshot.PayloadPushers = 0;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // 脱出地点は地点ファイルの Id の順に 0 から振る。箱 (ServerLoot) は離れた所から振るので重ならない。開いているかは全員に同じ
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            if (_points == null)
            {
                return;
            }

            for (var i = 0; i < _points.Length && items.Count < items.MaxCount; i++)
            {
                var point = _points[i];
                ref var item = ref items.Add();
                item.ItemId = i;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.ExtractionPoint;
                item.Team = Team.Alpha;
                item.HasOwner = false;
                item.OwnerId = 0;
                item.Position = point.Center;
                item.State = ObjectiveItemState.Home;
                item.HasCarrier = false;
                item.CarrierId = 0;
                item.Opened = IsPointOpen(point.Id);
                item.Radius = point.Radius;
            }
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        // 脱出の待ちは持ち主にだけ見せる
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var owner = snapshot.Owner;
            owner.HasExtractionPoint = false;
            owner.ExtractionPointId = 0;
            owner.ExtractionProgress = 0f;
            if (receiver == null || !_raiders.TryGetValue(receiver.PlayerId, out var raider) || !raider.IsRaiding)
            {
                return;
            }

            var holding = raider.Session.HoldingPointId;
            var index = holding != null ? IndexOf(holding) : -1;
            if (index < 0)
            {
                return;
            }

            owner.HasExtractionPoint = true;
            owner.ExtractionPointId = index;
            owner.ExtractionProgress = Math.Clamp(raider.Session.HoldProgress, 0f, 1f);
        }

        // 地点の Id の順に並べるのは、同じ種で同じ地点が開き、スナップショットの物の Id がマップの書き出しの順に左右されないようにするため
        private static NetMapExtractionPoint[] Build(NetMapPoints points, string failure)
        {
            if (points.ExtractionPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    failure + "it has no extraction points; place ExtractionZones in the scene and export the map points with make export-server-rules");
            }

            var sorted = new NetMapExtractionPoint[points.ExtractionPoints.Count];
            for (var i = 0; i < sorted.Length; i++)
            {
                sorted[i] = points.ExtractionPoints[i];
            }

            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        // ルールが替わっても目的は作り直されるので、今のルールのデータから読めば足りる
        private void ReadTuning()
        {
            var data = _rules.Data;
            _spec = data?.Extraction ?? ExtractionSpec.Default;
            _stashSpec = data?.Stash ?? StashSpec.Default;
            _items = data?.Items ?? ItemCatalog.Empty;
        }

        private void PickOpenPoints()
        {
            var ids = new string[_points.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = _points[i].Id;
            }

            _openPointIds = ExtractionSession.PickOpenPoints(_spec, ids, _random);
        }

        private bool IsPointOpen(string pointId)
        {
            if (!PointsOpen)
            {
                return false;
            }

            foreach (var id in _openPointIds)
            {
                if (string.Equals(id, pointId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int IndexOf(string pointId)
        {
            for (var i = 0; i < _points.Length; i++)
            {
                if (string.Equals(_points[i].Id, pointId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        // 倒れている人・まだ出ていない人は地点に立っていない。開いた地点と閉じた地点が重なっていれば、開いた方に立っているとみなす (ExtractionRunner.ZoneAt と同じ)
        private string StandingPointOf(int playerId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(playerId) || !session.IsAlive(playerId) || session.IsDowned(playerId)
                || !_host.Bodies.TryGet(playerId, out var body) || !body.HasSpawned)
            {
                return null;
            }

            string closed = null;
            var raid = _raiders[playerId].Session;
            foreach (var point in _points)
            {
                if (!point.Contains(body.Position))
                {
                    continue;
                }

                if (raid.IsOpen(point.Id))
                {
                    return point.Id;
                }

                closed ??= point.Id;
            }

            return closed;
        }

        private int? NearestOpenPoint(ExtractionSession session, Vector3 from)
        {
            int? nearest = null;
            var best = float.PositiveInfinity;
            for (var i = 0; i < _points.Length; i++)
            {
                if (!session.IsOpen(_points[i].Id))
                {
                    continue;
                }

                var dx = _points[i].Center.X - from.X;
                var dz = _points[i].Center.Z - from.Z;
                var distance = (dx * dx) + (dz * dz);
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private bool AnyRaiding()
        {
            foreach (var raider in _raiders.Values)
            {
                if (raider.IsRaiding)
                {
                    return true;
                }
            }

            return false;
        }

        // Bot は開ける要求を送らないので、向かわせた箱に届いたらサーバーが開ける
        private void OpenBoxesForBots()
        {
            if (!_loot.IsRunning)
            {
                return;
            }

            foreach (var raider in _ticking)
            {
                if (!raider.IsBot || !raider.IsRaiding || !raider.GoalBoxId.HasValue)
                {
                    continue;
                }

                var boxId = raider.GoalBoxId.Value;
                if (_loot.IsInReach(boxId, raider.PlayerId))
                {
                    _loot.Open(raider.PlayerId, boxId);
                }
            }
        }

        private Raider Enroll(int playerId)
        {
            if (_raiders.TryGetValue(playerId, out var raider))
            {
                return raider;
            }

            var match = _rules.Context?.Match;
            if (match == null || !match.TryGetPlayer(playerId, out var player))
            {
                return null;
            }

            var account = ResolveAccount(player);
            Stash stash;
            var saves = false;
            if (account == null)
            {
                _host.Log.WriteLine(ExtractionRulesLog.NoAccount(playerId));
                stash = Stash.Fresh(_stashSpec, _items);
            }
            else
            {
                try
                {
                    var had = _store.HasSave(account);
                    stash = _store.Load(account, _stashSpec, _items);
                    saves = true;
                    _host.Log.WriteLine(ExtractionRulesLog.Loaded(playerId, account, _store.PathOf(account), stash.Stacks.Count, had));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    _host.Log.WriteLine(ExtractionRulesLog.LoadFailed(playerId, account, e.Message));
                    stash = Stash.Fresh(_stashSpec, _items);
                }
            }

            raider = new Raider(playerId, player.IsBot, account, saves, stash);
            _raiders.Add(playerId, raider);
            return raider;
        }

        private string ResolveAccount(int playerId)
        {
            var match = _rules.Context?.Match;
            return match != null && match.TryGetPlayer(playerId, out var player) ? ResolveAccount(player) : null;
        }

        // 通信路が確かめた ProductUserId があれば、MatchServer がそれを鍵にしている
        private static string ResolveAccount(ServerPlayer player)
        {
            if (player.IsBot)
            {
                return BotAccountPrefix + player.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrEmpty(player.RejoinKey))
            {
                return null;
            }

            var key = player.RejoinKey.ToLowerInvariant();
            return ServerStashStore.IsAccountId(key) ? key : null;
        }

        // 選んだ並び (選んでいなければ既定の並び) のうち、持ち物庫にある武器だけを持ち込む
        private List<ItemCount> ChooseBroughtIn(Raider raider)
        {
            raider.Allowed.Clear();
            var broughtIn = new List<ItemCount>();
            var chosen = _rules.Loadout?.WeaponsFor(raider.PlayerId) ?? _noWeapons;
            foreach (var spec in chosen)
            {
                if (!_items.TryFindWeapon(spec.Id, out var item))
                {
                    _host.Log.WriteLine(ExtractionRulesLog.NotAnItem(raider.PlayerId, spec.Id));
                    continue;
                }

                var taken = 0;
                foreach (var already in broughtIn)
                {
                    if (string.Equals(already.ItemId, item.Id, StringComparison.Ordinal))
                    {
                        taken += already.Count;
                    }
                }

                if (raider.Stash.CountOf(item.Id) <= taken)
                {
                    _host.Log.WriteLine(ExtractionRulesLog.NotInStash(raider.PlayerId, spec.Id));
                    continue;
                }

                broughtIn.Add(new ItemCount(item.Id, 1));
                raider.Allowed.Add(spec.Id);
            }

            return broughtIn;
        }

        // 持ち帰れる物は、出撃で数えた物 (持ち込み・箱) と今持っている物の少ない方
        private void MatchCarried(Raider raider)
        {
            // 箱のモジュールが止まった後 (サーバーを閉じている間) は持ち物を数えられないので、数えた物のままにする
            if (!_loot.IsRunning)
            {
                return;
            }

            var held = _loot.HeldItems(raider.PlayerId);
            var carried = raider.Session.Carried;
            var next = new List<ItemCount>(carried.Count);
            foreach (var item in carried)
            {
                var count = 0;
                foreach (var have in held)
                {
                    if (string.Equals(have.ItemId, item.ItemId, StringComparison.Ordinal))
                    {
                        count = have.Count;
                        break;
                    }
                }

                count = Math.Min(count, item.Count);
                if (count > 0)
                {
                    next.Add(new ItemCount(item.ItemId, count));
                }
            }

            raider.Session.ReplaceCarried(next);
        }

        // 倒れた・抜けた人の出撃を終え、持っていた物を居た所の箱にする
        private void Fall(Raider raider)
        {
            MatchCarried(raider);
            raider.Session.Die();
            if (_loot.IsRunning)
            {
                _loot.SpawnDeathBox(raider.PlayerId);
            }
        }

        private void CallOff(Raider raider)
        {
            var returned = new List<ItemCount>();
            foreach (var item in raider.Session.BroughtIn)
            {
                var added = raider.Stash.Add(item.ItemId, item.Count);
                if (added > 0)
                {
                    returned.Add(new ItemCount(item.ItemId, added));
                }
            }

            _host.Log.WriteLine(ExtractionRulesLog.Abandoned(raider.PlayerId, returned));
            Save(raider);
        }

        private void HandleEnded(Raider raider, ExtractionSession session)
        {
            // 前の出撃の終わりが遅れて届いても、今の出撃の人を書き換えない
            if (!ReferenceEquals(raider.Session, session))
            {
                return;
            }

            _host.Log.WriteLine(ExtractionRulesLog.RaidEnded(raider.PlayerId, raider.AccountId, session));
            if (session.Outcome == RaidOutcome.Extracted && _loot.IsRunning)
            {
                // 持ち帰った物は持ち物庫に移ったので、場に残さない
                _loot.Stow(raider.PlayerId);
            }

            raider.Allowed.Clear();
            Save(raider);
            SendState(raider);
        }

        private void HandleGiven(int playerId, IReadOnlyList<ItemCount> given)
        {
            if (!_raiders.TryGetValue(playerId, out var raider) || !raider.IsRaiding)
            {
                return;
            }

            foreach (var item in given)
            {
                try
                {
                    raider.Session.Pick(item.ItemId, item.Count);
                    raider.Picked = true;
                }
                catch (ArgumentException e)
                {
                    _host.Log.WriteLine(ExtractionRulesLog.PickRefused(playerId, item.ItemId, e.Message));
                }
            }

            raider.GoalBoxId = null;
            raider.GoalBoxPosition = null;
            SendState(raider);
        }

        private void Save(Raider raider)
        {
            if (!raider.Saves)
            {
                return;
            }

            try
            {
                _store.Save(raider.AccountId, raider.Stash);
                _host.Log.WriteLine(ExtractionRulesLog.Saved(raider.PlayerId, raider.AccountId, _store.PathOf(raider.AccountId), raider.Stash.Stacks.Count));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 保存できなくても試合は続ける。持ち物庫はこの人が居る間はサーバーに残り、次の保存で書き直す
                _host.Log.WriteLine(ExtractionRulesLog.SaveFailed(raider.PlayerId, raider.AccountId, e.Message));
            }
        }

        private void SendState(Raider raider)
        {
            var context = _rules.Context;
            if (context == null || !context.Match.TryGetPlayer(raider.PlayerId, out var player))
            {
                return;
            }

            _state.Clear();
            var stash = raider.Stash.Stacks;
            _merged.Clear();
            _mergedOrder.Clear();
            foreach (var stack in stash)
            {
                if (_merged.TryGetValue(stack.ItemId, out var count))
                {
                    _merged[stack.ItemId] = (int)Math.Min((long)count + stack.Count, int.MaxValue);
                }
                else
                {
                    _merged.Add(stack.ItemId, stack.Count);
                    _mergedOrder.Add(stack.ItemId);
                }
            }

            foreach (var id in _mergedOrder)
            {
                AddTo(_state.Stash, id, _merged[id]);
            }

            var session = raider.Session;
            if (session != null)
            {
                foreach (var item in session.Carried)
                {
                    AddTo(_state.Carried, item.ItemId, item.Count);
                }

                foreach (var item in session.BroughtIn)
                {
                    AddTo(_state.BroughtIn, item.ItemId, item.Count);
                }

                _state.Outcome = ToStashOutcome(session.Outcome);
            }

            context.Messenger.Send(player.Connection, TransportChannel.Reliable, _state);
        }

        // 送れる数に収まらない分は落とす。持ち物庫そのものはディスクに全部残る
        private static void AddTo(StashItemList list, string itemId, int count)
        {
            if (list.Count < StashItemList.MaxCount && count > 0)
            {
                list.Add(itemId, Math.Min(count, ushort.MaxValue));
            }
        }

        internal static StashOutcome ToStashOutcome(RaidOutcome outcome)
        {
            switch (outcome)
            {
                case RaidOutcome.Raiding:
                    return StashOutcome.None;
                case RaidOutcome.Extracted:
                    return StashOutcome.Extracted;
                case RaidOutcome.Died:
                    return StashOutcome.Killed;
                case RaidOutcome.TimedOut:
                    return StashOutcome.TimedOut;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown extraction outcome");
            }
        }

        private sealed class Raider
        {
            public Raider(int playerId, bool isBot, string accountId, bool saves, Stash stash)
            {
                PlayerId = playerId;
                IsBot = isBot;
                AccountId = accountId;
                Saves = saves;
                Stash = stash;
            }

            public int PlayerId { get; }

            public bool IsBot { get; }

            public string AccountId { get; }

            // 読めなかった持ち物庫を新しい中身で上書きしないよう、読めたときだけ書く
            public bool Saves { get; }

            public Stash Stash { get; }

            public ExtractionSession Session { get; set; }

            public bool IsRaiding => Session != null && !Session.IsOver;

            // 持ち込んだ武器の Id。出現で配る武器をこれに絞る
            public HashSet<string> Allowed { get; } = new HashSet<string>(StringComparer.Ordinal);

            public bool Picked { get; set; }

            public int? GoalBoxId { get; set; }

            public Vector3? GoalBoxPosition { get; set; }

            public int? GoalPointIndex { get; set; }
        }
    }
}
