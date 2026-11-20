using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 通報が届いてから記録し始めても、通報された動きはもう過ぎているので、人ごとに直前の数秒を常に持っておく。
    // 時刻は tick の秒数の和で数える。通報の間引き (ServerReports) と同じく、時計の飛びに左右されず、テストから時間を進められるようにするため。
    // Bot は通報できない (ServerReports が断る) ので持たない。
    internal sealed class ServerReportEvidence : IServerModule, IReportEvidence
    {
        // 入力は移動の予算で tick の数までに抑えられ、射撃と命中もそれを大きく超えない。
        // 秒数を長く設定されても 1 人の記録で膨らみ続けないよう、この数を上限にして古いものから上書きする
        public const int MaxRecordsPerSecond = TickClock.DefaultTickRate * 4;

        private const int _initialCapacity = 64;
        private const double _filesWindowSeconds = 60d;

        private readonly ServerCheatSignals _signals;
        private readonly Dictionary<int, Ring> _byPlayer = new Dictionary<int, Ring>();
        private readonly int _maxRecords;
        private readonly Queue<double> _describedAt = new Queue<double>();
        private ServerContext _context;
        private bool _listening;
        private double _now;

        public ServerReportEvidence(ServerCheatSignals signals, AntiCheatTuning.EvidenceSettings settings)
        {
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _maxRecords = (int)Math.Min(int.MaxValue / 2, Math.Ceiling(settings.Seconds * (double)MaxRecordsPerSecond));
        }

        public string Name => "report-evidence";

        public AntiCheatTuning.EvidenceSettings Settings { get; }

        public int HeldPlayerCount => _byPlayer.Count;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Settings.Enabled)
            {
                context.Log.WriteLine(ServerReportEvidenceLog.Off());
                return;
            }

            if (!_listening)
            {
                _signals.Looked += HandleLooked;
                _signals.ShotAccepted += HandleShotAccepted;
                _signals.Hit += HandleHit;
                _listening = true;
            }

            context.Log.WriteLine(ServerReportEvidenceLog.Holding(Settings.Seconds));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (!_listening || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _now += deltaSeconds;
            foreach (var ring in _byPlayer.Values)
            {
                ring.DropBefore(_now - Settings.Seconds);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        // 接続の番号は使い回されうるので、抜けた人の記録を残すと、次にその番号で入った人の証拠に混ざる
        public void OnDisconnected(ConnectionId connection)
        {
            if (ServerMatch.TryGetPlayerId(connection, out var playerId))
            {
                _byPlayer.Remove(playerId);
            }
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (_listening)
            {
                _signals.Looked -= HandleLooked;
                _signals.ShotAccepted -= HandleShotAccepted;
                _signals.Hit -= HandleHit;
                _listening = false;
            }

            _byPlayer.Clear();
            _describedAt.Clear();
        }

        public string Describe(int targetId)
        {
            if (!_listening)
            {
                return null;
            }

            while (_describedAt.Count > 0 && _now - _describedAt.Peek() >= _filesWindowSeconds)
            {
                _describedAt.Dequeue();
            }

            if (_describedAt.Count >= Settings.MaxFilesPerMinute)
            {
                _context.Log.WriteLine(ServerReportEvidenceLog.Limited(targetId, Settings.MaxFilesPerMinute));
                return null;
            }

            _describedAt.Enqueue(_now);

            if (!_byPlayer.TryGetValue(targetId, out var ring))
            {
                return string.Empty;
            }

            ring.DropBefore(_now - Settings.Seconds);
            var text = new StringBuilder();
            for (var i = 0; i < ring.Count; i++)
            {
                text.Append(Format(ring[i])).Append('\n');
            }

            return text.ToString();
        }

        private string Format(in Record record)
        {
            var ago = _now - record.At;
            switch (record.Kind)
            {
                case RecordKind.Look:
                    return ServerReportEvidenceLog.Look(ago, record.Tick, record.HasPosition ? record.First : (Vector3?)null, record.Yaw, record.Pitch);
                case RecordKind.Shot:
                    return ServerReportEvidenceLog.Shot(ago, record.Tick, record.First, record.Second);
                case RecordKind.Hit:
                    return ServerReportEvidenceLog.Hit(ago, record.Victim, record.Zone, record.Tick);
                default:
                    throw new InvalidOperationException("unknown evidence record " + record.Kind);
            }
        }

        private void HandleLooked(int playerId, int tick, float yaw, float? pitch)
        {
            // 位置は移動が共有の体へ写した後に出るので、この tick の入力を当てた後の位置になる
            var hasBody = _context.Bodies.TryGet(playerId, out var body);
            Add(playerId, new Record
            {
                Kind = RecordKind.Look,
                Tick = tick,
                HasPosition = hasBody,
                First = hasBody ? body.Position : Vector3.Zero,
                Yaw = yaw,
                Pitch = pitch,
            });
        }

        private void HandleShotAccepted(int shooterId, Vector3 origin, Vector3 direction, float shotTick)
        {
            Add(shooterId, new Record { Kind = RecordKind.Shot, Tick = shotTick, First = origin, Second = direction });
        }

        // 撃たれた側ではなく撃った側に残す。証拠は通報された人がしたことなので
        private void HandleHit(int shooterId, int victimId, HitZone zone, float rewindTicks)
        {
            Add(shooterId, new Record { Kind = RecordKind.Hit, Victim = victimId, Zone = zone, Tick = rewindTicks });
        }

        private void Add(int playerId, Record record)
        {
            if (BotRoster.IsBotId(playerId))
            {
                return;
            }

            if (!_byPlayer.TryGetValue(playerId, out var ring))
            {
                ring = new Ring(Math.Min(_initialCapacity, _maxRecords), _maxRecords);
                _byPlayer.Add(playerId, ring);
            }

            record.At = _now;
            ring.DropBefore(_now - Settings.Seconds);
            ring.Add(record);
        }

        private enum RecordKind
        {
            Look,
            Shot,
            Hit,
        }

        // 種類ごとに別の型にしないのは、毎 tick の向きの記録で 1 件ずつ箱を作らずに済むようにするため。
        // Tick は向きでは入力の tick、射撃では撃った tick、命中では巻き戻した tick の数
        private struct Record
        {
            public RecordKind Kind;
            public double At;
            public double Tick;
            public bool HasPosition;
            public Vector3 First;
            public Vector3 Second;
            public float Yaw;
            public float? Pitch;
            public int Victim;
            public HitZone Zone;
        }

        // 秒数の分だけを持つので、要る数は入力の頻度で変わる。最初から上限の大きさで取ると、短い試合でも人数分の大きな配列が残るので、倍にしながら広げる
        private sealed class Ring
        {
            private readonly int _max;
            private Record[] _items;
            private int _head;

            public Ring(int capacity, int max)
            {
                _items = new Record[Math.Max(1, capacity)];
                _max = Math.Max(1, max);
            }

            public int Count { get; private set; }

            public ref readonly Record this[int index] => ref _items[(_head + index) % _items.Length];

            public void Add(Record record)
            {
                if (Count == _items.Length)
                {
                    if (_items.Length < _max)
                    {
                        Grow();
                    }
                    else
                    {
                        _head = (_head + 1) % _items.Length;
                        Count--;
                    }
                }

                _items[(_head + Count) % _items.Length] = record;
                Count++;
            }

            public void DropBefore(double oldest)
            {
                while (Count > 0 && _items[_head].At < oldest)
                {
                    _items[_head] = default;
                    _head = (_head + 1) % _items.Length;
                    Count--;
                }
            }

            private void Grow()
            {
                var grown = new Record[(int)Math.Min(_max, (long)_items.Length * 2)];
                for (var i = 0; i < Count; i++)
                {
                    grown[i] = _items[(_head + i) % _items.Length];
                }

                _items = grown;
                _head = 0;
            }
        }
    }
}
