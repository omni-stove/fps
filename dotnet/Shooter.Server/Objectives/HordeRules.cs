using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Horde のとき、人を全員守る側 (Alpha) に、波の敵を ServerBots の Bot (Bravo) にして、波を全滅させ続ける。
    // 波・通貨・勝ち負けはオフラインの Horde と同じ Core (WaveSchedule・HordeEconomy・HordeWin) で決める。
    // Session (TeamDeathmatchSession) は倒れた人を自分では戻さず (RespawnsByRound)、点数と時間の上限も届かない値にする。
    // 倒れた守る側は波の間にまとめて戻し、倒された敵の Bot は CorpseSeconds 後に試合から抜いて、空いた分を新しい Bot で足す
    internal sealed class HordeRules : IMatchObjectiveRules, ISnapshotContributor
    {
        public const Team Defenders = Team.Alpha;
        public const Team Attackers = Team.Bravo;

        private readonly IMatchObjectiveHost _host;
        private readonly HordeSettings _settings;
        private readonly HordeSpec _spec;
        private readonly IHordeFortifications _fortifications;

        // Bot の Id の昇順で回し、同じ試合なら同じ順に決める
        private readonly SortedDictionary<int, Enemy> _enemies = new SortedDictionary<int, Enemy>();
        private readonly List<int> _scratch = new List<int>();
        private WaveSchedule _schedule;
        private HordeEconomy _economy;
        private Team? _winner;
        private float _clock;
        private int _score;
        private bool _started;

        // fortifications が null なら、直す・置く要求をいつも NothingToDo で断る (防衛物の無いマップ・部品をつながずに作ったとき)
        public HordeRules(NetMatchSettings settings, IMatchObjectiveHost host, IHordeFortifications fortifications = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _settings = settings.Horde;
            _fortifications = fortifications;
            try
            {
                _spec = _settings.ToSpec(Side.FromTeam(Defenders), Side.FromTeam(Attackers));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: " + e.Message, e);
            }

            NewSchedule();
        }

        private enum EnemyState
        {
            // 波の敵として数えた、倒されていない Bot
            Fighting,

            // 倒された Bot。CorpseSeconds の間は体を残す
            Corpse,

            // 数えていない Bot。次の足し引きで ServerBots に抜かせる
            Spare,
        }

        public MatchObjective Objective => MatchObjective.Horde;

        public WaveSchedule Schedule => _schedule;

        public HordeEconomy Economy => _economy;

        public bool PlaysInRounds => false;

        // 試合の点数は敵を倒した数 (HordeSpec.KillScore) にする。人が倒されても敵の側には点を入れない
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        // 制限時間が無いので残り時間は送らず、始まってからの秒を ObjectiveRemainingSeconds で送る
        public float? RemainingSeconds => 0f;

        public int? BotsWanted
        {
            get
            {
                var session = _host.Session;
                if (session == null || session.State == GameModeState.Ended || CountDefenders(session) == 0)
                {
                    return 0;
                }

                var kept = 0;
                foreach (var enemy in _enemies.Values)
                {
                    if (enemy.State != EnemyState.Spare)
                    {
                        kept++;
                    }
                }

                return kept + _schedule.SpawnBudget;
            }
        }

        public int BotDropOrder(int playerId)
        {
            if (!_enemies.TryGetValue(playerId, out var enemy))
            {
                return 0;
            }

            switch (enemy.State)
            {
                case EnemyState.Spare:
                    return 0;
                case EnemyState.Corpse:
                    return 1;
                default:
                    return 2;
            }
        }

        public Team? FixedTeam(bool isBot) => isBot ? Attackers : Defenders;

        // 敵を見つけていない Bot を、いちばん近い守る側へ向かわせる (オフラインの WaveSpawner の呼び寄せと同じ間隔)。
        // 毎 tick 向け直すと Bot の路を毎回作り直すので、AlertInterval の間は同じ先を返す
        public Vector3? BotGoal(int playerId)
        {
            if (!_enemies.TryGetValue(playerId, out var enemy) || enemy.State != EnemyState.Fighting)
            {
                return null;
            }

            if (enemy.Goal.HasValue && _clock < enemy.NextAlertAt)
            {
                return enemy.Goal;
            }

            enemy.NextAlertAt = _clock + _spec.AlertInterval;
            enemy.Goal = NearestDefender(playerId);
            return enemy.Goal;
        }

        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, rules, respawnsByRound: true);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            if (!HasSpawnFor(points, Defenders) || !HasSpawnFor(points, Attackers))
            {
                throw new InvalidOperationException(
                    "cannot start the " + Objective + " match on map " + map.Name + ": it needs spawn points for both the defenders ("
                    + Defenders + ") and the enemies (" + Attackers + ")");
            }
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            CheckMap(map, points);
            _fortifications?.Prepare();
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _fortifications?.Prepare();
            Subscribe(_schedule);
            _host.Log.WriteLine(HordeRulesLog.Started(_settings));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            Unsubscribe(_schedule);
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            // 置く場所の防衛物は、試合が始まる前 (守る側を待っている間) から置かれていない姿で見せる
            _fortifications?.Tick();
            var session = _host.Session;
            if (session == null || session.State == GameModeState.Ended)
            {
                return false;
            }

            // Session は両方のチームに人が居るまで始まらず、敵の Bot は波が始まるまで居ないので、守る側が入ったら試合の始まりを待たずに波を始める
            if (_schedule.Phase == WavePhase.Waiting)
            {
                if (CountDefenders(session) == 0)
                {
                    return false;
                }

                _schedule.Start();
            }

            if (!matchPlaying)
            {
                return false;
            }

            _clock += deltaSeconds;
            _schedule.Tick(deltaSeconds);
            SpareCorpses();
            ReturnDefenders(session);

            var defenders = CountDefenders(session);
            var defendersOut = defenders == 0 || (_schedule.Phase == WavePhase.Fighting && CountStandingDefenders(session) == 0);
            var winner = HordeWin.Decide(_schedule.Phase, defendersOut, Side.FromTeam(Defenders), Side.FromTeam(Attackers));
            if (!winner.HasValue)
            {
                return false;
            }

            if (defenders == 0)
            {
                _host.Log.WriteLine(HordeRulesLog.NoDefenders(_schedule.WaveNumber));
            }

            _winner = winner.Value.ToTeam();
            return true;
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            // チームを移された (MatchRulesModule.HandleTeamChanged) Bot もここへ来るので、知っている Bot は数え直さない
            if (session == null || !session.HasPlayer(playerId) || !BotRoster.IsBotId(playerId) || _enemies.ContainsKey(playerId))
            {
                return;
            }

            // ServerBots が足した Bot。波が出せる数の内なら波の敵として数え、多ければ数えずに抜かせる
            if (_schedule.SpawnBudget > 0 && session.IsAlive(playerId))
            {
                _schedule.ReportSpawned();
                _enemies[playerId] = new Enemy(EnemyState.Fighting);
                _host.Log.WriteLine(HordeRulesLog.EnemyJoined(playerId, _schedule));
                return;
            }

            _enemies[playerId] = new Enemy(EnemyState.Spare);
            _host.Log.WriteLine(HordeRulesLog.SpareBot(playerId, _schedule.Phase));
        }

        public void PlayerLeft(int playerId)
        {
            if (!_enemies.TryGetValue(playerId, out var enemy))
            {
                return;
            }

            _enemies.Remove(playerId);

            // 試合が終わると ServerBots が Bot を抜くので、終わった後に抜けた敵は数えない
            var session = _host.Session;
            if (session == null || session.State == GameModeState.Ended)
            {
                return;
            }

            if (enemy.State == EnemyState.Fighting && _schedule.Phase == WavePhase.Fighting && _schedule.Alive > 0)
            {
                _host.Log.WriteLine(HordeRulesLog.EnemyVanished(playerId, _schedule.WaveNumber, _schedule.RemainingEnemies - 1));
                _schedule.ReportKilled();
            }
        }

        public void PlayerKilled(int killerId, int victimId)
        {
            if (!_enemies.TryGetValue(victimId, out var enemy) || enemy.State != EnemyState.Fighting)
            {
                return;
            }

            enemy.State = EnemyState.Corpse;
            enemy.Goal = null;
            enemy.RemoveAt = _clock + _settings.CorpseSeconds;
            if (_schedule.Phase != WavePhase.Fighting || _schedule.Alive <= 0)
            {
                return;
            }

            // オフラインと同じく、誰に倒されても (味方撃ち・落下でも) 通貨を払う。全滅の知らせで通貨のログが出るので、払うのを先にする
            var waveNumber = _schedule.WaveNumber;
            if (_economy.TryEarn(out var reward))
            {
                _host.Log.WriteLine(HordeLog.EarnedLog(waveNumber, reward, _economy.Currency));
                _score = (int)Math.Min(int.MaxValue, (long)_score + _spec.KillScore);
                _host.SetMatchScores(_score, 0);
            }

            _host.Log.WriteLine(HordeRulesLog.EnemyKilled(victimId, killerId, waveNumber, _schedule.RemainingEnemies - 1));
            _schedule.ReportKilled();
        }

        // Session.Restart が全員を戻した直後。残っている Bot は前の試合の敵なので数えずに抜かせ、守る側が居れば次の Tick で波を始める
        public void MatchRestarted()
        {
            Unsubscribe(_schedule);
            NewSchedule();
            if (_started)
            {
                Subscribe(_schedule);
            }

            foreach (var enemy in _enemies.Values)
            {
                enemy.State = EnemyState.Spare;
                enemy.Goal = null;
            }

            _winner = null;
            _clock = 0f;
            _score = 0;
            _fortifications?.Prepare();
            _host.SetMatchScores(0, 0);
            _host.Log.WriteLine(HordeRulesLog.Restart());
        }

        public void MatchEnded()
        {
            var victory = _winner == Defenders;
            _host.Log.WriteLine(HordeLog.ResultLog(victory, _schedule.WaveNumber, _schedule.WaveCount));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        public Team? DecideWinner(Team? killWinner)
        {
            return _winner ?? killWinner;
        }

        // 防衛物の部品を戻す要求。払えるか (波の間か・通貨が足りるか) は オフラインの HordeRunner.TryInteract と同じく HordeEconomy で決める
        public HordePurchaseVerdict Fortify(int playerId)
        {
            var session = _host.Session;
            if (!CanReach(playerId, out var body))
            {
                return HordePurchaseVerdict.NothingToDo;
            }

            var target = _fortifications.FindInReach(body.Position, _spec.InteractRadius);
            if (!target.HasValue || target.Value.Action == HordeFortificationAction.None || target.Value.RestorablePieces == 0)
            {
                _host.Log.WriteLine(HordeRulesLog.Denied(playerId, HordePurchaseVerdict.NothingToDo));
                return HordePurchaseVerdict.NothingToDo;
            }

            var found = target.Value;
            var cost = found.Action == HordeFortificationAction.Build ? _spec.BuildCost : _spec.RepairCost(found.RestorablePieces);
            var verdict = _economy.TryPay(session.State == GameModeState.Playing, cost);
            if (verdict != HordePurchaseVerdict.Ok)
            {
                _host.Log.WriteLine(HordeLog.DeniedLog(found.Action, found.NameKey, verdict, cost, _economy.Currency));
                return verdict;
            }

            var restored = _fortifications.Restore(found.Id, found.Action);
            _host.Log.WriteLine(HordeLog.PurchasedLog(found.Action, found.NameKey, restored, cost, _economy.Currency));
            return HordePurchaseVerdict.Ok;
        }

        // オフラインの HordeRunner の案内と同じく、試合中に立っている守る側にだけ、届く距離のいちばん近い防衛物の額と払えるかを出す。
        // 戻せる部品が無い (全部が人の体と重なる) なら、要求しても Fortify が断るので NothingToDo で見せる
        public HordeOfferSync? Offer(int playerId)
        {
            var session = _host.Session;
            if (session == null || session.State != GameModeState.Playing || !CanReach(playerId, out var body))
            {
                return null;
            }

            var target = _fortifications.FindInReach(body.Position, _spec.InteractRadius);
            if (!target.HasValue || target.Value.Action == HordeFortificationAction.None)
            {
                return null;
            }

            var found = target.Value;
            var cost = found.Action == HordeFortificationAction.Build ? _spec.BuildCost : _spec.RepairCost(found.RestorablePieces);
            var verdict = found.RestorablePieces == 0 ? HordePurchaseVerdict.NothingToDo : _economy.Check(true, cost);
            return new HordeOfferSync(found.Id, found.Action, found.NameKey, cost, verdict);
        }

        // ほかの人の案内は見せないので、受け取る人の分だけを持ち主の部分に書く
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            snapshot.Owner.HordeOffer = receiver != null ? Offer(receiver.PlayerId) : null;
        }

        // 欄は既存のものに載せる: 通貨は AlphaPoints、今の波の残りの敵は BravoPoints、波の番号と数は RoundNumber・RoundsToWin、
        // 休憩の残りは NextWaveSeconds。制限時間が無いので、始まってからの秒は残り時間の欄 (ObjectiveRemainingSeconds) で送る
        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaScore = _score;
            snapshot.BravoScore = 0;
            snapshot.AlphaPoints = _economy.Currency;
            snapshot.BravoPoints = _schedule.RemainingEnemies;
            snapshot.RoundNumber = _schedule.WaveNumber;
            snapshot.RoundsToWin = _schedule.WaveCount ?? 0;
            snapshot.NextWaveSeconds = _schedule.Phase == WavePhase.Resting ? HudFormat.DisplayedSeconds(_schedule.RestRemaining) : 0;
            snapshot.ObjectiveRemainingSeconds = (int)Math.Min(int.MaxValue, Math.Floor(_clock));
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
        }

        // 直す・置くのは立っている人だけ。Bot (波の敵) とダウン中の人は防衛物に触れない
        private bool CanReach(int playerId, out ServerBody body)
        {
            body = null;
            var session = _host.Session;
            return _fortifications != null && session != null && session.HasPlayer(playerId) && !BotRoster.IsBotId(playerId)
                && session.IsAlive(playerId) && !session.IsDowned(playerId) && _host.Bodies != null && _host.Bodies.TryGet(playerId, out body);
        }

        private void NewSchedule()
        {
            _schedule = _settings.CreateSchedule();
            _economy = new HordeEconomy(_schedule, _spec);
        }

        private void Subscribe(WaveSchedule schedule)
        {
            schedule.WaveStarted += HandleWaveStarted;
            schedule.WaveCleared += HandleWaveCleared;
            schedule.Completed += HandleCompleted;
        }

        private void Unsubscribe(WaveSchedule schedule)
        {
            schedule.WaveStarted -= HandleWaveStarted;
            schedule.WaveCleared -= HandleWaveCleared;
            schedule.Completed -= HandleCompleted;
        }

        private void HandleWaveStarted(int number, Wave wave)
        {
            _host.Log.WriteLine(HordeLog.WaveStartedLog(number, _schedule.WaveCount, wave.EnemyCount));
            _host.Log.WriteLine(WaveLog.Started(number, _schedule.WaveCount, wave));
        }

        private void HandleWaveCleared(int number)
        {
            _host.Log.WriteLine(HordeLog.WaveClearedLog(number, _economy.Currency, CountWaitingDefenders()));
            _host.Log.WriteLine(WaveLog.Cleared(number, _schedule.Phase == WavePhase.Resting ? _schedule.RestRemaining : (float?)null));
        }

        private void HandleCompleted()
        {
            _host.Log.WriteLine(WaveLog.Completed(_schedule.WaveNumber));
        }

        private void SpareCorpses()
        {
            foreach (var enemy in _enemies.Values)
            {
                if (enemy.State == EnemyState.Corpse && _clock >= enemy.RemoveAt)
                {
                    enemy.State = EnemyState.Spare;
                }
            }
        }

        // オフラインの HordeRespawn と同じく、倒れた守る側は波の間 (休憩) にまとめて戻す。戻す順は Id の昇順
        private void ReturnDefenders(TeamDeathmatchSession session)
        {
            if (!HordeRespawn.IsBetween(_schedule.Phase))
            {
                return;
            }

            _scratch.Clear();
            _scratch.AddRange(session.GetPlayerIds());
            _scratch.Sort();
            foreach (var playerId in _scratch)
            {
                if (IsDefender(session, playerId) && !session.IsAlive(playerId) && session.RespawnPlayer(playerId))
                {
                    _host.Log.WriteLine(HordeRulesLog.Returned(playerId, _schedule.WaveNumber));
                }
            }
        }

        private Vector3? NearestDefender(int botId)
        {
            var session = _host.Session;
            var bodies = _host.Bodies;
            if (session == null || bodies == null || !bodies.TryGet(botId, out var self))
            {
                return null;
            }

            Vector3? nearest = null;
            var best = float.PositiveInfinity;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (!IsDefender(session, playerId) || !session.IsAlive(playerId) || !bodies.TryGet(playerId, out var body))
                {
                    continue;
                }

                var distance = Vector3.DistanceSquared(self.Position, body.Position);
                if (distance < best)
                {
                    best = distance;
                    nearest = body.Position;
                }
            }

            return nearest;
        }

        private int CountWaitingDefenders()
        {
            var session = _host.Session;
            if (session == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (IsDefender(session, playerId) && !session.IsAlive(playerId))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDefenders(TeamDeathmatchSession session)
        {
            var count = 0;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (IsDefender(session, playerId))
                {
                    count++;
                }
            }

            return count;
        }

        // ダウン中の人も数えるのは、起こせる味方が立っていればまだ戦えるため。起こす味方が居なければ Session がダウン中の人を倒す
        private static int CountStandingDefenders(TeamDeathmatchSession session)
        {
            var count = 0;
            foreach (var playerId in session.GetPlayerIds())
            {
                if (IsDefender(session, playerId) && session.IsAlive(playerId))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsDefender(TeamDeathmatchSession session, int playerId)
        {
            return !BotRoster.IsBotId(playerId) && session.GetTeam(playerId) == Defenders;
        }

        private static bool HasSpawnFor(NetMapPoints points, Team team)
        {
            foreach (var point in points.SpawnPoints)
            {
                if (!point.Team.HasValue || point.Team.Value == team)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class Enemy
        {
            public Enemy(EnemyState state)
            {
                State = state;
            }

            public EnemyState State { get; set; }

            public float RemoveAt { get; set; }

            public float NextAlertAt { get; set; }

            public Vector3? Goal { get; set; }
        }
    }
}
