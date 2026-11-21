using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // チームは Session の決め方 (切断した人は同じチームへ) を ServerMatch.ChooseTeam に差し込んで 1 つにそろえる。
    // 次の試合は ServerMatch の待ちが終わったときに Session を TeamDeathmatchSession.Restart して始め、
    // Session には待ち時間を渡さずに、待ちを 2 か所で数えないようにする。
    internal sealed partial class MatchRulesModule : IServerModule, ISnapshotContributor
    {
        // 不正な要求の警告を出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _rejectWarningInterval = 1f;

        private readonly Dictionary<int, int> _lastKillers = new Dictionary<int, int>();
        private readonly KilledMessage _killed = new KilledMessage();
        private readonly TeleportedMessage _teleported = new TeleportedMessage();

        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private ServerRulesData _data;
        private TeamDeathmatchSession _session;
        private ServerLoadout _loadout;
        private KillShot? _killShot;
        private float _now;
        private long _tick;

        public event Action<int> PlayerSpawned;

        public event Action MatchStarted;

        // キルフィードを送り、目的に倒れたことを渡した後に出す。自滅と味方撃ちも含む
        public event Action<KillFeedEntry> PlayerKilled;

        public string Name => "rules";

        public TeamDeathmatchSession Session => _session;

        public ServerLoadout Loadout => _loadout;

        public GameRules Rules => _data?.Rules;

        // Start の前は null。目的 (Extraction など) が送り先と調整値を引くための口。目的を作るのは Start の中なので、目的の Start からは引ける
        internal ServerContext Context => _context;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            _data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            var points = PointsOf(context.Map);
            var rules = _data.Rules;
            var settings = _data.Settings;
            _objective = _createObjective(rules.Objective, settings, this)
                ?? throw new InvalidOperationException("no rules were created for the match objective " + rules.Objective);
            CheckOtherMaps();
            PrepareGunGame();
            _objective.UseMap(context.Map, points);
            _session = CreateSession(context.Map, points);

            _loadout = _data.File.CreateLoadout();
            ListenToSession(_session);
            _match.ChooseTeam = ChooseTeam;
            _match.FixedTeam = FixedTeam;
            _match.CheckNextRules = CheckNextRules;
            _match.PlayerJoined += HandlePlayerJoined;
            _match.PlayerLeft += HandlePlayerLeft;
            _match.Restarted += HandleMatchRestarted;
            _match.MapChanged += HandleMapChanged;
            _match.RulesChanged += HandleRulesChanged;
            _match.TeamChanged += HandleTeamChanged;

            _log.WriteLine(MatchLog.Tag + " waiting for players scoreLimit=" + Text(_session.Spec.ScoreLimit)
                + " timeLimit=" + Text(_session.Spec.TimeLimitSeconds) + "s respawn=" + Text(_session.Spec.RespawnSeconds)
                + "s restartDelay=" + Text(_match.RestartDelaySeconds) + "s reconnectGrace=" + Text(settings.ReconnectGraceSeconds)
                + "s spawnPoints=" + Text(points.SpawnPoints.Count));
            _log.WriteLine(MatchLog.Rules(_data.File.Name.Length > 0 ? _data.File.Name : _data.Name, rules));
            _log.WriteLine(NetDownedLog.Settings(rules.Actions, rules.Actions.ReviveRange));
            WarnUnsupportedActions();
            _log.WriteLine(MatchLog.Loadout(_data.File.LoadoutName, _loadout));
            for (var i = 0; i < _loadout.Weapons.Count; i++)
            {
                _log.WriteLine(MatchLog.Weapon(i, _loadout.Weapons[i]));
            }

            if (_loadout.Catalog != null)
            {
                _log.WriteLine(MatchLog.LoadoutCatalog(_data.File.CatalogName, _loadout.Catalog.Count));
            }
            else
            {
                _log.WriteLine(MatchLog.Tag + " no weapon catalog is in rules " + _data.Name + "; every player keeps the default loadout");
            }

            PreparePings(rules);
            PrepareIdle(rules);
            PrepareChat(settings, _data.ChatWords);
            PrepareNames(settings);
            PrepareMapVote();
            PrepareVoteKick();
            PrepareEconomy();
            StartObjective();
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _tick = tick;
            _now += deltaSeconds;

            // 蘇生は Session の Tick より前に進める (前の Tick から進めなかった累計を Tick が 0 に戻すため)
            AdvanceRevives(deltaSeconds);
            _session.Tick(deltaSeconds);
            TickObjective(deltaSeconds);
            TickGunGame();
            TickRound();
            TickIdle(deltaSeconds);
            TickPings();
            TickMapVote(deltaSeconds);
            TickVoteKick(deltaSeconds);
            TickEconomy(deltaSeconds);
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!_match.TryGetPlayer(connection, out var player) || !_session.HasPlayer(player.PlayerId))
            {
                return;
            }

            switch (message)
            {
                case InputMessage input:
                    ObserveInput(player.PlayerId, input);
                    break;
                case FireRequestMessage _:
                    // 捨てる射撃でも操作はしている
                    TouchIdle(player.PlayerId);
                    break;
                case PingRequestMessage ping:
                    ServerPing(player.PlayerId, ping.Direction);
                    break;
                case ReviveRequestMessage revive:
                    ServerRevive(player.PlayerId, revive.TargetPlayerId);
                    break;
                case LoadoutRequestMessage loadout:
                    ServerChooseCosmetic(player, loadout.CosmeticId);
                    ServerChooseLoadout(player, loadout.WeaponIds, loadout.AbilityIds);
                    break;
                case ChatRequestMessage chat:
                    OnChatRequest(player, chat);
                    break;
                case MapVoteRequestMessage vote:
                    OnMapVoteRequest(player, vote);
                    break;
                case SpectateRequestMessage spectate:
                    OnSpectateRequest(player, spectate);
                    break;
                case VoteKickRequestMessage voteKick:
                    OnVoteKickRequest(player, voteKick);
                    break;
                case BuyRequestMessage buy:
                    OnBuyRequest(player, buy);
                    break;
            }
        }

        // 機能ごとの中身を MatchRulesModule.<機能>.cs に分けて書けるよう、イベントにせず partial method にする (書かれていなければ呼び出しごと消える)
        partial void OnChatRequest(ServerPlayer player, ChatRequestMessage request);

        partial void OnMapVoteRequest(ServerPlayer player, MapVoteRequestMessage request);

        partial void OnSpectateRequest(ServerPlayer player, SpectateRequestMessage request);

        partial void OnVoteKickRequest(ServerPlayer player, VoteKickRequestMessage request);

        partial void OnBuyRequest(ServerPlayer player, BuyRequestMessage request);

        partial void PrepareVoteKick();

        partial void TickVoteKick(float deltaSeconds);

        partial void PrepareEconomy();

        partial void TickEconomy(float deltaSeconds);

        partial void OnPlayerJoined(ServerPlayer player);

        partial void OnPlayerLeft(ServerPlayer player);

        partial void OnMatchEnded(Team? winner);

        partial void OnNextMatchStarting();

        public void Stop()
        {
            if (_session == null)
            {
                return;
            }

            _match.PlayerJoined -= HandlePlayerJoined;
            _match.PlayerLeft -= HandlePlayerLeft;
            _match.Restarted -= HandleMatchRestarted;
            _match.MapChanged -= HandleMapChanged;
            _match.RulesChanged -= HandleRulesChanged;
            _match.TeamChanged -= HandleTeamChanged;
            if (_match.ChooseTeam == ChooseTeam)
            {
                _match.ChooseTeam = null;
            }

            if (_match.FixedTeam == FixedTeam)
            {
                _match.FixedTeam = null;
            }

            if (_match.CheckNextRules == CheckNextRules)
            {
                _match.CheckNextRules = null;
            }

            StopEconomy();
            StopObjective();
            StopMapVote();
            StopVoteKick();
            StopNames();
            _nextData = null;
            _session = null;
        }

        // finishesDowned は目的のとどめの技 (IMatchObjectiveRules.DownedFinisher) が与えるダメージ。目的がとどめを技に限るなら、ほかのダメージはダウン中の人に通さない
        public DamageOutcome ApplyDamage(int? attackerId, int victimId, int amount, KillShot? shot = null, bool finishesDowned = false)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            // Session は攻撃者と被害者が同じ Id のダメージを自滅として扱うので、攻撃者のいないダメージはそれに寄せる
            var source = attackerId ?? victimId;
            if (_session == null || !_session.HasPlayer(source) || !_session.HasPlayer(victimId) || IsBetweenRounds || AreNeutral(source, victimId)
                || (!finishesDowned && ShieldsDowned(victimId)))
            {
                return DamageOutcome.Ignored;
            }

            _killShot = shot;
            _damageAttackerId = source;
            _damageVictimId = victimId;
            try
            {
                return _session.ApplyDamage(source, victimId, amount);
            }
            finally
            {
                _killShot = null;
                _damageAttackerId = null;
                _damageVictimId = null;
            }
        }

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (_session == null)
            {
                return;
            }

            FillMatch(ref snapshot.Match);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                FillPlayer(row.PlayerId, ref row);
            }

            snapshot.Owner.RespawnSeconds = RespawnSecondsOf(receiver);
            snapshot.Owner.Money = MoneyOf(receiver);
            FillControlPoints(snapshot.ControlPoints);
            FillObjectiveItems(snapshot.ObjectiveItems);

            // 目的が人ごとの欄 (Gun Game の段など) を書くなら、Session の行を書いた後に任せる
            (_objective as ISnapshotContributor)?.Contribute(snapshot, receiver);
        }

        private int? RespawnSecondsOf(ServerPlayer receiver)
        {
            if (receiver == null || !_session.HasPlayer(receiver.PlayerId))
            {
                return null;
            }

            var remaining = _session.GetRespawnRemaining(receiver.PlayerId);
            return remaining.HasValue ? HudFormat.DisplayedSeconds(remaining.Value) : (int?)null;
        }

        public bool CanFire(int playerId)
        {
            return _session != null && _session.HasPlayer(playerId) && _session.State == GameModeState.Playing
                && _session.IsAlive(playerId) && !_session.IsDowned(playerId) && !IsBetweenRounds;
        }

        public void NotifyFired(int playerId)
        {
            if (_session == null || !_session.HasPlayer(playerId) || !_session.EndSpawnProtection(playerId))
            {
                return;
            }

            _log.WriteLine(MatchLog.Tag + " spawn protection ended player=" + Text(playerId) + " reason=fired");
        }

        public IReadOnlyList<WeaponSpec> WeaponsFor(int playerId)
        {
            var weapons = TryGunGameWeapons(playerId, out var gunGame) ? gunGame : LoadoutFor(playerId);
            return _objective != null ? _objective.FilterWeapons(playerId, weapons) : weapons;
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            var winner = _session.State == GameModeState.Ended ? DecideWinner(_session.Winner) : null;
            snapshot.State = _match.State;
            snapshot.HasWinner = winner.HasValue;
            snapshot.Winner = winner ?? Team.Alpha;
            snapshot.AlphaScore = _session.GetScore(Team.Alpha);
            snapshot.BravoScore = _session.GetScore(Team.Bravo);
            snapshot.RemainingSeconds = (int)Math.Ceiling(_objective.RemainingSeconds ?? _session.RemainingSeconds);
            snapshot.NextMatchSeconds = (int)Math.Ceiling(_match.RestartRemainingSeconds);
            var wave = _session.SecondsUntilNextWave;
            snapshot.NextWaveSeconds = wave.HasValue ? HudFormat.DisplayedSeconds(wave.Value) : 0;
            var synced = MatchRulesSync.From(_session.Spec.Rules);
            synced.WeatherId = _data.Settings.WeatherId;
            synced.TimeOfDayId = _data.Settings.TimeOfDayId;
            snapshot.Rules = synced;
            _objective.FillMatch(ref snapshot);
            FillRound(ref snapshot);
        }

        public bool FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
            if (_session == null || !_session.HasPlayer(playerId))
            {
                return false;
            }

            snapshot.PlayerId = playerId;
            snapshot.Team = _session.GetTeam(playerId);
            snapshot.CosmeticId = CosmeticOf(playerId);
            snapshot.Health = _session.IsAlive(playerId) ? _session.GetHealth(playerId) : 0;
            snapshot.Downed = DownedOf(playerId);
            snapshot.Kills = _session.GetKills(playerId);
            snapshot.Deaths = _session.GetDeaths(playerId);
            snapshot.Assists = _session.GetAssists(playerId);
            snapshot.HasKiller = _lastKillers.TryGetValue(playerId, out var killer);
            snapshot.KillerId = snapshot.HasKiller ? killer : 0;
            snapshot.SpawnProtectionSeconds = HudFormat.DisplayedSeconds(_session.GetSpawnProtectionRemaining(playerId));
            _objective.FillPlayer(playerId, ref snapshot);
            return true;
        }

        private Team ChooseTeam(string rejoinKey, out bool rejoined)
        {
            var held = _session.FindHeld(KeyOf(rejoinKey));
            rejoined = held.HasValue;
            return held.HasValue ? held.Value.Team : _session.NextTeam;
        }

        private void HandlePlayerJoined(ServerPlayer player)
        {
            var playerId = player.PlayerId;
            var key = KeyOf(player.RejoinKey);
            var held = _session.FindHeld(key);
            if (held.HasValue)
            {
                _log.WriteLine(MatchLog.Rejoined(playerId, held.Value));
            }

            _bodies.Add(playerId);
            // 試合の枠がパーティを見て決めたチームは Session の NextTeam と違いうるので、枠のチームをそのまま渡す
            var team = _session.Join(playerId, key, player.Team);
            if (team != player.Team)
            {
                _log.WriteLine(MatchLog.Tag + " player " + Text(playerId) + " joined the rules as " + team + " but the match as " + player.Team);
            }

            PingPlayerJoined(player);
            IdlePlayerJoined(playerId);
            ObjectivePlayerJoined(playerId);
            NamesPlayerJoined(player);
            VoicePlayerJoined(player);
            VoteKickPlayerJoined(player);
            EconomyPlayerJoined(playerId);
            OnPlayerJoined(player);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            var playerId = player.PlayerId;
            _lastKillers.Remove(playerId);
            _loadout.Remove(playerId);
            Abilities.Remove(playerId);
            _cosmetics.Remove(playerId);
            ForgetDowned(playerId);
            PingPlayerLeft(playerId);
            IdlePlayerLeft(playerId);
            _objective.PlayerLeft(playerId);
            ChatPlayerLeft(playerId);
            SpectatePlayerLeft(playerId);
            NamesPlayerLeft(player);
            VoicePlayerLeft(player);
            VoteKickPlayerLeft(playerId);
            EconomyPlayerLeft(playerId);
            OnPlayerLeft(player);
            if (_session.HasPlayer(playerId) && _session.Leave(playerId))
            {
                _log.WriteLine(MatchLog.Held(playerId, _session.ReconnectGraceSeconds));
            }

            _bodies.Remove(playerId);
        }

        // 続く HandleMatchRestarted の Session.Restart が、移った先のチームの地点に出現させてスナップショットに載せる
        private void HandleTeamChanged(ServerPlayer player, Team from)
        {
            var playerId = player.PlayerId;
            if (!_session.HasPlayer(playerId))
            {
                return;
            }

            if (_session.State != GameModeState.Ended)
            {
                _log.WriteLine(MatchLog.Tag + " player " + Text(playerId) + " moved to " + player.Team + " in the match but the rules are " + _session.State);
                return;
            }

            // マーカーは送り先をチームで選ぶので、次の試合で消えるものも移す前に消して、前のチームのマーカーを残さない
            PingMatchRestarted();
            _session.ChangeTeam(playerId, player.Team);
            ObjectivePlayerJoined(playerId);
        }

        private void HandleMatchRestarted()
        {
            OnNextMatchStarting();
            if (_nextData != null)
            {
                UseNextRules();
                return;
            }

            if (_session.State == GameModeState.Ended)
            {
                _session.Restart();
            }
        }

        private void HandleStarted()
        {
            MatchStarted?.Invoke();
        }

        // Session が点数・キル数・時間・体力を戻した直後。続けて点数・各プレイヤー・出現の知らせが来る
        private void HandleRestarted()
        {
            ClearMatchState();
            _objective.MatchRestarted();
            SpectateMatchRestarted();
        }

        private void ClearMatchState()
        {
            _loadout.ResetBudgets();
            _lastKillers.Clear();
            ClearDowned();
            PingMatchRestarted();
            EconomyMatchRestarted();
        }

        // ラウンド制のように目的が試合の点数を書くなら、キルの点数では書き換えない
        private void HandleScoreChanged(int alpha, int bravo)
        {
            if (_objective.KeepsMatchScore)
            {
                return;
            }

            _match.SetScores(alpha, bravo);
            _log.WriteLine(MatchLog.Score(alpha, bravo));
        }

        // Session は Killed の直後に倒された人の PlayerChanged と DownedChanged を出すので、ここで記録すればそこで引ける
        private void HandleKilled(int killerId, int victimId, IReadOnlyList<int> assisterIds)
        {
            _lastKillers[victimId] = killerId;
            var byAlly = killerId != victimId && !AreHostile(killerId, victimId);
            _log.WriteLine(byAlly ? MatchLog.TeamKill(killerId, victimId) : MatchLog.Kill(killerId, victimId, assisterIds));
            _match.WriteKill(killerId, victimId, byAlly, assisterIds);
            var entry = KillFeedEntryFor(killerId, victimId, byAlly, _killShot, assisterIds);
            SendKill(entry);

            // 目的がこのキルでラウンドを終えて額を渡す前に、キルの額と失う武器を決める
            EconomyPlayerKilled(killerId, victimId);
            _objective.PlayerKilled(killerId, victimId);
            CreditGunGameKill(entry);
            SpectatePlayerKilled(victimId);
            PlayerKilled?.Invoke(entry);
        }

        private void HandleEnded(Team? winner)
        {
            winner = DecideWinner(winner);
            _objective.MatchEnded();
            _match.End(winner);
            _reviveHolds.Clear();
            OnMatchEnded(winner);
        }

        private void HandleHeldExpired(HeldPlayer held)
        {
            _log.WriteLine(MatchLog.HoldExpired(held.PlayerId));
        }

        private void HandleSpawned(int playerId, SpawnPoint point)
        {
            var afterDeath = TakeRespawningAfterDeath(playerId);
            SpectatePlayerSpawned(playerId);
            if (!_bodies.TryGet(playerId, out var body))
            {
                return;
            }

            var position = point.Position;
            var yaw = body.Yaw;
            if (afterDeath)
            {
                var anchor = ChooseRespawnAnchor(playerId, ref position, ref yaw);
                _log.WriteLine(MatchLog.Spawn(playerId, position, anchor));
            }
            else
            {
                _log.WriteLine(MatchLog.Spawn(playerId, point));
            }

            _bodies.Teleport(playerId, position, yaw, _tick);
            _teleported.PlayerId = playerId;
            _teleported.Tick = (int)Math.Min(_tick, int.MaxValue);
            _teleported.Position = position;
            _teleported.Yaw = yaw;
            SendToAll(TransportChannel.Reliable, _teleported);

            var protection = _session.GetSpawnProtectionRemaining(playerId);
            if (protection > 0f)
            {
                _log.WriteLine(MatchLog.Tag + " spawn protection player=" + Text(playerId)
                    + " seconds=" + protection.ToString("0.##", CultureInfo.InvariantCulture));
            }

            // 目的が配る物を決める (出撃で持ち物庫から取るなど) のを、配るより先に済ませる
            _objective?.PlayerSpawning(playerId);

            // 選んだ並びはここで初めて配る (倒れる前・試合の途中では持ち替えない)
            PlayerSpawned?.Invoke(playerId);
        }

        private void SendKill(KillFeedEntry entry)
        {
            _killed.KillerId = entry.KillerId;
            _killed.VictimId = entry.VictimId;
            _killed.WeaponId = entry.WeaponId;
            _killed.Headshot = entry.Headshot;
            _killed.Friendly = entry.Friendly;
            _killed.ClearAssisters();
            var assisters = entry.AssisterIds;
            // 載せきれない分は例外にせず落とす。並びは与えた量の多い順なので落ちるのは寄与の小さい人で、点数とログには全員が残る
            var count = Math.Min(assisters.Count, KilledMessage.MaxAssisters);
            for (var i = 0; i < count; i++)
            {
                _killed.AddAssister(assisters[i]);
            }

            SendToAll(TransportChannel.Reliable, _killed);
        }

        internal static KillFeedEntry KillFeedEntryFor(
            int killerId, int victimId, bool friendly, KillShot? shot, IReadOnlyList<int> assisterIds = null)
        {
            if (!shot.HasValue || shot.Value.ShooterId != killerId)
            {
                return new KillFeedEntry(killerId, victimId, null, headshot: false, friendly, assisterIds);
            }

            var byShot = victimId == shot.Value.TargetId;
            if (!byShot && victimId != killerId)
            {
                return new KillFeedEntry(killerId, victimId, null, headshot: false, friendly, assisterIds);
            }

            return new KillFeedEntry(
                killerId, victimId, shot.Value.WeaponId, byShot && shot.Value.Zone == HitZone.Head, friendly, assisterIds);
        }

        private void SendToAll(TransportChannel channel, IMessage message)
        {
            foreach (var player in _match.Players)
            {
                _context.Messenger.Send(player.Connection, channel, message);
            }
        }

        private void SendToTeam(Team team, TransportChannel channel, IMessage message)
        {
            foreach (var player in _match.Players)
            {
                if (_session.HasPlayer(player.PlayerId) && _session.GetTeam(player.PlayerId) == team)
                {
                    _context.Messenger.Send(player.Connection, channel, message);
                }
            }
        }

        private int CountTeam(Team team)
        {
            var count = 0;
            foreach (var player in _match.Players)
            {
                if (_session.HasPlayer(player.PlayerId) && _session.GetTeam(player.PlayerId) == team)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector3 PositionOf(int playerId)
        {
            return _bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
        }

        // 承認のデータで鍵を送らなかった人は空文字で届くが、Session は null を「成績を残さない」とする
        private static string KeyOf(string rejoinKey)
        {
            return string.IsNullOrEmpty(rejoinKey) ? null : rejoinKey;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }

    internal readonly struct KillShot
    {
        public KillShot(int shooterId, int targetId, string weaponId, HitZone zone)
        {
            ShooterId = shooterId;
            TargetId = targetId;
            WeaponId = weaponId ?? string.Empty;
            Zone = zone;
        }

        public int ShooterId { get; }

        public int TargetId { get; }

        public string WeaponId { get; }

        public HitZone Zone { get; }
    }
}
