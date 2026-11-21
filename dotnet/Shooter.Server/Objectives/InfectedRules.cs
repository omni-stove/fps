using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Infected のとき、最初の感染者の選び方・倒された生存者が感染者に移ること・時間切れで生存者が勝つことを
    // オフラインと同じ Core の Infected に任せ、ここは試合の出来事を渡すだけにする。
    // Session (TeamDeathmatchSession) は試合の途中でチームを替えられないので、生存者 (Alpha) と感染者 (Bravo) の陣営は Infected が持ち、
    // 敵味方・スナップショットのチーム・勝った人はその陣営で決める。Session は体力・出現だけを受け持ち、キル数でも時間でも先に終わらないよう上限を届かない値にする。
    // 感染者の体 (体力の上限・移動の速さ) は感染したときに Session と体に書く。武器は目的から一覧を引けないので、持ち替える人を Rearming で
    // 試合の側 (MatchRulesModule) に知らせ、そちらが一覧から引いた武器を配る (Gun Game の段の武器と同じ分け方)
    internal sealed class InfectedRules : IMatchObjectiveRules
    {
        private readonly IMatchObjectiveHost _host;
        private readonly Infected _infected;

        // 体を感染者のものにした人。試合の頭と目的を止めるときに生存者の体へ戻す
        private readonly HashSet<int> _infectedBodies = new HashSet<int>();
        private readonly List<int> _restoring = new List<int>();
        private bool _started;
        private bool _loggedOutbreak;
        private int _sentSurvivorScore;
        private int _sentInfectedScore;

        public InfectedRules(NetMatchSettings settings, IMatchObjectiveHost host)
            : this(settings, host, null)
        {
        }

        // pickIndex (0 から count 未満の添字を返す) を渡せるのは、テストで最初の感染者を決まった人にするため。null なら Infected が乱数で選ぶ
        internal InfectedRules(NetMatchSettings settings, IMatchObjectiveHost host, Func<int, int> pickIndex)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _infected = new Infected(settings.Infected, pickIndex);
        }

        // 感染者の武器に持ち替えさせる人。倒れている人も来るので、受け手は立っている人だけを持ち替えさせる (倒れている人は出直すときに持つ)
        public event Action<int> Rearming;

        public MatchObjective Objective => MatchObjective.Infected;

        public Infected Infected => _infected;

        public bool PlaysInRounds => false;

        // 試合の点数は感染させた点と生き残った点にする
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        public float? RemainingSeconds => Math.Max(0f, _infected.Session.RemainingSeconds);

        // settings の出直しの秒でなく Infected の値を使うのは、Session と Infected で倒れている間の長さをそろえるため
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, _infected.Spec.InfectedRespawnSeconds, rules);
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
            _infected.Turned += HandleTurned;
            _host.Log.WriteLine(InfectedRulesLog.Started(_infected.Spec));
            _host.Log.WriteLine(InfectedRulesLog.Body(_infected.Spec));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _infected.Turned -= HandleTurned;

            // ルールを替えて次の目的に移っても体は同じ人のまま残るので、感染者の速さを持ち越させない
            RestoreBodies();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            var session = _infected.Session;
            if (session.State == GameModeState.Waiting)
            {
                session.Start();
            }

            if (session.State == GameModeState.Playing)
            {
                session.Tick(deltaSeconds);
                LogOutbreak();
                SendScores();
            }

            return session.State == GameModeState.Ended;
        }

        // 始まる前に入った人は生存者、感染が始まった後に入った人は感染者にする。抜けて入り直して生存者に戻るのを防ぐため。
        // チームを替えて入り直した人も来るが、陣営は Infected が決めるので入れ直さない
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            var mode = _infected.Session;
            if (session == null || !session.HasPlayer(playerId) || mode.HasPlayer(playerId))
            {
                return;
            }

            var side = _infected.HasBrokenOut ? Infected.InfectedSide : Infected.SurvivorSide;
            mode.AddPlayer(playerId, side);
            _host.Log.WriteLine(InfectedRulesLog.Joined(playerId, side));

            // Session は入ったときに出現させて武器を配り終えているので、感染者として入った人の体と武器はここで替える
            if (side == Infected.InfectedSide)
            {
                TurnBody(playerId);
                Rearming?.Invoke(playerId);
            }
        }

        public void PlayerLeft(int playerId)
        {
            var mode = _infected.Session;
            if (mode.HasPlayer(playerId))
            {
                mode.RemovePlayer(playerId);
            }

            // Session は抜けた人の上限を戻し、体は抜けると捨てられるので、戻さずに忘れる
            _infectedBodies.Remove(playerId);
        }

        // キルを先に Infected の Session へ渡すのは、感染させた点を倒した時点の陣営で数えるため (InfectedRunner と同じ順)。
        // 自滅と、試合に居ない人に倒された生存者も感染者に移る
        public void PlayerKilled(int killerId, int victimId)
        {
            var mode = _infected.Session;
            if (!mode.HasPlayer(victimId))
            {
                return;
            }

            var byOther = killerId != victimId && mode.HasPlayer(killerId);
            if (byOther)
            {
                mode.ReportKill(killerId, victimId);
            }
            else
            {
                mode.ReportDeath(victimId);
            }

            _infected.Infect(victimId, byOther ? killerId : (int?)null);
            SendScores();
        }

        // Session は Restart で体力の上限を戻した後にここを呼ぶ。続く出現で配る武器は、Reset で全員が生存者に戻った後の陣営で決まる
        public void MatchRestarted()
        {
            RestoreBodies();
            _infected.Reset();
            _loggedOutbreak = false;
            _sentSurvivorScore = 0;
            _sentInfectedScore = 0;
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(InfectedRulesLog.Restart());
        }

        public void MatchEnded()
        {
            _host.Log.WriteLine(InfectedRulesLog.State(_infected));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // Infected が終えていない (目的の外で試合が終わった) ときは、Session がキル数で決めた勝者のまま
        public Team? DecideWinner(Team? killWinner)
        {
            var session = _infected.Session;
            return session.State == GameModeState.Ended ? Side.ToTeam(session.Winner) : killWinner;
        }

        // 勝ったかは試合に入ったときのチームでなく、終わったときの陣営で決める
        public bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            if (!winner.HasValue)
            {
                return false;
            }

            var mode = _infected.Session;
            var team = mode.HasPlayer(playerId) ? mode.GetSide(playerId).ToTeam() : playerTeam;
            return team == winner.Value;
        }

        // どちらかが Infected に居ない (入る前の) ときは、試合のチームで決める
        public bool AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            if (a == b)
            {
                return false;
            }

            var mode = _infected.Session;
            if (mode.HasPlayer(a) && mode.HasPlayer(b))
            {
                return mode.GetSide(a) != mode.GetSide(b);
            }

            return aTeam != bTeam;
        }

        // AlphaPoints・BravoPoints に生存者と感染者の人数を、ObjectiveRemainingSeconds に感染が始まるまでの秒を載せる。
        // 始まる前の残りは 1 秒以上で送り、0 を「始まった」の印にする (始まりの秒が 0 でも、始まる前に 0 を送らない)
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            var session = _infected.Session;
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = _infected.SurvivorCount;
            snapshot.BravoPoints = _infected.InfectedCount;
            snapshot.AlphaScore = HudFormat.DisplayedScore(session.GetScore(Infected.SurvivorSide));
            snapshot.BravoScore = HudFormat.DisplayedScore(session.GetScore(Infected.InfectedSide));
            snapshot.ObjectiveRemainingSeconds = _infected.HasBrokenOut
                ? 0
                : Math.Max(1, HudFormat.DisplayedSeconds(_infected.SecondsUntilOutbreak));
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

        // 行のチームを陣営で書き直し、クライアントが感染者を敵の色で出せるようにする
        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
            var mode = _infected.Session;
            if (mode.HasPlayer(playerId))
            {
                snapshot.Team = mode.GetSide(playerId).ToTeam();
            }
        }

        // 目的に入っていない人 (入る前・抜けた後) は感染者でない
        public bool IsInfected(int playerId)
        {
            return _infected.Session.HasPlayer(playerId) && _infected.IsInfected(playerId);
        }

        private void HandleTurned(InfectedTurn turn)
        {
            // 最初の感染者は Outbreak の行にまとめて出すので、1 人ずつは書かない
            if (!turn.IsFirst)
            {
                _host.Log.WriteLine(InfectedLog.Turn(turn));
            }

            TurnBody(turn.PlayerId);
            Rearming?.Invoke(turn.PlayerId);
        }

        // 立っている人は新しい上限まで満たす。オフラインの InfectedRunner は最初の感染者を出直させ、倒された生存者は出直すときに
        // 新しい上限で出るので、どちらも満タンの感染者として動き出すのに合わせる。倒れている人には Heal が効かず、出直すときに満タンになる
        private void TurnBody(int playerId)
        {
            var spec = _infected.Spec;
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId))
            {
                session.SetMaxHealth(playerId, spec.InfectedMaxHealth);
                session.Heal(playerId, spec.InfectedMaxHealth);
            }

            var bodies = _host.Bodies;
            if (bodies != null && bodies.TryGet(playerId, out var body))
            {
                body.MoveSpeedMultiplier = spec.InfectedSpeedMultiplier;
            }

            _infectedBodies.Add(playerId);
            _host.Log.WriteLine(InfectedRulesLog.Turned(playerId, spec));
        }

        private void RestoreBodies()
        {
            if (_infectedBodies.Count == 0)
            {
                return;
            }

            _restoring.Clear();
            _restoring.AddRange(_infectedBodies);
            _infectedBodies.Clear();
            _restoring.Sort();
            var session = _host.Session;
            var bodies = _host.Bodies;
            foreach (var playerId in _restoring)
            {
                if (session != null && session.HasPlayer(playerId))
                {
                    session.SetMaxHealth(playerId, session.MaxHealth);
                }

                if (bodies != null && bodies.TryGet(playerId, out var body))
                {
                    body.MoveSpeedMultiplier = 1f;
                }
            }

            _host.Log.WriteLine(InfectedRulesLog.Restored(_restoring));
            _restoring.Clear();
        }

        private void LogOutbreak()
        {
            if (!_infected.HasBrokenOut || _loggedOutbreak)
            {
                return;
            }

            _loggedOutbreak = true;
            _host.Log.WriteLine(InfectedLog.Outbreak(_infected.FirstInfectedIds));
            if (_infected.FirstInfectedIds.Count == 0)
            {
                _host.Log.WriteLine(InfectedRulesLog.NoOneInfected(_infected.Session.PlayerCount));
            }
        }

        private void SendScores()
        {
            var session = _infected.Session;
            var survivors = HudFormat.DisplayedScore(session.GetScore(Infected.SurvivorSide));
            var infected = HudFormat.DisplayedScore(session.GetScore(Infected.InfectedSide));
            if (survivors == _sentSurvivorScore && infected == _sentInfectedScore)
            {
                return;
            }

            _sentSurvivorScore = survivors;
            _sentInfectedScore = infected;
            _host.SetMatchScores(survivors, infected);
            _host.Log.WriteLine(InfectedRulesLog.Score(survivors, infected));
        }
    }
}
