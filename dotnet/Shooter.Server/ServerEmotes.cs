using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // エモートの要求 (EmoteRequestMessage) を受け、出せるかをサーバーの知る状態とルールで決めて、始めたことと止めたことを全員へ配る (EmoteStateMessage)。
    // 止める条件はオフラインの EmoteController と同じ (動いた・撃った・倒れた・ダウンした・乗った・長さを終えた・ルールが Off になった)。
    // アニメーションはサーバーに無いので、ここは Id と時間だけを持つ。
    internal sealed class ServerEmotes : IServerModule
    {
        // server-data/tuning の名前。Unity の Resources/Tuning/emotes.json (EmoteJson.ResourcePath) を ServerRulesExporter が同じ名前で写す
        public const string EmotesFileName = "emotes.json";

        // 要求ごとに全員へ配るので、連打する 1 人で全員の回線を埋めさせないための最短の間隔 (秒)。
        // 調整値に出さないのは、遊びの手触りではなく回線を守る下限で、人が輪から選ぶ速さより十分短いため
        internal const float MinRequestIntervalSeconds = 0.25f;

        // 断った理由のログを出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerMovement _movement;
        private readonly ServerCheatSignals _signals;
        private readonly IReadOnlyList<EmoteSpec> _emotes;
        private readonly string _emotesPath;
        private readonly Dictionary<int, Performer> _performers = new Dictionary<int, Performer>();
        private readonly List<int> _scratch = new List<int>();
        private readonly EmoteStateMessage _state = new EmoteStateMessage();
        private ServerContext _context;
        private TextWriter _log;
        private double _now;
        private double _nextRefusalLogTime = double.NegativeInfinity;

        // 撃ったら止めるので、戦闘が射撃を受け付けたことを signals で知る。ServerModules が戦闘へ渡すのと同じものを渡す
        public ServerEmotes(
            MatchRulesModule rules, ServerMovement movement, ServerCheatSignals signals, IReadOnlyList<EmoteSpec> emotes, string emotesPath = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            _emotes = EmoteSpec.UniqueById(emotes ?? throw new ArgumentNullException(nameof(emotes)));
            _emotesPath = emotesPath;
        }

        public string Name => "emotes";

        public IReadOnlyList<EmoteSpec> Emotes => _emotes;

        private bool IsOn => _rules.Rules?.Actions.Emote == Emote.On;

        public static bool TryLoadEmotes(
            IReadOnlyList<string> searchRoots, out IReadOnlyList<EmoteSpec> emotes, out string path, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            emotes = EmoteSpec.DefaultEmotes;
            path = null;
            foreach (var root in searchRoots)
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, EmotesFileName));
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    emotes = EmoteJson.Parse(File.ReadAllText(candidate), candidate);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    emotes = EmoteSpec.DefaultEmotes;
                    error = "cannot read emote tuning: " + e.Message;
                    return false;
                }

                path = candidate;
                error = null;
                return true;
            }

            // エモートはゲームがアニメーションを用意して初めて意味を持つので、ファイルが無ければオフラインの既定と同じく空の一覧で動かす
            error = null;
            return true;
        }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            context.Match.PlayerJoined += HandlePlayerJoined;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            _rules.PlayerSpawned += HandlePlayerSpawned;
            _rules.MatchStarted += HandleMatchStarted;
            _rules.RulesChanged += HandleRulesChanged;
            _signals.ShotAccepted += HandleShotAccepted;
            _log.WriteLine(ServerEmotesLog.Started(_rules.Rules?.Actions.Emote ?? Emote.Off, _emotes, _emotesPath));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            if (_performers.Count == 0)
            {
                return;
            }

            // 止めると一覧から外すので、回している間に書き換えないよう先に写す
            _scratch.Clear();
            _scratch.AddRange(_performers.Keys);
            foreach (var playerId in _scratch)
            {
                if (!_performers.TryGetValue(playerId, out var performer) || performer.Current == null)
                {
                    continue;
                }

                var reason = StopReasonOf(performer);
                if (reason.HasValue)
                {
                    StopCurrent(performer, reason.Value);
                }
            }

            _scratch.Clear();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is EmoteRequestMessage request) || !_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            if (request.Action == EmoteAction.Stop)
            {
                if (_performers.TryGetValue(player.PlayerId, out var performer))
                {
                    StopCurrent(performer, EmoteStopReason.Cancelled);
                }

                return;
            }

            Play(player.PlayerId, request.EmoteId);
        }

        public void Stop()
        {
            if (_context == null)
            {
                return;
            }

            _context.Match.PlayerJoined -= HandlePlayerJoined;
            _context.Match.PlayerLeft -= HandlePlayerLeft;
            _context.Match.MapChanged -= HandleMapChanged;
            _rules.PlayerSpawned -= HandlePlayerSpawned;
            _rules.MatchStarted -= HandleMatchStarted;
            _rules.RulesChanged -= HandleRulesChanged;
            _signals.ShotAccepted -= HandleShotAccepted;
            _performers.Clear();
        }

        internal bool IsPlaying(int playerId, out string emoteId)
        {
            emoteId = _performers.TryGetValue(playerId, out var performer) ? performer.Current?.Id : null;
            return emoteId != null;
        }

        internal EmoteRefusal Play(int playerId, string emoteId)
        {
            var performer = GetOrAdd(playerId);

            // 断った要求も間隔に数えるのは、断られる要求の連打もログと判定の手間を生むため
            var tooSoon = _now < performer.NextRequestTime;
            performer.NextRequestTime = _now + MinRequestIntervalSeconds;
            EmoteSpec emote = null;
            var refusal = tooSoon ? EmoteRefusal.TooSoon : Check(playerId, emoteId, out emote);
            if (refusal != EmoteRefusal.None)
            {
                Refuse(playerId, emoteId, refusal);
                return refusal;
            }

            if (performer.Current != null)
            {
                // 次の始まりを受けたクライアントは前のエモートを止めて替えるので、止めたことは配らずにログにだけ残す
                _log.WriteLine(ServerEmotesLog.Stopped(playerId, performer.Current.Id, EmoteStopReason.Replaced, Elapsed(performer)));
            }

            performer.Current = emote;
            performer.StartedAt = _now;
            performer.StartPosition = _context.Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            FillPlaying(playerId, emote.Id);
            var recipients = SendToAll(_state);
            _log.WriteLine(ServerEmotesLog.Played(playerId, emote, recipients));
            return EmoteRefusal.None;
        }

        // ダウンは体力 0 で倒れた後の状態で、生きていない扱いにもなり得るので、先に見ないと Downed が出ない (EmoteController と同じ順)
        private EmoteRefusal Check(int playerId, string emoteId, out EmoteSpec emote)
        {
            emote = null;
            if (!IsOn)
            {
                return EmoteRefusal.RuleOff;
            }

            emote = EmoteSpec.Find(_emotes, emoteId);
            if (emote == null)
            {
                return EmoteRefusal.UnknownId;
            }

            var session = _rules.Session;
            if (session != null && session.HasPlayer(playerId) && session.IsDowned(playerId))
            {
                return EmoteRefusal.Downed;
            }

            if (session == null
                || !session.HasPlayer(playerId)
                || !session.IsAlive(playerId)
                || !_context.Bodies.TryGet(playerId, out var body)
                || !body.HasSpawned)
            {
                return EmoteRefusal.Dead;
            }

            return _movement.IsRiding(playerId) ? EmoteRefusal.Riding : EmoteRefusal.None;
        }

        private EmoteStopReason? StopReasonOf(Performer performer)
        {
            var playerId = performer.PlayerId;
            if (!IsOn)
            {
                return EmoteStopReason.RuleOff;
            }

            var session = _rules.Session;
            if (session != null && session.HasPlayer(playerId) && session.IsDowned(playerId))
            {
                return EmoteStopReason.Downed;
            }

            if (session == null || !session.HasPlayer(playerId) || !session.IsAlive(playerId))
            {
                return EmoteStopReason.Died;
            }

            if (_movement.IsRiding(playerId))
            {
                return EmoteStopReason.Riding;
            }

            if (performer.Current.StopsOnMove && HasMoved(performer))
            {
                return EmoteStopReason.Moved;
            }

            return performer.Current.IsFinishedAt(Elapsed(performer)) ? EmoteStopReason.Finished : (EmoteStopReason?)null;
        }

        // オフラインと同じく水平の動きだけを見る。落ちたり跳ねたりの上下では止めない
        private bool HasMoved(Performer performer)
        {
            if (!_context.Bodies.TryGet(performer.PlayerId, out var body))
            {
                return false;
            }

            var offset = body.Position - performer.StartPosition;
            var horizontal = (offset.X * offset.X) + (offset.Z * offset.Z);
            return horizontal > EmoteSpec.MoveToleranceMeters * EmoteSpec.MoveToleranceMeters;
        }

        private void StopCurrent(Performer performer, EmoteStopReason reason)
        {
            if (performer.Current == null)
            {
                return;
            }

            var stopped = performer.Current;
            var elapsed = Elapsed(performer);
            performer.Current = null;
            _state.PlayerId = performer.PlayerId;
            _state.Playing = false;
            _state.EmoteId = "";
            _state.StopReason = reason;
            SendToAll(_state);
            _log.WriteLine(ServerEmotesLog.Stopped(performer.PlayerId, stopped.Id, reason, elapsed));
        }

        private void StopAll(EmoteStopReason reason)
        {
            foreach (var performer in _performers.Values)
            {
                StopCurrent(performer, reason);
            }
        }

        private float Elapsed(Performer performer)
        {
            return (float)Math.Max(0d, _now - performer.StartedAt);
        }

        private void FillPlaying(int playerId, string emoteId)
        {
            _state.PlayerId = playerId;
            _state.Playing = true;
            _state.EmoteId = emoteId;
            _state.StopReason = default;
        }

        private int SendToAll(IMessage message)
        {
            var recipients = 0;
            foreach (var player in _context.Match.Players)
            {
                if (player.IsBot)
                {
                    continue;
                }

                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, message);
                recipients++;
            }

            return recipients;
        }

        private void Refuse(int playerId, string emoteId, EmoteRefusal reason)
        {
            if (_now < _nextRefusalLogTime)
            {
                return;
            }

            _nextRefusalLogTime = _now + _refusalLogInterval;
            _log.WriteLine(ServerEmotesLog.Refused(playerId, emoteId, reason));
        }

        private Performer GetOrAdd(int playerId)
        {
            if (!_performers.TryGetValue(playerId, out var performer))
            {
                performer = new Performer(playerId);
                _performers.Add(playerId, performer);
            }

            return performer;
        }

        // 途中から入った人にも、続いているエモート (繰り返すものなど) を見せる
        private void HandlePlayerJoined(ServerPlayer joined)
        {
            foreach (var performer in _performers.Values)
            {
                if (performer.Current == null || performer.PlayerId == joined.PlayerId)
                {
                    continue;
                }

                FillPlaying(performer.PlayerId, performer.Current.Id);
                _context.Messenger.Send(joined.Connection, TransportChannel.Reliable, _state);
            }
        }

        // 抜けた人の体はクライアントが消すので、止めたことは配らない
        private void HandlePlayerLeft(ServerPlayer player)
        {
            _performers.Remove(player.PlayerId);
        }

        private void HandlePlayerSpawned(int playerId)
        {
            if (_performers.TryGetValue(playerId, out var performer))
            {
                StopCurrent(performer, EmoteStopReason.Cancelled);
            }
        }

        private void HandleMatchStarted()
        {
            StopAll(EmoteStopReason.Cancelled);
        }

        private void HandleMapChanged(ServerMapData map)
        {
            StopAll(EmoteStopReason.Cancelled);
        }

        private void HandleRulesChanged(ServerRulesData data)
        {
            var rule = data.Rules.Actions.Emote;
            if (rule != Emote.On)
            {
                StopAll(EmoteStopReason.RuleOff);
            }

            _log.WriteLine(ServerEmotesLog.Started(rule, _emotes, _emotesPath));
        }

        private void HandleShotAccepted(int shooterId, Vector3 origin, Vector3 direction, float shotTick)
        {
            if (_performers.TryGetValue(shooterId, out var performer))
            {
                StopCurrent(performer, EmoteStopReason.Fired);
            }
        }

        private sealed class Performer
        {
            public Performer(int playerId)
            {
                PlayerId = playerId;
            }

            public int PlayerId { get; }

            public EmoteSpec Current { get; set; }

            public double StartedAt { get; set; }

            public Vector3 StartPosition { get; set; }

            public double NextRequestTime { get; set; } = double.NegativeInfinity;
        }
    }
}
