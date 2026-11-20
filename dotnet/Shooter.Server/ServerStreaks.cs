using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 倒した数はオフラインの StreakController と同じく Core の StreakTracker で数え、報酬を使うかと効き目はサーバーが決める。
    // UAV は使った人の陣営にだけ敵を追う印 (PingPlaced) を送り、空爆は選ばれた地点と向きの線に ServerCombat.Explode で爆発させる (味方撃ち・キルの数え方は撃った弾と同じ)。
    internal sealed class ServerStreaks : IServerModule, ISnapshotContributor
    {
        public const string LogTag = ServerStreakLog.LogTag;

        // 印の番号は MatchRulesModule の TeamPingRelay が 1 から振るので、クライアントの同じ SharedPingMarkers で取り違えないよう離れた所から振る
        internal const int FirstMarkId = 1 << 30;

        // 断った理由のログを出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _refusalLogInterval = 1f;

        // 送る側の地点は自分の位置の予測から測るので、サーバーの位置との少しのずれでは断らない
        private const float _rangeTolerance = 2f;

        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly Random _random;
        private readonly Dictionary<int, StreakTracker> _trackers = new Dictionary<int, StreakTracker>();
        private readonly List<UavMark> _marks = new List<UavMark>();
        private readonly List<Shell> _shells = new List<Shell>();
        private readonly List<Shell> _dueShells = new List<Shell>();
        private readonly PingPlacedMessage _placed = new PingPlacedMessage();
        private readonly PingMovedMessage _moved = new PingMovedMessage();
        private readonly PingRemovedMessage _removed = new PingRemovedMessage();
        private readonly StreakActivatedMessage _activated = new StreakActivatedMessage();

        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private Streaks _rule = Streaks.Off;
        private float _now;
        private float _nextTrackTime;
        private float _nextRefusalLogTime = float.NegativeInfinity;
        private int _nextMarkId = FirstMarkId;

        // 調整値は Unity と同じ streaks の JSON から読んだものを受け取る。渡されなければ Core の既定で動かす
        public ServerStreaks(MatchRulesModule rules, ServerCombat combat, StreakSpec spec = null, Random random = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Spec = spec ?? StreakSpec.Default;
            _random = random ?? new Random();
        }

        public string Name => "streaks";

        public StreakSpec Spec { get; }

        public bool IsEnabled => _rule == Streaks.On;

        internal int MarkCount => _marks.Count;

        internal int PendingShellCount => _shells.Count;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            var data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            _rule = data.Rules.Streaks;

            _match.PlayerJoined += HandlePlayerJoined;
            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            _match.TeamChanged += HandleTeamChanged;
            _rules.MatchStarted += HandleMatchStarted;
            _rules.RulesChanged += HandleRulesChanged;
            _combat.PlayerKilled += HandlePlayerKilled;

            foreach (var player in _match.Players)
            {
                GetOrAdd(player.PlayerId);
            }

            _log.WriteLine(ServerStreakLog.Rule(_rule, Spec));
            _log.WriteLine(StreakLog.Configured("server", Spec));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _now += deltaSeconds;
            }

            FireDueShells();
            TickMarks();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (message is StreakUseRequestMessage request && _match.TryGetPlayer(connection, out var player))
            {
                Use(player.PlayerId, request.RewardId, request.HasTarget ? request.Target : (Vector3?)null, request.HeadingDegrees);
            }
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerJoined -= HandlePlayerJoined;
            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            _match.TeamChanged -= HandleTeamChanged;
            _rules.MatchStarted -= HandleMatchStarted;
            _rules.RulesChanged -= HandleRulesChanged;
            _combat.PlayerKilled -= HandlePlayerKilled;
            _trackers.Clear();
            _marks.Clear();
            _shells.Clear();
        }

        // 貯めた数は続く MatchStarted で試合ごとに戻るので、ここでは使えるかどうかだけを替える
        private void HandleRulesChanged(ServerRulesData data)
        {
            _rule = data.Rules.Streaks;
            _log.WriteLine(ServerStreakLog.Rule(_rule, Spec));
        }

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (_trackers.TryGetValue(row.PlayerId, out var tracker))
                {
                    row.StreakKills = tracker.Kills;
                }
            }
        }

        internal StreakStatus? StatusOf(int playerId)
        {
            return _trackers.TryGetValue(playerId, out var tracker) ? tracker.Status : (StreakStatus?)null;
        }

        // どの報酬を使うかはクライアントが選ぶ (HUD で選び替えられる) ので、送られた報酬を持っていれば先頭でなくても使わせる。
        // 送られた報酬を持っていなければ、別の報酬に読み替えずに断る。古い表示のまま押したときに、見ていたのと違う報酬を使わせないため
        internal ServerStreakRefusal Use(int playerId, string rewardId, Vector3? target, float headingDegrees)
        {
            if (!_trackers.TryGetValue(playerId, out var tracker))
            {
                return ServerStreakRefusal.NotInMatch;
            }

            // 報酬の Id はクライアントが送った文字なので、この試合の報酬の表に無い Id はログの行を崩さないよう載せない
            var known = Spec.FindReward(rewardId)?.Id;
            if (!IsEnabled)
            {
                return Refuse(playerId, known, ServerStreakRefusal.RuleOff);
            }

            if (known == null)
            {
                return Refuse(playerId, null, ServerStreakRefusal.UnknownReward);
            }

            if (!_rules.CanFire(playerId) || !_bodies.TryGet(playerId, out var body))
            {
                return Refuse(playerId, rewardId, ServerStreakRefusal.CannotAct);
            }

            var ready = tracker.ReadyRewardIds;
            if (ready.Count == 0)
            {
                return Refuse(playerId, rewardId, ServerStreakRefusal.NothingReady);
            }

            if (!tracker.IsReady(rewardId))
            {
                return Refuse(playerId, rewardId, ServerStreakRefusal.NotReady);
            }

            // 狙えないまま報酬を取り去らないよう、使うと決める前に地点を確かめる
            var isAirstrike = string.Equals(rewardId, StreakSpec.AirstrikeRewardId, StringComparison.Ordinal);
            if (isAirstrike && !IsUsableTarget(target, headingDegrees))
            {
                return Refuse(playerId, rewardId, ServerStreakRefusal.InvalidAim);
            }

            if (isAirstrike && !AirstrikeLine.IsWithinRange(body.Position, target.Value, Spec.Airstrike.Range + _rangeTolerance))
            {
                return Refuse(playerId, rewardId, ServerStreakRefusal.OutOfRange);
            }

            var status = tracker.Use(rewardId, out _);
            _log.WriteLine(StreakLog.Used(ServerStreakLog.Id(playerId), rewardId, status));
            if (isAirstrike)
            {
                CallAirstrike(playerId, target.Value, headingDegrees, body.Position.Y);
            }
            else
            {
                ActivateUav(playerId, body);
            }

            return ServerStreakRefusal.None;
        }

        private void ActivateUav(int playerId, ServerBody user)
        {
            var session = _rules.Session;
            var team = session.GetTeam(playerId);
            var uav = Spec.Uav;
            var marked = 0;
            foreach (var other in _bodies.All)
            {
                var id = other.PlayerId;
                if (!session.HasPlayer(id) || !session.IsAlive(id) || session.GetTeam(id) == team
                    || Vector3.Distance(user.Position, other.Position) > uav.Range)
                {
                    continue;
                }

                var mark = new UavMark(NextMarkId(), team, id, _now + uav.DurationSeconds);
                _marks.Add(mark);
                FillPlaced(mark, other.Center);
                SendToTeam(team, TransportChannel.Reliable, _placed);
                marked++;
            }

            Announce(playerId, team, StreakSpec.UavRewardId, null, uav.DurationSeconds);
            _log.WriteLine(ServerStreakLog.UavMarked(playerId, team, marked, CountTeam(team), uav.DurationSeconds));
        }

        // 送られた高さは手元の世界で探したものなので、中心も弾もサーバーの世界で真上から落として決め直す
        private void CallAirstrike(int playerId, Vector3 target, float headingDegrees, float baseY)
        {
            var strike = Spec.Airstrike;
            // 届いた高さは使わず X/Z だけ取り、使った人の体の高さを起点にする (屋根の下から探し始めさせないため)
            // 探す幅は手元の印 (StreakTargeting.Ground) と同じ viewHeight にし、高低差のある地点でも手元と同じ地面に置く
            var searchHeight = Math.Max(Spec.Targeting.ViewHeight, strike.DropHeight);
            var center = Drop(new Vector3(target.X, baseY, target.Z), searchHeight);
            for (var i = 0; i < strike.ShellCount; i++)
            {
                var shellPoint = AirstrikeLine.ShellPoint(center, headingDegrees, strike, i, _random);
                var point = Drop(new Vector3(shellPoint.X, baseY, shellPoint.Z), searchHeight);
                Enqueue(new Shell(_now + ((i + 1) * strike.IntervalSeconds), point, i + 1, strike.ShellCount, playerId));
            }

            Announce(playerId, _rules.Session.GetTeam(playerId), StreakSpec.AirstrikeRewardId, center, strike.ShellCount * strike.IntervalSeconds);
            _log.WriteLine(ServerStreakLog.AirstrikeCalled(playerId, center, headingDegrees, strike.ShellCount, strike.IntervalSeconds, strike.LineLength));

            // 間隔が 0 なら呼んだ tick のうちに落とす (オフラインの AirstrikeReward.Call と同じ)
            FireDueShells();
        }

        // 坂や段差で線の上の点が地面に埋まったり宙に浮いたりせず、屋根や橋があればその上で止まるよう、上から真下へ探す
        private Vector3 Drop(Vector3 point, float height)
        {
            var top = point + new Vector3(0f, height, 0f);
            if (_context.World.Raycast(top, -Vector3.UnitY, height * 2f, ServerHitboxes.RaycastLayers, out var ground))
            {
                return ImpactResolver.DetonationPoint(ground.Point, ground.Normal);
            }

            return point;
        }

        private void Enqueue(Shell shell)
        {
            var index = _shells.Count;
            while (index > 0 && _shells[index - 1].At > shell.At)
            {
                index--;
            }

            _shells.Insert(index, shell);
        }

        private void FireDueShells()
        {
            var count = 0;
            while (count < _shells.Count && _shells[count].At <= _now)
            {
                count++;
            }

            if (count == 0)
            {
                return;
            }

            // 爆発で倒れた知らせから試合が始め直されて並びが消されても、回している途中の並びを書き換えないよう先に取り出す
            _dueShells.Clear();
            _dueShells.AddRange(_shells.GetRange(0, count));
            _shells.RemoveRange(0, count);
            foreach (var shell in _dueShells)
            {
                var (damaged, killed) = _combat.Explode(shell.Point, Spec.Airstrike.Explosion, shell.CallerId, StreakSpec.AirstrikeRewardId);
                _log.WriteLine(ServerStreakLog.AirstrikeShell(shell.CallerId, shell.Index, shell.Count, damaged, killed));
            }

            _dueShells.Clear();
        }

        // 印が切れたことはクライアントが残りの秒で消すので送らない。倒れた・抜けた敵の印だけを消させる
        private void TickMarks()
        {
            if (_marks.Count == 0)
            {
                return;
            }

            for (var i = _marks.Count - 1; i >= 0; i--)
            {
                if (_now >= _marks[i].ExpiresAt)
                {
                    _marks.RemoveAt(i);
                }
            }

            if (_now < _nextTrackTime)
            {
                return;
            }

            _nextTrackTime = _now + MatchRulesModule.PingTrackIntervalSeconds;
            var session = _rules.Session;
            for (var i = _marks.Count - 1; i >= 0; i--)
            {
                var mark = _marks[i];
                var alive = session != null && session.HasPlayer(mark.TargetId) && session.IsAlive(mark.TargetId);
                if (alive && _bodies.TryGet(mark.TargetId, out var target))
                {
                    // 位置は次の知らせで上書きされるので、落ちても送り直さない
                    _moved.PingId = mark.Id;
                    _moved.Position = target.Center;
                    SendToTeam(mark.Team, TransportChannel.Unreliable, _moved);
                    continue;
                }

                _marks.RemoveAt(i);
                SendRemoved(mark, PingRemoval.TargetGone);
            }
        }

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (_trackers.TryGetValue(entry.VictimId, out var victim))
            {
                var lost = victim.Kills;
                var died = victim.Died();
                _log.WriteLine(StreakLog.Died(ServerStreakLog.Id(entry.VictimId), lost, died));
            }

            // 自分が倒れている間に入った分 (飛んでいる空爆など) は、倒れずに倒した数ではないので数えない (オフラインと同じ)
            var session = _rules.Session;
            if (entry.IsSuicide || entry.Friendly || !_trackers.TryGetValue(entry.KillerId, out var killer)
                || session == null || !session.HasPlayer(entry.KillerId) || !session.IsAlive(entry.KillerId))
            {
                return;
            }

            var status = killer.Killed(out var earned);
            var id = ServerStreakLog.Id(entry.KillerId);
            _log.WriteLine(StreakLog.Killed(id, status));
            foreach (var rewardId in earned)
            {
                _log.WriteLine(StreakLog.Earned(id, rewardId, status));
            }
        }

        // 途中から入った人にも、自分の陣営の今の UAV の印を見せる
        private void HandlePlayerJoined(ServerPlayer player)
        {
            GetOrAdd(player.PlayerId);
            var session = _rules.Session;
            if (session == null || !session.HasPlayer(player.PlayerId))
            {
                return;
            }

            var team = session.GetTeam(player.PlayerId);
            foreach (var mark in _marks)
            {
                if (mark.Team == team && _bodies.TryGet(mark.TargetId, out var target) && FillPlaced(mark, target.Center))
                {
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _placed);
                }
            }
        }

        // 呼んだ人が抜けても、呼んだ空爆は落ちる (爆発は攻撃者のいないダメージになる)
        private void HandlePlayerLeft(ServerPlayer player)
        {
            _trackers.Remove(player.PlayerId);
        }

        // 印は送り先をチームで選ぶので、移った人には移る前のチームの印を直接消させる (移った後では届かない)
        internal void HandleTeamChanged(ServerPlayer player, Team from)
        {
            foreach (var mark in _marks)
            {
                if (mark.Team == from)
                {
                    _removed.PingId = mark.Id;
                    _removed.Reason = PingRemoval.Cleared;
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _removed);
                }
            }
        }

        private void HandleMatchStarted()
        {
            foreach (var tracker in _trackers.Values)
            {
                tracker.Reset();
            }

            ClearEffects();
        }

        // 印と落ちる地点は前のマップの位置なので、新しいマップへ持ち込まない
        private void HandleMapChanged(ServerMapData _)
        {
            ClearEffects();
        }

        private void ClearEffects()
        {
            if (_marks.Count > 0)
            {
                _log.WriteLine(ServerStreakLog.UavCleared(_marks.Count));
                foreach (var mark in _marks)
                {
                    SendRemoved(mark, PingRemoval.Cleared);
                }

                _marks.Clear();
            }

            if (_shells.Count > 0)
            {
                _log.WriteLine(ServerStreakLog.AirstrikeCancelled(_shells.Count));
                _shells.Clear();
            }
        }

        private void Announce(int playerId, Team team, string rewardId, Vector3? target, float durationSeconds)
        {
            _activated.PlayerId = playerId;
            _activated.Team = team;
            _activated.RewardId = rewardId;
            _activated.HasTarget = target.HasValue;
            _activated.Target = target ?? Vector3.Zero;
            _activated.DurationSeconds = durationSeconds;
            foreach (var player in _match.Players)
            {
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _activated);
            }
        }

        private bool FillPlaced(UavMark mark, Vector3 position)
        {
            var remaining = mark.ExpiresAt - _now;
            if (!(remaining > 0f))
            {
                return false;
            }

            _placed.PingId = mark.Id;
            _placed.Kind = PingKind.Enemy;
            _placed.Position = position;
            _placed.Tracking = true;
            _placed.RemainingSeconds = remaining;
            return true;
        }

        private void SendRemoved(UavMark mark, PingRemoval reason)
        {
            _removed.PingId = mark.Id;
            _removed.Reason = reason;
            SendToTeam(mark.Team, TransportChannel.Reliable, _removed);
        }

        private void SendToTeam(Team team, TransportChannel channel, IMessage message)
        {
            var session = _rules.Session;
            if (session == null)
            {
                return;
            }

            foreach (var player in _match.Players)
            {
                if (session.HasPlayer(player.PlayerId) && session.GetTeam(player.PlayerId) == team)
                {
                    _context.Messenger.Send(player.Connection, channel, message);
                }
            }
        }

        private int CountTeam(Team team)
        {
            var session = _rules.Session;
            var count = 0;
            foreach (var player in _match.Players)
            {
                if (session != null && session.HasPlayer(player.PlayerId) && session.GetTeam(player.PlayerId) == team)
                {
                    count++;
                }
            }

            return count;
        }

        private ServerStreakRefusal Refuse(int playerId, string rewardId, ServerStreakRefusal reason)
        {
            if (_now >= _nextRefusalLogTime)
            {
                _nextRefusalLogTime = _now + _refusalLogInterval;
                _log.WriteLine(reason == ServerStreakRefusal.NothingReady
                    ? StreakLog.NothingToUse(ServerStreakLog.Id(playerId))
                    : ServerStreakLog.Refused(playerId, rewardId, reason.ToString()));
            }

            return reason;
        }

        private int NextMarkId()
        {
            var id = _nextMarkId;
            _nextMarkId = _nextMarkId == int.MaxValue ? FirstMarkId : _nextMarkId + 1;
            return id;
        }

        private StreakTracker GetOrAdd(int playerId)
        {
            if (!_trackers.TryGetValue(playerId, out var tracker))
            {
                tracker = new StreakTracker(Spec);
                _trackers.Add(playerId, tracker);
            }

            return tracker;
        }

        private static bool IsUsableTarget(Vector3? target, float headingDegrees)
        {
            if (!target.HasValue)
            {
                return false;
            }

            var point = target.Value;
            return float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z) && float.IsFinite(headingDegrees);
        }

        private readonly struct UavMark
        {
            public UavMark(int id, Team team, int targetId, float expiresAt)
            {
                Id = id;
                Team = team;
                TargetId = targetId;
                ExpiresAt = expiresAt;
            }

            public int Id { get; }

            public Team Team { get; }

            public int TargetId { get; }

            public float ExpiresAt { get; }
        }

        private readonly struct Shell
        {
            public Shell(float at, Vector3 point, int index, int count, int callerId)
            {
                At = at;
                Point = point;
                Index = index;
                Count = count;
                CallerId = callerId;
            }

            public float At { get; }

            public Vector3 Point { get; }

            public int Index { get; }

            public int Count { get; }

            public int CallerId { get; }
        }
    }
}
