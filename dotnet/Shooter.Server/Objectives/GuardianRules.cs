using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Guardian のとき。オフラインの GuardianMode と同じく、試合の始まりに各チームから 1 人ずつリーダーを選び、
    // リーダーが倒れたチームはもう出直さない。出直さないチームが全員倒れたら相手の勝ち、時間切れではリーダーが片方だけ立っていればそのチームの勝ち、
    // どちらでもなければ Session がキル数で決めた勝者のまま。
    // Session (TeamDeathmatchSession) は自分では出直させない形 (respawnsByRound) で作り、出直しの待ちはここで数える。
    // チームごとに出直しを止める口が Session に無いため
    internal sealed class GuardianRules : IMatchObjectiveRules
    {
        private static readonly Team[] _teams = { Team.Alpha, Team.Bravo };

        private readonly IMatchObjectiveHost _host;
        private readonly float _respawnSeconds;
        private readonly float _timeLimitSeconds;
        private readonly Func<int, int> _pick;
        private readonly Dictionary<Team, int> _leaders = new Dictionary<Team, int>();
        private readonly HashSet<Team> _fallen = new HashSet<Team>();
        private readonly Dictionary<Team, Vector3> _fellAt = new Dictionary<Team, Vector3>();

        // playerId の昇順に回して、同じ入力なら同じ順に出直させる
        private readonly SortedDictionary<int, float> _waiting = new SortedDictionary<int, float>();
        private readonly List<int> _ready = new List<int>();
        private bool _started;
        private bool _leadersChosen;
        private bool _decided;
        private Team? _decidedWinner;

        public GuardianRules(NetMatchSettings settings, IMatchObjectiveHost host, int? seed = null)
            : this(settings, host, Picker(seed))
        {
        }

        // pick は人数を受け取って選ぶ人の添字を返す。テストで選ぶ人を決めるための口
        internal GuardianRules(NetMatchSettings settings, IMatchObjectiveHost host, Func<int, int> pick)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _pick = pick ?? throw new ArgumentNullException(nameof(pick));
            _respawnSeconds = settings.RespawnSeconds;
            _timeLimitSeconds = settings.TimeLimitSeconds;
        }

        public MatchObjective Objective => MatchObjective.Guardian;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => null;

        public bool LeadersChosen => _leadersChosen;

        public int? LeaderOf(Team team)
        {
            return _leaders.TryGetValue(team, out var playerId) ? playerId : (int?)null;
        }

        public bool HasLeaderFallen(Team team) => _fallen.Contains(team);

        public float? RespawnRemaining(int playerId)
        {
            return _waiting.TryGetValue(playerId, out var remaining) ? Math.Max(0f, remaining) : (float?)null;
        }

        // 点数の上限は設けず (オフラインと同じくキルの点で終わらない)、時間は Session に数えさせて時間切れの勝者を DecideWinner で決め直す
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, settings.TimeLimitSeconds, settings.RespawnSeconds, rules, respawnsByRound: true);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _host.Log.WriteLine(GuardianRulesLog.Started(_respawnSeconds, _timeLimitSeconds));
        }

        public void Stop()
        {
            _started = false;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            var session = _host.Session;
            if (!matchPlaying || session == null || _decided)
            {
                return false;
            }

            if (!_leadersChosen)
            {
                ChooseLeaders(session);
            }

            TickRespawns(session, deltaSeconds);
            return CheckEliminated(session);
        }

        public void PlayerJoined(int playerId)
        {
        }

        public void PlayerLeft(int playerId)
        {
            _waiting.Remove(playerId);
            foreach (var team in _teams)
            {
                if (_leaders.TryGetValue(team, out var leader) && leader == playerId && !_fallen.Contains(team))
                {
                    Fall(team, playerId, GuardianRulesLog.Left);
                    return;
                }
            }
        }

        public void PlayerKilled(int killerId, int victimId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(victimId))
            {
                return;
            }

            var team = session.GetTeam(victimId);
            if (_leaders.TryGetValue(team, out var leader) && leader == victimId && !_fallen.Contains(team))
            {
                Fall(team, victimId, GuardianRulesLog.Killed);
                return;
            }

            if (!_fallen.Contains(team) && session.State == GameModeState.Playing)
            {
                _waiting[victimId] = _respawnSeconds;
            }
        }

        public void MatchRestarted()
        {
            _leaders.Clear();
            _fallen.Clear();
            _fellAt.Clear();
            _waiting.Clear();
            _leadersChosen = false;
            _decided = false;
            _decidedWinner = null;
            _host.Log.WriteLine(GuardianRulesLog.Restart());
        }

        // オフラインの GuardianRespawn.CancelAll と同じく、リーダーと倒れたかどうかは結果の表示のために残す
        public void MatchEnded()
        {
            _waiting.Clear();
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // スナップショットを送るたびにも呼ばれるので、同じ状態なら同じ勝者を返す
        public Team? DecideWinner(Team? killWinner)
        {
            if (_decided)
            {
                return _decidedWinner;
            }

            // オフラインの LeaderStandingWin と同じく、両チームにリーダーがいたときだけ、立っている方を勝ちにする
            if (_leaders.Count == _teams.Length)
            {
                Team? standing = null;
                var standingCount = 0;
                foreach (var team in _teams)
                {
                    if (!_fallen.Contains(team))
                    {
                        standing = team;
                        standingCount++;
                    }
                }

                if (standingCount == 1)
                {
                    return standing;
                }
            }

            return killWinner;
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = 0;
            snapshot.BravoPoints = 0;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            foreach (var team in _teams)
            {
                if (!_leaders.TryGetValue(team, out var leader))
                {
                    continue;
                }

                var fallen = _fallen.Contains(team);
                ref var item = ref items.Add();
                item.ItemId = Side.FromTeam(team).Id;
                item.Present = ObjectiveItemFields.All;
                item.Kind = ObjectiveItemKind.Leader;
                item.Team = team;
                item.HasOwner = true;
                item.OwnerId = leader;
                item.Position = LeaderPosition(team, leader, fallen);
                item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
                item.HasCarrier = !fallen;
                item.CarrierId = fallen ? 0 : leader;
                item.Opened = false;
                item.Radius = 0f;
            }
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        private static Func<int, int> Picker(int? seed)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            return count => random.Next(count);
        }

        // オフラインの GuardianRespawn.ChooseLeaders と同じく、チームの人を playerId の昇順に並べて pick で選ぶ
        private void ChooseLeaders(TeamDeathmatchSession session)
        {
            _leadersChosen = true;
            var ids = session.GetPlayerIds();
            var members = new List<int>();
            foreach (var team in _teams)
            {
                members.Clear();
                foreach (var playerId in ids)
                {
                    if (session.GetTeam(playerId) == team)
                    {
                        members.Add(playerId);
                    }
                }

                if (members.Count == 0)
                {
                    continue;
                }

                var index = _pick(members.Count);
                if (index < 0 || index >= members.Count)
                {
                    throw new InvalidOperationException("pick returned " + index + " for " + members.Count + " member(s) of " + team);
                }

                var leader = members[index];
                _leaders[team] = leader;
                _host.Log.WriteLine(GuardianRulesLog.LeaderChosen(team, leader));

                // 選ぶ前の Tick で倒れていたら、選んだ時点で倒れたことにする (オフラインの SetLeader と同じ)
                if (!session.IsAlive(leader))
                {
                    Fall(team, leader, GuardianRulesLog.Killed);
                }
            }
        }

        private void TickRespawns(TeamDeathmatchSession session, float deltaSeconds)
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            _ready.Clear();
            var keys = new List<int>(_waiting.Keys);
            foreach (var playerId in keys)
            {
                var remaining = _waiting[playerId] - deltaSeconds;
                if (remaining > 0f)
                {
                    _waiting[playerId] = remaining;
                }
                else
                {
                    _ready.Add(playerId);
                }
            }

            foreach (var playerId in _ready)
            {
                _waiting.Remove(playerId);
                if (session.HasPlayer(playerId) && !_fallen.Contains(session.GetTeam(playerId)))
                {
                    session.RespawnPlayer(playerId);
                }
            }

            _ready.Clear();
        }

        // ダウン中の人も立っている側に数えるのは、起こされればまだ戦えるから (Session.IsAlive と同じ)
        private bool CheckEliminated(TeamDeathmatchSession session)
        {
            if (_fallen.Count == 0)
            {
                return false;
            }

            var outCount = 0;
            Team? winner = null;
            foreach (var team in _teams)
            {
                if (_fallen.Contains(team) && !HasStanding(session, team))
                {
                    outCount++;
                }
                else
                {
                    winner = team;
                }
            }

            if (outCount == 0)
            {
                return false;
            }

            _decided = true;
            _decidedWinner = outCount == _teams.Length ? null : winner;
            _host.Log.WriteLine(GuardianRulesLog.Eliminated(_decidedWinner));
            return true;
        }

        private static bool HasStanding(TeamDeathmatchSession session, Team team)
        {
            foreach (var playerId in session.GetPlayerIds())
            {
                if (session.GetTeam(playerId) == team && session.IsAlive(playerId))
                {
                    return true;
                }
            }

            return false;
        }

        private void Fall(Team team, int leader, string reason)
        {
            _fallen.Add(team);
            _fellAt[team] = BodyPosition(leader) ?? Vector3.Zero;

            var session = _host.Session;
            if (session != null)
            {
                var keys = new List<int>(_waiting.Keys);
                foreach (var playerId in keys)
                {
                    if (!session.HasPlayer(playerId) || session.GetTeam(playerId) == team)
                    {
                        _waiting.Remove(playerId);
                    }
                }
            }

            _host.Log.WriteLine(GuardianRulesLog.LeaderFell(team, leader, reason));
        }

        private Vector3 LeaderPosition(Team team, int leader, bool fallen)
        {
            if (fallen)
            {
                return _fellAt.TryGetValue(team, out var at) ? at : Vector3.Zero;
            }

            return BodyPosition(leader) ?? Vector3.Zero;
        }

        private Vector3? BodyPosition(int playerId)
        {
            return _host.Bodies != null && _host.Bodies.TryGet(playerId, out var body) ? body.Position : (Vector3?)null;
        }
    }
}
