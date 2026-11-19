using System;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が SearchAndDestroy のとき、試合を攻守のラウンドに区切り、点数と勝敗を Core の SearchAndDestroy に決めさせる。
    // Session (TeamDeathmatchSession) は倒れた人を出直させず、キル数でも時間でも試合を終えない形で作る。
    // 設置地点はマップの拠点の範囲 (NetMapPoints.ControlPoints) を使い、番号もそのまま地点の番号にする。
    // 設置・解除の長押しはオフラインの bot と同じく、立っている間ずっと押しているとみなす (入力に長押しのボタンが無いため)
    internal sealed class SearchAndDestroyRules : IMatchObjectiveRules, IEconomyRounds
    {
        // オフラインの SearchAndDestroyRunner.ReachHeight と揃える。Runtime はサーバーから参照できないため
        internal const float ReachHeight = 2f;

        internal const int BombItemId = 0;

        private readonly NetMatchSettings _settings;
        private readonly IMatchObjectiveHost _host;
        private ControlPointMap _sites;
        private SearchAndDestroy _game;
        private bool _started;
        private Vector3 _bombGround;
        private bool _hasBombGround;

        public SearchAndDestroyRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        // マップが替わると Game を作り直すので、経済は Game ではなくこちらの出来事を受ける
        public event Action<int> PreparationStarted;

        public event Action<int> RoundStarted;

        public event Action<Side?> RoundEnded;

        public event Action SidesSwapped;

        public event Action<Side> Ended;

        public MatchObjective Objective => MatchObjective.SearchAndDestroy;

        // UseMap の前は null
        public SearchAndDestroy Game => _game;

        // 地面に落ちている・設置された爆弾の足元。持たれているか、まだ置かれていなければ null
        public Vector3? BombGround => _hasBombGround ? _bombGround : (Vector3?)null;

        public bool PlaysInRounds => true;

        // 試合の点数はラウンドの勝ち数にする
        public bool KeepsMatchScore => true;

        // 合間は撃ち合わせない。撃ち始めの秒を全員でそろえるため
        public bool HoldsFire => _game != null && _game.Phase == RoundPhase.Preparing;

        // 合間は始まるまで、設置の後は起爆まで、それ以外はラウンドの残り
        public float? RemainingSeconds
        {
            get
            {
                if (_game == null)
                {
                    return null;
                }

                switch (_game.Phase)
                {
                    case RoundPhase.Preparing:
                        return _game.PreparationRemaining;
                    case RoundPhase.Live:
                        return IsPlantedOrAfter(_game.Bomb) ? _game.FuseRemaining : _game.RoundRemaining;
                    case RoundPhase.Ended:
                        return 0f;
                    default:
                        return _game.Spec.RoundSeconds;
                }
            }
        }

        // 試合全体を時間とキル数で切らず、ラウンドの勝ち数で区切るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, rules, respawnsByRound: true);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            try
            {
                _ = CreateGame(points, out _);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException("cannot change to map " + map.Name + " for the " + Objective + " match: " + e.Message, e);
            }
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            SearchAndDestroy game;
            ControlPointMap sites;
            try
            {
                game = CreateGame(points, out sites);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match on map " + map.Name + ": " + e.Message, e);
            }

            var started = _started;
            if (started)
            {
                Stop();
            }

            _game = game;
            _sites = sites;
            _hasBombGround = false;
            if (started)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_game == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _game.PreparationStarted += HandlePreparationStarted;
            _game.RoundStarted += HandleRoundStarted;
            _game.RoundEnded += HandleRoundEnded;
            _game.SidesSwapped += HandleSidesSwapped;
            _game.Ended += HandleEnded;
            _game.BombPickedUp += HandleBombPickedUp;
            _game.BombDropped += HandleBombDropped;
            _game.BombPlanted += HandleBombPlanted;
            _game.BombDefused += HandleBombDefused;
            _game.BombDetonated += HandleBombDetonated;
            _host.Log.WriteLine(SearchAndDestroyText.SettingsLog(_game.Spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _game.PreparationStarted -= HandlePreparationStarted;
            _game.RoundStarted -= HandleRoundStarted;
            _game.RoundEnded -= HandleRoundEnded;
            _game.SidesSwapped -= HandleSidesSwapped;
            _game.Ended -= HandleEnded;
            _game.BombPickedUp -= HandleBombPickedUp;
            _game.BombDropped -= HandleBombDropped;
            _game.BombPlanted -= HandleBombPlanted;
            _game.BombDefused -= HandleBombDefused;
            _game.BombDetonated -= HandleBombDetonated;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (_game == null || !matchPlaying)
            {
                return false;
            }

            if (_game.Phase == RoundPhase.Waiting)
            {
                _game.Start();
            }

            if (_game.Phase == RoundPhase.Live && _game.Mode.State == GameModeState.Playing)
            {
                UpdateIntents();
            }

            if (_game.Phase != RoundPhase.Ended)
            {
                _game.Tick(deltaSeconds);
            }

            return _game.Phase == RoundPhase.Ended;
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (_game == null || session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            if (_game.HasPlayer(playerId))
            {
                if (_game.Mode.GetSide(playerId) == side)
                {
                    return;
                }

                _game.RemovePlayer(playerId);
            }

            _game.AddPlayer(playerId, side);
        }

        public void PlayerLeft(int playerId)
        {
            if (_game != null && _game.HasPlayer(playerId))
            {
                _game.RemovePlayer(playerId);
            }
        }

        // 倒した人が試合の顔ぶれにいなければ (抜けた後の弾など)、倒れたことだけを数える
        public void PlayerKilled(int killerId, int victimId)
        {
            if (_game == null || !_game.HasPlayer(victimId) || _game.Phase != RoundPhase.Live)
            {
                return;
            }

            if (_game.HasPlayer(killerId))
            {
                _game.ReportKill(killerId, victimId);
            }
            else
            {
                _game.ReportDeath(victimId);
            }
        }

        public void MatchRestarted()
        {
            _hasBombGround = false;
            _game?.Reset();
            _host.SetMatchScores(0, 0);
        }

        public void MatchEnded()
        {
        }

        // 試合が終わった後もラウンドが Live のまま残りうるので、試合の終わりを Ended として送る
        public RoundPhase? CurrentRoundPhase()
        {
            var session = _host.Session;
            if (session != null && session.State == GameModeState.Ended)
            {
                return RoundPhase.Ended;
            }

            return _game != null ? _game.Phase : RoundPhase.Waiting;
        }

        public Team? DecideWinner(Team? killWinner)
        {
            if (_game == null)
            {
                return killWinner;
            }

            if (_game.Phase == RoundPhase.Ended)
            {
                return Side.ToTeam(_game.Winner);
            }

            return DominationObjective.Leader(_game.GetRoundWins(Side.Alpha), _game.GetRoundWins(Side.Bravo));
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            if (_game == null)
            {
                snapshot.AlphaPoints = 0;
                snapshot.BravoPoints = 0;
                snapshot.HasBomb = false;
                snapshot.Bomb = BombState.Carried;
                snapshot.HasActivePoint = false;
                snapshot.ActivePointIndex = 0;
                snapshot.ObjectiveRemainingSeconds = 0;
                snapshot.RoundNumber = 0;
                snapshot.RoundsToWin = _settings.SearchAndDestroy.WinsToWin;
                snapshot.HasLastRound = false;
                snapshot.LastRoundEnd = default;
                snapshot.HasLastRoundWinner = false;
                snapshot.LastRoundWinner = Team.Alpha;
                snapshot.PlantFraction = 0f;
                snapshot.HasDefuser = false;
                snapshot.DefuserId = 0;
                snapshot.DefuseFraction = 0f;
                return;
            }

            snapshot.AlphaPoints = _game.GetRoundWins(Side.Alpha);
            snapshot.BravoPoints = _game.GetRoundWins(Side.Bravo);
            snapshot.HasBomb = _game.Phase != RoundPhase.Waiting;
            snapshot.Bomb = _game.Bomb;
            var site = ActiveSite();
            snapshot.HasActivePoint = site.HasValue;
            snapshot.ActivePointIndex = site ?? 0;
            snapshot.ObjectiveRemainingSeconds = IsPlantedOrAfter(_game.Bomb) && _game.Phase != RoundPhase.Waiting
                ? (int)Math.Ceiling(Math.Max(0f, _game.FuseRemaining))
                : 0;
            snapshot.RoundNumber = _game.RoundNumber;
            snapshot.RoundsToWin = _game.WinsToWin;
            snapshot.HasLastRound = _game.LastRoundEnd.HasValue;
            snapshot.LastRoundEnd = _game.LastRoundEnd ?? default;
            snapshot.HasLastRoundWinner = _game.LastRoundEnd.HasValue && _game.LastRoundWinner.HasValue;
            snapshot.LastRoundWinner = snapshot.HasLastRoundWinner ? _game.LastRoundWinner.Value.ToTeam() : Team.Alpha;
            snapshot.PlantFraction = _game.PlantingSite.HasValue ? Fraction(_game.PlantFraction) : 0f;
            var defusing = _game.Bomb == BombState.Planted && _game.DefuserId.HasValue;
            snapshot.HasDefuser = defusing;
            snapshot.DefuserId = defusing ? _game.DefuserId.Value : 0;
            snapshot.DefuseFraction = defusing ? Fraction(_game.DefuseFraction) : 0f;
        }

        // 浮動小数の誤差で 1 をわずかに超えても書けるよう、0〜1 に収める
        private static float Fraction(float value)
        {
            return value > 0f ? Math.Min(1f, value) : 0f;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        // 爆弾は 1 つなので Id は固定。持ち主のチームは攻撃側にして、クライアントが攻守を知れるようにする
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            if (_game == null || _game.Phase == RoundPhase.Waiting || !TryBombPosition(out var position))
            {
                return;
            }

            ref var item = ref items.Add();
            item.ItemId = BombItemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Bomb;
            item.Team = _game.Attacker.ToTeam();
            item.HasOwner = false;
            item.OwnerId = 0;
            item.Position = position;
            var carried = _game.Bomb == BombState.Carried && _game.CarrierId.HasValue;
            item.State = carried ? ObjectiveItemState.Carried
                : IsPlantedOrAfter(_game.Bomb) ? ObjectiveItemState.Planted
                : ObjectiveItemState.Dropped;
            item.HasCarrier = carried;
            item.CarrierId = carried ? _game.CarrierId.Value : 0;
        }

        internal static bool IsPlantedOrAfter(BombState bomb)
        {
            return bomb == BombState.Planted || bomb == BombState.Defused || bomb == BombState.Detonated;
        }

        // 地点の番号はスナップショットの拠点の番号 (MatchSnapshot.ActivePointIndex) で送るので、その上限を超えるマップは断る
        private SearchAndDestroy CreateGame(NetMapPoints points, out ControlPointMap sites)
        {
            var areas = points.ControlPoints;
            if (areas.Count == 0)
            {
                throw new InvalidOperationException("it has no control points to use as bomb sites");
            }

            if (areas.Count > SnapshotMessage.MaxControlPoints)
            {
                throw new InvalidOperationException(
                    "it has " + areas.Count.ToString(CultureInfo.InvariantCulture) + " control points but at most "
                    + SnapshotMessage.MaxControlPoints.ToString(CultureInfo.InvariantCulture) + " bomb sites fit in a snapshot");
            }

            var spec = _settings.SearchAndDestroy;
            try
            {
                sites = new ControlPointMap(areas);
                return new SearchAndDestroy(new SearchAndDestroySpec(
                    sites.Count,
                    spec.RoundSeconds,
                    spec.PlantSeconds,
                    spec.DefuseSeconds,
                    spec.FuseSeconds,
                    spec.WinsToWin,
                    spec.PreparationSeconds,
                    spec.SwapEveryRounds,
                    spec.BombPickupRadius,
                    spec.DefuseRadius));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }

        // 設置の後は設置した地点、持っている間は設置している最中の地点
        private int? ActiveSite()
        {
            if (IsPlantedOrAfter(_game.Bomb))
            {
                return _game.PlantedSite;
            }

            return _game.Bomb == BombState.Carried ? _game.PlantingSite : null;
        }

        private bool TryBombPosition(out Vector3 position)
        {
            if (_game.Bomb == BombState.Carried && _game.CarrierId.HasValue
                && _host.Bodies != null && _host.Bodies.TryGet(_game.CarrierId.Value, out var carrier))
            {
                position = carrier.Position;
                return true;
            }

            position = _bombGround;
            return _hasBombGround;
        }

        // 倒れている人とダウン中の人は拾えず、設置も解除もできない
        private void UpdateIntents()
        {
            var session = _host.Session;
            var bodies = _host.Bodies;
            if (session == null || bodies == null)
            {
                return;
            }

            var pickupRadius = _game.Spec.BombPickupRadius;
            var defuseRadius = _game.Spec.DefuseRadius;
            foreach (var body in bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_game.HasPlayer(playerId))
                {
                    continue;
                }

                var standing = session.HasPlayer(playerId) && session.IsAlive(playerId) && !session.IsDowned(playerId);
                if (!standing)
                {
                    _game.SetPlanting(playerId, null);
                    _game.SetDefusing(playerId, false);
                    continue;
                }

                var ground = body.Position;
                if (_game.IsAttacker(playerId))
                {
                    if (_game.Bomb == BombState.Dropped && IsNearBomb(ground, pickupRadius))
                    {
                        _game.PickUpBomb(playerId);
                    }

                    _game.SetPlanting(playerId, _game.CarrierId == playerId ? _sites.Locate(ground) : null);
                    _game.SetDefusing(playerId, false);
                }
                else
                {
                    _game.SetPlanting(playerId, null);
                    _game.SetDefusing(playerId, _game.Bomb == BombState.Planted && IsNearBomb(ground, defuseRadius));
                }
            }
        }

        // 段差の上下で拾ったり解除したりしないよう、上下の差も見る
        private bool IsNearBomb(Vector3 ground, float radius)
        {
            if (!_hasBombGround)
            {
                return false;
            }

            var offset = ground - _bombGround;
            return Math.Abs(offset.Y) <= ReachHeight && (offset.X * offset.X) + (offset.Z * offset.Z) <= radius * radius;
        }

        private bool TryBodyPosition(int playerId, out Vector3 position)
        {
            if (_host.Bodies != null && _host.Bodies.TryGet(playerId, out var body))
            {
                position = body.Position;
                return true;
            }

            position = default;
            return false;
        }

        // 体力と位置は Session が持つので、ここで全員を出直させる。倒れている人の蘇生の受け付けも捨てる
        private void HandlePreparationStarted(int round)
        {
            _host.Log.WriteLine(SearchAndDestroyText.RoundLog(round, _game.Attacker, _game.Defender));
            _hasBombGround = false;
            _host.ClearDowned();
            _host.Session?.RespawnAll();
            PreparationStarted?.Invoke(round);
        }

        private void HandleRoundStarted(int round)
        {
            _host.Log.WriteLine(SearchAndDestroyText.LiveLog(round));
            RoundStarted?.Invoke(round);
        }

        private void HandleRoundEnded(Side? winner, RoundEndReason reason)
        {
            var alpha = _game.GetRoundWins(Side.Alpha);
            var bravo = _game.GetRoundWins(Side.Bravo);
            _host.Log.WriteLine(SearchAndDestroyText.RoundEndLog(_game.RoundNumber, winner, reason, alpha, bravo));
            _host.SetMatchScores(alpha, bravo);
            RoundEnded?.Invoke(winner);
        }

        private void HandleSidesSwapped()
        {
            _host.Log.WriteLine(SearchAndDestroyText.SwapLog(_game.Attacker, _game.Defender));
            SidesSwapped?.Invoke();
        }

        private void HandleEnded(Side winner)
        {
            _host.Log.WriteLine(SearchAndDestroyText.EndedLog(winner));
            Ended?.Invoke(winner);
        }

        private void HandleBombPickedUp(int playerId)
        {
            _host.Log.WriteLine(SearchAndDestroyText.PickUpLog(playerId));
            _hasBombGround = false;
        }

        // 落とした人の体がもう無ければ (抜けた後など)、落ちた場所が分からないので誰も拾えない
        private void HandleBombDropped(int playerId)
        {
            _host.Log.WriteLine(SearchAndDestroyText.DropLog(playerId));
            _hasBombGround = TryBodyPosition(playerId, out _bombGround);
        }

        private void HandleBombPlanted(int playerId, int site)
        {
            _host.Log.WriteLine(SearchAndDestroyText.PlantedLog(playerId, site));
            if (!TryBodyPosition(playerId, out _bombGround))
            {
                _bombGround = _sites[site].Center;
            }

            _hasBombGround = true;
        }

        private void HandleBombDefused(int playerId)
        {
            _host.Log.WriteLine(SearchAndDestroyText.DefusedLog(playerId));
        }

        private void HandleBombDetonated(int site)
        {
            _host.Log.WriteLine(SearchAndDestroyText.DetonatedLog(site));
        }
    }
}
