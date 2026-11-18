using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    // 数はルールのキルの知らせから自分で数え、試合の終わりに送る。Session の数を読まないのは、途中で抜けた人の数が Session から消えても送れるようにするため。
    internal sealed class ServerStatsModule : IServerModule, IDisposable
    {
        public const string TuningFileName = "leaderboards.json";

        private readonly MatchRulesModule _rules;
        private readonly Func<TextWriter, IStatsIngestBackend> _createBackend;
        private readonly LeaderboardSpec _spec;
        private readonly Func<float> _clock;
        private readonly Action<int> _sleep;
        private readonly ServerMatchStats _stats = new ServerMatchStats();
        private readonly List<string> _assisters = new List<string>();
        private ServerContext _context;
        private ServerMatch _match;
        private StatsIngestQueue _queue;

        public ServerStatsModule(MatchRulesModule rules, Func<TextWriter, IStatsIngestBackend> createBackend, LeaderboardSpec spec)
            : this(rules, createBackend, spec, StopwatchClock(), Thread.Sleep)
        {
        }

        public ServerStatsModule(
            MatchRulesModule rules, Func<TextWriter, IStatsIngestBackend> createBackend, LeaderboardSpec spec, Func<float> clock, Action<int> sleep)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _createBackend = createBackend ?? throw new ArgumentNullException(nameof(createBackend));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
        }

        public string Name => "stats";

        public ServerMatchStats Stats => _stats;

        public StatsIngestQueue Queue => _queue;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var backend = _createBackend(context.Log) ?? throw new InvalidOperationException("the EOS stats factory returned no backend");
            _queue = new StatsIngestQueue(backend, context.Log, _clock, _sleep);
            _match = context.Match;
            _match.Started += HandleStarted;
            _match.Ended += HandleEnded;
            _match.PlayerJoined += HandlePlayerJoined;
            _rules.PlayerKilled += HandleKilled;

            context.Log.WriteLine(StatsIngestLog.On(_spec.Boards.Count));
            foreach (var duplicate in ServerMatchStats.DuplicateStatNames(_spec))
            {
                context.Log.WriteLine(StatsIngestLog.DuplicateStatName(duplicate.StatName, duplicate.Id));
            }

            // ルールより後に登録されて、試合がもう始まっていても数え始める
            if (_match.State == GameModeState.Playing)
            {
                HandleStarted();
            }
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _queue?.Tick();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            Unsubscribe();
            if (_queue == null)
            {
                return;
            }

            try
            {
                _queue.FlushAndWait();
            }
            finally
            {
                _queue.Dispose();
                _queue = null;
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            _queue?.Dispose();
            _queue = null;
        }

        // ファイルが無ければ、Unity の KitTuning と同じく既定のボードで送る。
        public static bool TryLoadSpec(string root, out LeaderboardSpec spec, out string error)
        {
            ArgumentNullException.ThrowIfNull(root);
            var path = Path.GetFullPath(Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, TuningFileName));
            if (!File.Exists(path))
            {
                spec = LeaderboardSpec.Default;
                error = null;
                return true;
            }

            try
            {
                spec = LeaderboardJson.Parse(File.ReadAllText(path), path);
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
            {
                spec = null;
                error = "cannot read leaderboard tuning: " + e.Message;
                return false;
            }
        }

        private void HandleStarted()
        {
            _stats.Clear();
            foreach (var player in _match.Players)
            {
                _stats.Join(player.ProductUserId);
            }
        }

        private void HandlePlayerJoined(ServerPlayer player)
        {
            if (_match.State == GameModeState.Playing)
            {
                _stats.Join(player.ProductUserId);
            }
        }

        private void HandleKilled(KillFeedEntry entry)
        {
            if (_match.State != GameModeState.Playing)
            {
                return;
            }

            _assisters.Clear();
            foreach (var assister in entry.AssisterIds)
            {
                _assisters.Add(UserOf(assister));
            }

            _stats.RecordKill(
                UserOf(entry.KillerId), UserOf(entry.VictimId), _assisters, entry.Headshot, entry.Friendly, entry.IsSuicide);
        }

        // 終わったときにいる人だけを試合に出た・勝ったと数える。途中で抜けた人もそれまでのキルなどは送る
        private void HandleEnded(Team? winner)
        {
            var skipped = 0;
            foreach (var player in _match.Players)
            {
                if (player.IsBot)
                {
                    continue;
                }

                var userId = player.ProductUserId;
                if (userId == null)
                {
                    skipped++;
                    continue;
                }

                _stats.Add(userId, PlayerStat.MatchesPlayed);
                if (winner.HasValue && player.Team == winner.Value)
                {
                    _stats.Add(userId, PlayerStat.MatchesWon);
                }
            }

            var matchId = _match.MatchId;
            var queued = 0;
            foreach (var userId in _stats.Users)
            {
                var ingest = _stats.Build(_spec, userId);
                if (ingest.Count == 0)
                {
                    continue;
                }

                _queue.Enqueue(matchId, userId, ingest);
                queued++;
            }

            _context.Log.WriteLine(queued > 0 || skipped > 0 ? StatsIngestLog.Queued(matchId, queued, skipped) : StatsIngestLog.NothingToSend(matchId));
            _stats.Clear();
        }

        private string UserOf(int playerId)
        {
            return _match.TryGetPlayer(playerId, out var player) ? player.ProductUserId : null;
        }

        private void Unsubscribe()
        {
            if (_match == null)
            {
                return;
            }

            _match.Started -= HandleStarted;
            _match.Ended -= HandleEnded;
            _match.PlayerJoined -= HandlePlayerJoined;
            _rules.PlayerKilled -= HandleKilled;
            _match = null;
        }

        private static Func<float> StopwatchClock()
        {
            var watch = Stopwatch.StartNew();
            return () => (float)watch.Elapsed.TotalSeconds;
        }
    }
}
