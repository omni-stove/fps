using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 通報は運営があとで読むものなので、標準出力のイベントの JSON とは別のファイルに 1 件 1 行で足していく。
    // 間引く間隔を tick の秒数の和で数えるのは、時計の飛びに左右されず、テストから時間を進められるようにするため。
    internal sealed class ServerReports : IServerModule, IDisposable
    {
        public const string DefaultFileName = "reports.jsonl";

        public const double DefaultRepeatSeconds = 300;

        private readonly Func<DateTime> _utcNow;
        private readonly Func<string> _newReportId;
        private readonly Dictionary<(int Reporter, int Target), double> _savedAt = new Dictionary<(int Reporter, int Target), double>();
        private readonly List<(int Reporter, int Target)> _expired = new List<(int Reporter, int Target)>();
        private ServerContext _context;
        private StreamWriter _writer;
        private double _now;

        // newReportId を受け取るのは、テストが証拠のファイルの名前を決め打ちで確かめられるようにするため
        public ServerReports(
            string path,
            Func<DateTime> utcNow,
            double repeatSeconds = DefaultRepeatSeconds,
            IReportEvidence evidence = null,
            Func<string> newReportId = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be empty", nameof(path));
            }

            if (!(repeatSeconds >= 0d) || double.IsInfinity(repeatSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(repeatSeconds), repeatSeconds, "repeatSeconds must be finite and not negative");
            }

            Path = System.IO.Path.GetFullPath(path);
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            RepeatSeconds = repeatSeconds;
            Evidence = evidence;
            _newReportId = newReportId ?? ServerReportLog.NewReportId;
        }

        public string Name => "reports";

        public string Path { get; }

        public double RepeatSeconds { get; }

        public IReportEvidence Evidence { get; }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (_writer != null)
            {
                throw new InvalidOperationException("the report file is already open");
            }

            // 書けない置き場所のまま試合を始めると、通報が届くたびに黙って消えるので、起動で止める
            try
            {
                var folder = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                _writer = new StreamWriter(Path, true, new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidOperationException(ServerReportLog.CannotOpen(Path, e.Message), e);
            }

            context.Log.WriteLine(ServerReportLog.Opened(Path, RepeatSeconds));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _now += deltaSeconds;
            _expired.Clear();
            foreach (var pair in _savedAt)
            {
                if (_now - pair.Value >= RepeatSeconds)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (var key in _expired)
            {
                _savedAt.Remove(key);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        // 接続の番号は使い回されうるので、抜けた人の間引きを残すと、次にその番号で入った人の通報まで落としてしまう。
        public void OnDisconnected(ConnectionId connection)
        {
            if (!ServerMatch.TryGetPlayerId(connection, out var playerId))
            {
                return;
            }

            _expired.Clear();
            foreach (var key in _savedAt.Keys)
            {
                if (key.Reporter == playerId)
                {
                    _expired.Add(key);
                }
            }

            foreach (var key in _expired)
            {
                _savedAt.Remove(key);
            }
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is ReportRequestMessage report) || _context == null)
            {
                return;
            }

            var log = _context.Log;
            if (!_context.Match.TryGetPlayer(connection, out var reporter))
            {
                log.WriteLine(ServerReportLog.NotJoined(connection.Value));
                return;
            }

            var targetId = report.TargetPlayerId;
            if (targetId == reporter.PlayerId)
            {
                log.WriteLine(ServerReportLog.Refused(reporter.PlayerId, targetId, ServerReportRefusal.Self));
                return;
            }

            if (BotRoster.IsBotId(targetId))
            {
                log.WriteLine(ServerReportLog.Refused(reporter.PlayerId, targetId, ServerReportRefusal.Bot));
                return;
            }

            if (!_context.Match.TryGetPlayer(targetId, out var target))
            {
                log.WriteLine(ServerReportLog.Refused(reporter.PlayerId, targetId, ServerReportRefusal.UnknownTarget));
                return;
            }

            var key = (reporter.PlayerId, targetId);
            if (_savedAt.TryGetValue(key, out var savedAt) && _now - savedAt < RepeatSeconds)
            {
                log.WriteLine(ServerReportLog.Repeated(reporter.PlayerId, targetId));
                return;
            }

            var reportId = _newReportId();
            var line = ServerReportLog.Record(
                reportId,
                _utcNow(),
                _context.Match.MatchId,
                reporter.PlayerId,
                reporter.ProductUserId,
                targetId,
                target.ProductUserId,
                report.Reason,
                report.Comment);
            if (!TryWrite(line))
            {
                return;
            }

            // 書けなかった通報は間引きに数えない。数えると、送り直しても残らなくなる
            _savedAt[key] = _now;
            log.WriteLine(ServerReportLog.Saved(reporter.PlayerId, targetId, report.Reason));
            WriteEvidence(reportId, targetId, reporter.ProductUserId);
        }

        public string EvidencePath(string reportId)
        {
            if (!ServerReportLog.IsReportId(reportId))
            {
                throw new ArgumentException("not a report id", nameof(reportId));
            }

            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? string.Empty, reportId + ServerReportEvidenceLog.FileSuffix);
        }

        // 通報の行を書いた後に書くのは、証拠だけが残って対になる通報の無いファイルを作らないため。
        // 証拠が書けなくても通報は残す。通報の行は運営が読む本体で、証拠はその添え物なので
        private void WriteEvidence(string reportId, int targetId, string reporterKey)
        {
            var text = Evidence?.Describe(targetId, reporterKey);
            if (text == null)
            {
                return;
            }

            var path = EvidencePath(reportId);
            try
            {
                // CreateNew にするのは、id がぶつかったときに前の通報の証拠を上書きして消さないため
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(text);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _context.Log.WriteLine(ServerReportEvidenceLog.WriteFailed(path, e.Message));
                return;
            }

            _context.Log.WriteLine(ServerReportEvidenceLog.Saved(reportId, targetId, path));
        }

        public void Stop()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Dispose();
                _context?.Log.WriteLine(ServerReportLog.Closed(Path));
            }
            catch (IOException e)
            {
                _context?.Log.WriteLine(ServerReportLog.WriteFailed(Path, e.Message));
            }
            finally
            {
                _writer = null;
                _savedAt.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        // 1 行を 1 回で書いて Flush するのは、落ちたときに行の途中で切れた通報や、書いたつもりで消えた通報を減らすため。
        // 改行を "\n" に決めるのは、JSON Lines を読む側がサーバーの OS で読み分けずに済むようにするため。
        private bool TryWrite(string line)
        {
            if (_writer == null)
            {
                _context.Log.WriteLine(ServerReportLog.WriteFailed(Path, "the report file is closed"));
                return false;
            }

            try
            {
                _writer.Write(line + "\n");
                _writer.Flush();
                return true;
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                // 試合の進行の途中で呼ばれるので、通報の書き込みの失敗で試合を止めない
                _context.Log.WriteLine(ServerReportLog.WriteFailed(Path, e.Message));
                return false;
            }
        }
    }
}
