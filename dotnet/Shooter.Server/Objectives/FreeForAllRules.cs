using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が FreeForAll のとき、全員を互いの敵として、点と勝敗をオフラインと同じ Core の FFA (FreeForAllMode の ModeSession) で決める。
    // Session (TeamDeathmatchSession) は体力・出直し・出現だけを受け持ち、チームの点数と時間では試合を終えない。
    // ModeSession の陣営はプレイヤー Id と同じ番号で作り、スナップショットの行とそのまま突き合わせる
    internal sealed class FreeForAllRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly FreeForAllSpec _spec;
        private readonly ModeSession _mode;
        private readonly Dictionary<int, LeftRecord> _left = new Dictionary<int, LeftRecord>();
        private bool _started;

        public FreeForAllRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _spec = settings.FreeForAll;

            // ModeSession にも出直しを待たせると、Session が先に出直させた人へのキルを倒れている人へのものとして捨てるので、待ちは Session だけに数えさせる
            _mode = FreeForAllMode.Create(_spec.TimeLimitSeconds, _spec.ScoreLimit, 0f, _spec.PointsPerKill);
        }

        public MatchObjective Objective => MatchObjective.FreeForAll;

        public ModeSession Mode => _mode;

        public FreeForAllSpec Spec => _spec;

        // 決着していなければ null
        public int? WinnerId => _mode.State == GameModeState.Ended && _mode.Winner.HasValue ? _mode.Winner.Value.Id : (int?)null;

        public bool PlaysInRounds => false;

        // チームのキル数は FFA の点ではないので、試合の点数をキルの点数で書き換えさせない
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => Math.Max(0f, _mode.RemainingSeconds);

        // Session は 2 チームでしか数えないので、同じチームの相手も撃てるようにし、味方による蘇生とチームの全滅での決着を外す。
        // 時間とキル数の上限は ModeSession が見るので、Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var sessionRules = rules
                .WithFriendlyFire(FriendlyFire.On)
                .WithRespawnMode(RespawnMode.Individual)
                .WithActions(rules.Actions.WithDowned(Downed.Off));
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, _spec.RespawnSeconds, sessionRules);
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
            _mode.Ended += HandleEnded;
            _host.Log.WriteLine(FreeForAllLog.Started(_spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _mode.Ended -= HandleEnded;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            StartIfWaiting();
            _mode.Tick(deltaSeconds);
            return _mode.State == GameModeState.Ended;
        }

        // 試合の合間にチームを移った人とマップが替わったときにも呼ばれるので、もう居る人は数え直さない
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session == null || !session.HasPlayer(playerId) || _mode.HasPlayer(playerId))
            {
                return;
            }

            var side = new Side(playerId);
            if (_left.TryGetValue(playerId, out var record))
            {
                _left.Remove(playerId);
                _mode.AddPlayer(playerId, side, record.Kills, record.Deaths);
                return;
            }

            _mode.AddPlayer(playerId, side);
        }

        // 陣営の点は抜けた後も ModeSession に残るので、同じ Id で戻ったときに点とキル数がずれないよう、キル数と倒れた数も残す
        public void PlayerLeft(int playerId)
        {
            if (!_mode.HasPlayer(playerId))
            {
                return;
            }

            _left[playerId] = new LeftRecord(_mode.GetKills(playerId), _mode.GetDeaths(playerId));
            _mode.RemovePlayer(playerId);
        }

        // Session は同じチームの相手に倒されたときを味方撃ちとして点にしないが、FFA では倒した人の点にする
        public void PlayerKilled(int killerId, int victimId)
        {
            if (!_mode.HasPlayer(victimId))
            {
                return;
            }

            // Session が始まった Tick のうちに倒れても、次の Tick を待たずに数える
            StartIfWaiting();
            if (killerId != victimId && _mode.HasPlayer(killerId))
            {
                _mode.ReportKill(killerId, victimId);
                return;
            }

            _mode.ReportDeath(victimId);
        }

        public void MatchRestarted()
        {
            _mode.Reset();
            _left.Clear();
            _host.Log.WriteLine(FreeForAllLog.Restart());
        }

        // Session が先に終わったときも、ModeSession をそのときの先頭で終えて、残る点と勝者をそろえる
        public void MatchEnded()
        {
            if (_mode.State == GameModeState.Playing)
            {
                _mode.Finish(_mode.LeadingSide);
            }
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // 勝つのはチームではなく 1 人なので、チームの勝者は出さない。勝った人はプレイヤーの行の点から決まる
        public Team? DecideWinner(Team? killWinner) => null;

        // スナップショットにはプレイヤーごとの点の欄が無いので、点はプレイヤーの行のキル数に 1 キルの点を掛けて出してもらう。
        // チームの点の欄は FFA では空くので、1 キルの点とキル数の上限を載せる
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _spec.PointsPerKill;
            snapshot.BravoPoints = _spec.ScoreLimit;
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
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
            if (!_mode.HasPlayer(playerId))
            {
                return;
            }

            snapshot.Kills = _mode.GetKills(playerId);
            snapshot.Deaths = _mode.GetDeaths(playerId);
        }

        public int GetScore(int playerId)
        {
            return HudFormat.DisplayedScore(_mode.GetScore(new Side(playerId)));
        }

        private void StartIfWaiting()
        {
            var session = _host.Session;
            if (_mode.State == GameModeState.Waiting && session != null && session.State == GameModeState.Playing)
            {
                _mode.Start();
            }
        }

        private void HandleEnded(Side? winner)
        {
            _host.Log.WriteLine(FreeForAllLog.Ended(_mode));
        }

        private readonly struct LeftRecord
        {
            public LeftRecord(int kills, int deaths)
            {
                Kills = kills;
                Deaths = deaths;
            }

            public int Kills { get; }

            public int Deaths { get; }
        }
    }
}
