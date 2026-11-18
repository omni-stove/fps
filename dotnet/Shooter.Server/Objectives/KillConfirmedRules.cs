using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が KillConfirmed のとき、倒れた人の足元にタグを落とし、拾った側で点と勝敗を Core の KillConfirmed に決めさせる。
    // Session (TeamDeathmatchSession) は体力・キル・出直しだけを受け持ち、キル数と時間では終わらせない。
    // 試合の点数 (ServerMatch) とスナップショットの点数は確定の点にし、目的の欄 (AlphaPoints/BravoPoints) には確定の数を載せる
    internal sealed class KillConfirmedRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly KillConfirmed _killConfirmed;
        private readonly Dictionary<int, Vector3> _tagPositions = new Dictionary<int, Vector3>();
        private readonly List<KillConfirmedTag> _scan = new List<KillConfirmedTag>();
        private readonly List<int> _stale = new List<int>();
        private bool _started;

        public KillConfirmedRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _killConfirmed = new KillConfirmed(settings.KillConfirmed);
        }

        public MatchObjective Objective => MatchObjective.KillConfirmed;

        public KillConfirmed KillConfirmed => _killConfirmed;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => Math.Max(0f, _killConfirmed.Session.RemainingSeconds);

        // 終わり方は KillConfirmed の点数の上限と制限時間で決めるので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, _killConfirmed.Spec.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
        }

        // タグは倒れた場所に落ちるので、マップから読むものは無い。替わる前のマップに落ちたタグは置き場所が意味を失うので捨てる
        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            foreach (var tagId in new List<int>(_tagPositions.Keys))
            {
                _killConfirmed.Discard(tagId);
            }

            _tagPositions.Clear();
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _killConfirmed.TagExpired += HandleTagExpired;
            _host.Log.WriteLine(ServerKillConfirmedLog.Settings(_killConfirmed.Spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _killConfirmed.TagExpired -= HandleTagExpired;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            var session = _killConfirmed.Session;
            if (session.State == GameModeState.Waiting)
            {
                session.Start();
            }

            if (session.State == GameModeState.Playing)
            {
                CollectTags();
            }

            // 拾って点数の上限に届いたら、Collect の中で終わっている
            if (session.State == GameModeState.Playing)
            {
                session.Tick(deltaSeconds);
            }

            ForgetGoneTags();
            return session.State == GameModeState.Ended;
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            var mode = _killConfirmed.Session;
            if (mode.HasPlayer(playerId))
            {
                if (mode.GetSide(playerId) == side)
                {
                    return;
                }

                mode.RemovePlayer(playerId);
            }

            mode.AddPlayer(playerId, side);
        }

        // 抜けた人のタグは場に残す。拾えば持ち主がいなくても確定・阻止になる
        public void PlayerLeft(int playerId)
        {
            var mode = _killConfirmed.Session;
            if (mode.HasPlayer(playerId))
            {
                mode.RemovePlayer(playerId);
            }
        }

        // 自滅と味方撃ちでもタグを落とすのは、オフラインの KillConfirmedRunner が倒れ方を問わず落とすのに合わせるため
        public void PlayerKilled(int killerId, int victimId)
        {
            if (!_killConfirmed.Session.HasPlayer(victimId) || !_killConfirmed.CanDropTag(victimId))
            {
                return;
            }

            if (!_host.Bodies.TryGet(victimId, out var body))
            {
                _host.Log.WriteLine(ServerKillConfirmedLog.NoBody(victimId));
                return;
            }

            var tag = _killConfirmed.DropTag(victimId);
            if (tag == null)
            {
                return;
            }

            _tagPositions[tag.Id] = body.Position;
            _host.Log.WriteLine(KillConfirmedText.DropLog(tag));
        }

        public void MatchRestarted()
        {
            _killConfirmed.Reset();
            _tagPositions.Clear();
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(ServerKillConfirmedLog.Restart());
        }

        public void MatchEnded()
        {
            _host.Log.WriteLine(ServerKillConfirmedLog.State(_killConfirmed));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner)
        {
            var session = _killConfirmed.Session;
            if (session.State == GameModeState.Ended)
            {
                return Side.ToTeam(session.Winner);
            }

            var alpha = session.GetScore(Side.Alpha);
            var bravo = session.GetScore(Side.Bravo);
            if (alpha > bravo)
            {
                return Team.Alpha;
            }

            return bravo > alpha ? Team.Bravo : (Team?)null;
        }

        // 試合の点数は Session のキル数でなく確定の点にする。HUD とスコアボードが勝敗を決める点を見せるため
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            var session = _killConfirmed.Session;
            snapshot.Objective = Objective;
            snapshot.AlphaScore = ScoreOf(session.GetScore(Side.Alpha));
            snapshot.BravoScore = ScoreOf(session.GetScore(Side.Bravo));
            snapshot.AlphaPoints = _killConfirmed.GetConfirmed(Side.Alpha);
            snapshot.BravoPoints = _killConfirmed.GetConfirmed(Side.Bravo);
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        // 並びの上限を超えた分は載せずに落とす。古い順に載せるので、落ちるのは後から落ちたタグで、点数の判定はサーバーに残る
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            foreach (var tag in _killConfirmed.Tags)
            {
                if (items.Count == items.MaxCount)
                {
                    break;
                }

                if (!_tagPositions.TryGetValue(tag.Id, out var position))
                {
                    continue;
                }

                ref var item = ref items.Add();
                item.ItemId = tag.Id;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.Tag;
                item.Team = tag.OwnerSide.ToTeam();
                item.HasOwner = true;
                item.OwnerId = tag.OwnerId;
                item.Position = position;
                item.State = ObjectiveItemState.Dropped;
                item.HasCarrier = false;
                item.CarrierId = 0;
            }
        }

        // 点は確定ごとの整数だが Core が小数で持つので、表示と同じく切り捨てる
        internal static int ScoreOf(double score)
        {
            return score > 0d ? (int)Math.Min(int.MaxValue, Math.Floor(score)) : 0;
        }

        // オフラインのタグはトリガーの球と体の重なりで拾うので、ここもタグの半径と体のカプセルの重なりで見る
        internal static bool Reaches(ServerBody body, Vector3 tagPosition, float pickupRadius)
        {
            ArgumentNullException.ThrowIfNull(body);
            body.Capsule(out var bottom, out var top);
            var reach = pickupRadius + body.Radius;
            return DistanceSquaredToSegment(tagPosition, bottom, top) <= reach * reach;
        }

        private static float DistanceSquaredToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            var lengthSquared = segment.LengthSquared();
            var t = lengthSquared > 0f ? Math.Clamp(Vector3.Dot(point - start, segment) / lengthSquared, 0f, 1f) : 0f;
            return Vector3.DistanceSquared(point, start + segment * t);
        }

        // 同じタグに何人も届いていたら、いちばん近い人 (同じ近さなら Id の小さい人) が拾う。届いた順を持たないサーバーでも結果を 1 つに決めるため
        private void CollectTags()
        {
            if (_killConfirmed.Tags.Count == 0)
            {
                return;
            }

            var session = _host.Session;
            if (session == null)
            {
                return;
            }

            var radius = _killConfirmed.Spec.TagPickupRadius;
            _scan.Clear();
            _scan.AddRange(_killConfirmed.Tags);
            foreach (var tag in _scan)
            {
                if (_killConfirmed.Session.State != GameModeState.Playing)
                {
                    break;
                }

                if (!_tagPositions.TryGetValue(tag.Id, out var position))
                {
                    continue;
                }

                var collectorId = FindCollector(session, position, radius);
                if (!collectorId.HasValue)
                {
                    continue;
                }

                var collectorSide = _killConfirmed.Session.GetSide(collectorId.Value);
                var result = _killConfirmed.Collect(tag.Id, collectorId.Value);
                _host.Log.WriteLine(KillConfirmedText.CollectLog(tag, collectorId.Value, collectorSide, result));
                if (result == KillConfirmedResult.Confirmed)
                {
                    var mode = _killConfirmed.Session;
                    var alpha = ScoreOf(mode.GetScore(Side.Alpha));
                    var bravo = ScoreOf(mode.GetScore(Side.Bravo));
                    _host.SetMatchScores(alpha, bravo);
                    _host.Log.WriteLine(MatchLog.Score(alpha, bravo));
                }
            }

            _scan.Clear();
        }

        private int? FindCollector(TeamDeathmatchSession session, Vector3 position, float radius)
        {
            int? best = null;
            var bestDistance = float.MaxValue;
            foreach (var body in _host.Bodies.All)
            {
                var playerId = body.PlayerId;
                if (!body.HasSpawned
                    || !_killConfirmed.Session.HasPlayer(playerId)
                    || !session.HasPlayer(playerId)
                    || !session.IsAlive(playerId)
                    || session.IsDowned(playerId)
                    || !Reaches(body, position, radius))
                {
                    continue;
                }

                var distance = Vector3.DistanceSquared(body.Position, position);
                if (!best.HasValue || distance < bestDistance || (distance == bestDistance && playerId < best.Value))
                {
                    best = playerId;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // Core は試合の終わりと Reset でタグを知らせずに消すので、置き場所もここで合わせて捨てる
        private void ForgetGoneTags()
        {
            if (_tagPositions.Count == _killConfirmed.Tags.Count)
            {
                return;
            }

            _stale.Clear();
            foreach (var tagId in _tagPositions.Keys)
            {
                if (_killConfirmed.FindTag(tagId) == null)
                {
                    _stale.Add(tagId);
                }
            }

            foreach (var tagId in _stale)
            {
                _tagPositions.Remove(tagId);
            }

            _stale.Clear();
        }

        private void HandleTagExpired(KillConfirmedTag tag)
        {
            _tagPositions.Remove(tag.Id);
            _host.Log.WriteLine(KillConfirmedText.ExpireLog(tag));
        }
    }
}
