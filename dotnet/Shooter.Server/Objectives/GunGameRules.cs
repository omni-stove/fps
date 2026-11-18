using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が GunGame のとき、武器の段と勝ち負けを Core の GunGame に任せる。
    // Session (TeamDeathmatchSession) の 2 チームで数えないのは、GunGame が 1 人 1 陣営で段と勝ち負けを決めるため。
    // オフラインの GunGameRunner と同じく FreeForAll の ModeSession を持ち、プレイヤーの Id をそのまま陣営の Id にする。
    // 試合の時間・体力・出現は Session のまま使う
    internal sealed class GunGameRules : IMatchObjectiveRules, ISnapshotContributor
    {
        private readonly IMatchObjectiveHost _host;
        private readonly GunGameSpec _spec;
        private readonly GunGame _gunGame;
        private readonly ModeSession _mode;
        private FriendlyFire _friendlyFire;
        private bool _started;
        private bool _ended;
        private Side? _winner;

        public GunGameRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _spec = settings.GunGame;
            try
            {
                _gunGame = new GunGame(_spec.ToLadder());
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                throw new InvalidOperationException("cannot start the " + Objective + " match: " + e.Message, e);
            }

            // Tick しないのは、時間切れを Session が数え、2 つの時計がずれないようにするため
            _mode = new ModeSession(
                new FreeForAll(),
                _spec.TimeLimitSeconds,
                Array.Empty<IScoreRule>(),
                null,
                new IWinCondition[] { _gunGame });
        }

        public MatchObjective Objective => MatchObjective.GunGame;

        public GunGame GunGame => _gunGame;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => null;

        // キル数の上限を届かない値にするのは、段を登り切った人が出るか時間が切れるまで続けるため
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            _friendlyFire = rules.FriendlyFire;
            return new TeamDeathmatchSpec(int.MaxValue, _spec.TimeLimitSeconds, _spec.RespawnSeconds, rules);
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
            _host.Log.WriteLine(GunGameRulesLog.Started(_spec, _friendlyFire));
        }

        public void Stop()
        {
            _started = false;
        }

        // 登り切った Tick でなく次の Tick で終えるのは、オフラインの ModeSession が CheckStanding を Tick で見るのとそろえるため
        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            StartModeIfPlaying();
            return _gunGame.FinisherId.HasValue;
        }

        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (session != null && session.HasPlayer(playerId) && !_mode.HasPlayer(playerId))
            {
                _mode.AddPlayer(playerId, SideOf(playerId));
            }
        }

        public void PlayerLeft(int playerId)
        {
            if (_mode.HasPlayer(playerId))
            {
                _mode.RemovePlayer(playerId);
            }
        }

        // 段を進めるには倒した武器の Id が要り、ここには届かないので、キルは CreditKill で受ける
        public void PlayerKilled(int killerId, int victimId)
        {
        }

        // MatchRulesModule.PlayerKilled (または ServerCombat.PlayerKilled) から受ける
        public void CreditKill(KillFeedEntry entry)
        {
            if (!_started || !_mode.HasPlayer(entry.KillerId) || !_mode.HasPlayer(entry.VictimId))
            {
                return;
            }

            StartModeIfPlaying();
            var kill = _gunGame.ReportKill(_mode, entry.KillerId, entry.VictimId, entry.WeaponId);
            var count = _gunGame.Ladder.TierCount;
            if (kill.Demoted)
            {
                _host.Log.WriteLine(GunGameText.Demote(kill.VictimId, kill.VictimTier, count, _gunGame.GetWeapon(kill.VictimId).LogName));
            }

            if (kill.Finished)
            {
                _host.Log.WriteLine(GunGameText.Finish(kill.KillerId, _gunGame.GetWeapon(kill.KillerId).LogName));
            }
            else if (kill.Promoted)
            {
                _host.Log.WriteLine(GunGameText.Promote(kill.KillerId, kill.KillerTier, count, _gunGame.GetWeapon(kill.KillerId).LogName));
            }
        }

        public string WeaponIdFor(int playerId)
        {
            return _gunGame.GetWeapon(playerId).WeaponId;
        }

        public void MatchRestarted()
        {
            _gunGame.Reset();
            _mode.Reset();
            _ended = false;
            _winner = null;
            _host.Log.WriteLine(GunGameRulesLog.Restart());
        }

        public void MatchEnded()
        {
            var winner = DecideSide();
            _ended = true;
            _winner = winner;
            _mode.Finish(winner);
            var team = TeamOf(winner);
            var finisher = _gunGame.FinisherId;
            _host.Log.WriteLine(finisher.HasValue
                ? GunGameRulesLog.Finished(finisher.Value, team)
                : GunGameRulesLog.TimeUp(winner?.Id, team));
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // スナップショットの勝者はチームでしか送れないので、勝った人のチームにする。勝った人がもう抜けていれば勝者なしで送る
        public Team? DecideWinner(Team? killWinner)
        {
            return TeamOf(DecideSide());
        }

        public bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            var side = DecideSide();
            return side.HasValue && side.Value.Id == playerId;
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
        }

        // 段の数まで登った値をそのまま送るのは、クライアントがそれで登り切った人を知るため
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                row.GunGameTier = _gunGame.GetTier(row.PlayerId);
            }
        }

        internal static Side SideOf(int playerId) => new Side(playerId);

        // 終わった後は決めた勝者を使い続ける。時間切れの順位は今いる人で数えるので、終わった後に抜けた人で勝者が変わらないように
        private Side? DecideSide()
        {
            if (_ended)
            {
                return _winner;
            }

            return _gunGame.FinisherSide ?? _gunGame.CheckTimeUp(_mode).Winner;
        }

        private Team? TeamOf(Side? side)
        {
            var session = _host.Session;
            if (!side.HasValue || session == null || !session.HasPlayer(side.Value.Id))
            {
                return null;
            }

            return session.GetTeam(side.Value.Id);
        }

        // GunGame は ModeSession が Playing の間のキルしか数えないので、Session が始まった (2 チームがそろった) ら始める
        private void StartModeIfPlaying()
        {
            var session = _host.Session;
            if (_mode.State == GameModeState.Waiting && session != null && session.State == GameModeState.Playing)
            {
                _mode.Start();
            }
        }
    }
}
