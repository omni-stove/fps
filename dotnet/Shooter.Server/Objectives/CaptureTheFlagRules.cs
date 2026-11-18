using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が CaptureTheFlag のとき、旗の取り合いと点数・勝敗を Core の CaptureTheFlag に任せる。
    // 体力・キル・出直しは Session (TeamDeathmatchSession) が持ち、CaptureTheFlag の ModeSession には倒れたことを写すだけにする。
    // ModeSession の出直しを 0 秒にするのは、出直しの待ちを Session と 2 か所で数えないため。倒れている間に旗に触れないことは Session の生死で見る
    internal sealed class CaptureTheFlagRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly CaptureTheFlagSpec _configured;
        private readonly CaptureTheFlag _game;
        private readonly Dictionary<Side, Vector3> _bases = new Dictionary<Side, Vector3>();
        private readonly Dictionary<Side, Vector3> _dropPoints = new Dictionary<Side, Vector3>();
        private readonly Dictionary<Side, Vector3> _carriedAt = new Dictionary<Side, Vector3>();
        private bool _started;

        public CaptureTheFlagRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _configured = settings.CaptureTheFlag;
            _game = new CaptureTheFlag(new CaptureTheFlagSpec(
                _configured.ScoreLimit,
                _configured.TimeLimitSeconds,
                0f,
                _configured.PickupRadius,
                _configured.CaptureRadius,
                _configured.ReturnSeconds,
                _configured.PointsPerCapture));
        }

        public MatchObjective Objective => MatchObjective.CaptureTheFlag;

        public CaptureTheFlag Game => _game;

        public bool PlaysInRounds => false;

        // 試合の点数は運び込んだ点にする
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => Math.Max(0f, _game.Session.RemainingSeconds);

        // 点数と時間の上限は CaptureTheFlag の ModeSession が見るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.CaptureTheFlag.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var problem = ResolveBases(points, null);
            if (problem != null)
            {
                throw new InvalidOperationException("cannot change to map " + map.Name + " for the " + Objective + " match: it " + problem);
            }
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var bases = new Dictionary<Side, Vector3>();
            var problem = ResolveBases(points, bases);
            if (problem != null)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: map " + map.Name + " " + problem);
            }

            _bases.Clear();
            foreach (var pair in bases)
            {
                _bases.Add(pair.Key, pair.Value);
            }

            _dropPoints.Clear();
            _carriedAt.Clear();
        }

        public void Start()
        {
            if (_bases.Count == 0)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _game.FlagTaken += HandleFlagTaken;
            _game.FlagDropped += HandleFlagDropped;
            _game.FlagReturned += HandleFlagReturned;
            _game.FlagCaptured += HandleFlagCaptured;
            _host.Log.WriteLine(CaptureTheFlagLog.NetRules(_game.Spec, _configured.RespawnSeconds));
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _game.FlagTaken -= HandleFlagTaken;
            _game.FlagDropped -= HandleFlagDropped;
            _game.FlagReturned -= HandleFlagReturned;
            _game.FlagCaptured -= HandleFlagCaptured;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying || _bases.Count == 0)
            {
                return false;
            }

            var mode = _game.Session;
            if (mode.State == GameModeState.Waiting)
            {
                mode.Start();
            }

            if (mode.State == GameModeState.Playing)
            {
                TouchFlags();
            }

            if (mode.State == GameModeState.Playing)
            {
                mode.Tick(deltaSeconds);
            }

            RememberCarriers();
            return mode.State == GameModeState.Ended;
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            var mode = _game.Session;
            if (mode.HasPlayer(playerId))
            {
                if (mode.GetSide(playerId) == side)
                {
                    return;
                }

                _game.RemovePlayer(playerId);
            }

            mode.AddPlayer(playerId, side);
        }

        public void PlayerLeft(int playerId)
        {
            if (_game.Session.HasPlayer(playerId))
            {
                _game.RemovePlayer(playerId);
            }
        }

        // 倒れたら運んでいた旗をその場に落とす。倒した人が抜けた後なら、倒されたことだけを数える
        public void PlayerKilled(int killerId, int victimId)
        {
            var mode = _game.Session;
            if (!mode.HasPlayer(victimId))
            {
                return;
            }

            if (killerId != victimId && mode.HasPlayer(killerId))
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
            _game.Reset();
            _dropPoints.Clear();
            _carriedAt.Clear();
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        public void MatchEnded()
        {
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner)
        {
            var mode = _game.Session;
            if (mode.State == GameModeState.Ended)
            {
                return Side.ToTeam(mode.Winner);
            }

            return DominationObjective.Leader((float)mode.GetScore(Side.Alpha), (float)mode.GetScore(Side.Bravo));
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = ScoreOf(Side.Alpha);
            snapshot.BravoPoints = ScoreOf(Side.Bravo);
            snapshot.ObjectiveRemainingSeconds = SoonestReturnSeconds();
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // 旗の Id は陣営の番号にする。クライアントは Id ではなく種類と陣営で旗を見分ける
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            if (_bases.Count == 0)
            {
                return;
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var state = _game.GetFlagState(side);
                var carrier = _game.GetCarrierId(side);
                ref var item = ref items.Add();
                item.ItemId = side.Id;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.Flag;
                item.Team = side.ToTeam();
                item.HasOwner = false;
                item.OwnerId = 0;
                item.Position = FlagPosition(side);
                item.State = ToItemState(state);
                item.HasCarrier = state == FlagState.Carried && carrier.HasValue;
                item.CarrierId = item.HasCarrier ? carrier.Value : 0;
            }
        }

        // 台の上では旗の居場所が台そのものなので、ずれて送らないよう台の位置をそのまま使う
        internal Vector3 FlagPosition(Side side)
        {
            var home = _bases[side];
            switch (_game.GetFlagState(side))
            {
                case FlagState.Carried:
                    var carrier = _game.GetCarrierId(side);
                    if (carrier.HasValue && _host.Bodies != null && _host.Bodies.TryGet(carrier.Value, out var body))
                    {
                        return body.Position;
                    }

                    return _carriedAt.TryGetValue(side, out var carried) ? carried : home;
                case FlagState.Dropped:
                    return _dropPoints.TryGetValue(side, out var dropped) ? dropped : home;
                default:
                    return home;
            }
        }

        internal static ObjectiveItemState ToItemState(FlagState state)
        {
            switch (state)
            {
                case FlagState.AtBase:
                    return ObjectiveItemState.Home;
                case FlagState.Carried:
                    return ObjectiveItemState.Carried;
                case FlagState.Dropped:
                    return ObjectiveItemState.Dropped;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "state is not a defined FlagState");
            }
        }

        // 陣営ごとに台が 1 つずつ無ければ遊べない。bases が null なら確かめるだけにする
        private static string ResolveBases(NetMapPoints points, Dictionary<Side, Vector3> bases)
        {
            var found = new HashSet<Team>();
            foreach (var flagBase in points.FlagBases)
            {
                if (!found.Add(flagBase.Team))
                {
                    return "has more than one flag base for team " + flagBase.Team;
                }

                bases?.Add(Side.FromTeam(flagBase.Team), flagBase.Position);
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var team = side.ToTeam();
                if (!found.Contains(team))
                {
                    return "has no flag base for team " + team;
                }
            }

            return null;
        }

        // 倒れている人とダウン中の人は旗に触れない。同じ Tick に 2 人が届いたら、Id の小さい人が先に触れる
        private void TouchFlags()
        {
            var session = _host.Session;
            var bodies = _host.Bodies;
            if (session == null || bodies == null)
            {
                return;
            }

            var spec = _game.Spec;
            var mode = _game.Session;
            foreach (var playerId in mode.GetPlayerIds())
            {
                // 前の人が旗を運び込んで試合が終わったら、残りの人はもう触れない
                if (mode.State != GameModeState.Playing)
                {
                    return;
                }

                if (!session.HasPlayer(playerId) || !session.IsAlive(playerId) || session.IsDowned(playerId)
                    || !bodies.TryGet(playerId, out var body) || !body.HasSpawned)
                {
                    continue;
                }

                var ground = body.Position;
                foreach (var side in CaptureTheFlag.FlagSides)
                {
                    if (_game.GetFlagState(side) != FlagState.Carried
                        && CaptureTheFlag.IsWithin(ground, FlagPosition(side), spec.PickupRadius))
                    {
                        _game.Touch(side, playerId);
                    }
                }

                if (_game.IsCarrying(playerId)
                    && CaptureTheFlag.IsWithin(ground, _bases[mode.GetSide(playerId)], spec.CaptureRadius))
                {
                    _game.ReachBase(playerId);
                }
            }
        }

        // 運ぶ人の体が先に消えても、落とした場所を最後に運ばれていた場所にするため覚えておく
        private void RememberCarriers()
        {
            var bodies = _host.Bodies;
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var carrier = _game.GetCarrierId(side);
                if (_game.GetFlagState(side) == FlagState.Carried && carrier.HasValue
                    && bodies != null && bodies.TryGet(carrier.Value, out var body))
                {
                    _carriedAt[side] = body.Position;
                }
            }
        }

        private int ScoreOf(Side side)
        {
            var score = _game.Session.GetScore(side);
            return score > 0d ? (int)Math.Min(int.MaxValue, Math.Floor(score)) : 0;
        }

        // 落ちた旗はどれも同じ秒数で戻るので、先に落ちた旗ほど先に戻る。両方落ちているときは先に戻る方を送る
        private int SoonestReturnSeconds()
        {
            float? soonest = null;
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                var remaining = _game.GetReturnRemaining(side);
                if (remaining.HasValue && (!soonest.HasValue || remaining.Value < soonest.Value))
                {
                    soonest = remaining.Value;
                }
            }

            return soonest.HasValue ? HudFormat.DisplayedSeconds(soonest.Value) : 0;
        }

        private void HandleFlagTaken(Side flag, int playerId)
        {
            _host.Log.WriteLine(CaptureTheFlagLog.Taken(flag, playerId));
            _dropPoints.Remove(flag);
            if (_host.Bodies != null && _host.Bodies.TryGet(playerId, out var body))
            {
                _carriedAt[flag] = body.Position;
            }

            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        private void HandleFlagDropped(Side flag, int playerId)
        {
            _host.Log.WriteLine(CaptureTheFlagLog.Dropped(flag, playerId));
            if (_host.Bodies != null && _host.Bodies.TryGet(playerId, out var body))
            {
                _dropPoints[flag] = body.Position;
            }
            else
            {
                _dropPoints[flag] = _carriedAt.TryGetValue(flag, out var carried) ? carried : _bases[flag];
            }

            _carriedAt.Remove(flag);
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        private void HandleFlagReturned(Side flag, int? playerId)
        {
            _host.Log.WriteLine(CaptureTheFlagLog.Returned(flag, playerId));
            _dropPoints.Remove(flag);
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }

        private void HandleFlagCaptured(Side flag, int playerId)
        {
            var mode = _game.Session;
            var side = mode.GetSide(playerId);
            _host.Log.WriteLine(CaptureTheFlagLog.Captured(flag, playerId, side, mode.GetScore(side)));
            _dropPoints.Remove(flag);
            _carriedAt.Remove(flag);
            _host.SetMatchScores(ScoreOf(Side.Alpha), ScoreOf(Side.Bravo));
            _host.Log.WriteLine(CaptureTheFlagLog.Flags(_game));
        }
    }
}
