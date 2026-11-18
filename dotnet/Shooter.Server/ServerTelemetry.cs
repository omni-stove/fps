using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // サーバーには設定の画面が無いので、同意は運用する人が起動で決めた値を受け取る。同意が無ければクライアントと同じく何も残さない。
    // 試合の長さは tick の秒数の和で数える。時計の飛びに左右されず、テストから時間を進められるようにするため。
    internal sealed class ServerTelemetry : IServerModule, IDisposable
    {
        public const string TuningFileName = "telemetry.json";

        private readonly TelemetrySpec _spec;
        private readonly string _folder;
        private readonly bool _consent;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<double> _random;
        private readonly Func<Action<string>, ITelemetrySink> _createSink;
        private ServerContext _context;
        private ServerMatch _match;
        private TelemetryRecorder _recorder;
        private bool _hooked;
        private double _now;
        private double? _startedAt;

        public ServerTelemetry(
            TelemetrySpec spec,
            string folder,
            bool consent,
            Func<DateTime> utcNow,
            Func<double> random = null,
            HttpMessageHandler handler = null)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (spec.Sink == TelemetrySink.LocalFile && string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("folder must not be empty for the LocalFile sink", nameof(folder));
            }

            _folder = string.IsNullOrWhiteSpace(folder) ? folder : System.IO.Path.GetFullPath(folder);
            _consent = consent;
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _random = random ?? new Random().NextDouble;
            // テストから渡す送り口は呼んだ側が閉じるので、ここでは持ち主にならない
            _createSink = log => TelemetrySinks.Create(_spec, _folder, log, handler);
        }

        public string Name => "telemetry";

        public TelemetryRecorder Recorder => _recorder;

        public string Path => _spec.Sink == TelemetrySink.LocalFile ? System.IO.Path.Combine(_folder, _spec.FileName) : null;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (_recorder != null)
            {
                throw new InvalidOperationException("the telemetry is already started");
            }

            var origin = new TelemetryOrigin(
                TelemetryOrigin.ServerSource, TelemetryOrigin.NewSession(), context.Version, RuntimeInformation.RuntimeIdentifier);
            _recorder = new TelemetryRecorder(_spec, origin, _consent, () => _createSink(WriteLog), _utcNow, _random, WriteLog);

            _match = context.Match;
            _match.Started += HandleStarted;
            _match.Ended += HandleEnded;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandled;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTask;
            _hooked = true;

            // ほかのモジュールより後に登録されて、試合がもう始まっていても長さを数え始める
            if (_match.State == GameModeState.Playing)
            {
                HandleStarted();
            }
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _now += deltaSeconds;
            _recorder?.Tick(deltaSeconds);
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
            Unhook();
            var recorder = _recorder;
            _recorder = null;
            recorder?.Dispose();
        }

        public void Dispose()
        {
            Unhook();
            _recorder?.Dispose();
            _recorder = null;
        }

        // プロセスが終わる直前に呼ばれうるので、終わるときは送り先へ届け切るまで待つ
        public bool RecordUnhandled(Exception exception, bool terminating)
        {
            ArgumentNullException.ThrowIfNull(exception);
            var recorder = _recorder;
            if (recorder == null)
            {
                return false;
            }

            var recorded = recorder.RecordCrash(exception, terminating);
            if (recorded && terminating)
            {
                recorder.Flush(true);
            }

            return recorded;
        }

        // ファイルが無ければ、Unity の KitTuning と同じく既定の値 (手元の JSON Lines) で残す。
        public static bool TryLoadSpec(string root, out TelemetrySpec spec, out string error)
        {
            ArgumentNullException.ThrowIfNull(root);
            var path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, TuningFileName));
            if (!File.Exists(path))
            {
                spec = TelemetrySpec.Default;
                error = null;
                return true;
            }

            try
            {
                spec = TelemetryJson.Parse(File.ReadAllText(path), path);
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
            {
                spec = null;
                error = "cannot read telemetry tuning: " + e.Message;
                return false;
            }
        }

        private void HandleStarted()
        {
            _startedAt = _now;
        }

        private void HandleEnded(Team? winner)
        {
            var recorder = _recorder;
            var match = _match;
            // 始まりを見ていない試合は長さが分からないので、誤った長さで残さない
            if (recorder == null || match == null || !_startedAt.HasValue)
            {
                return;
            }

            var duration = (float)Math.Max(0d, _now - _startedAt.Value);
            _startedAt = null;
            var scores = new[]
            {
                new SideScore(Side.Alpha, match.AlphaScore),
                new SideScore(Side.Bravo, match.BravoScore),
            };
            var summary = new TelemetryMatchSummary(
                match.MatchId,
                _context.Rules?.Rules.Objective.ToString(),
                _context.Map.Name,
                duration,
                scores,
                Side.FromTeam(winner),
                humans: match.HumanCount,
                bots: match.BotCount);
            recorder.RecordMatchSummary(summary);
        }

        private void HandleUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                RecordUnhandled(exception, args.IsTerminating);
            }
        }

        private void HandleUnobservedTask(object sender, UnobservedTaskExceptionEventArgs args)
        {
            foreach (var inner in args.Exception.Flatten().InnerExceptions)
            {
                RecordUnhandled(inner, false);
            }
        }

        // ログの書き手はメインのループと送信のスレッドの両方から呼ばれるので、まとめて 1 行ずつ書く
        private void WriteLog(string line)
        {
            var log = _context?.Log;
            if (log == null)
            {
                return;
            }

            lock (log)
            {
                log.WriteLine(line);
            }
        }

        private void Unhook()
        {
            if (!_hooked)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandled;
            TaskScheduler.UnobservedTaskException -= HandleUnobservedTask;
            if (_match != null)
            {
                _match.Started -= HandleStarted;
                _match.Ended -= HandleEnded;
            }

            _match = null;
            _hooked = false;
        }
    }
}
