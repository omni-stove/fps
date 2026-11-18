using System;
using System.IO;
using System.Text;

namespace Shooter.Core
{
    // クラッシュの行はプロセスが落ちる直前に書くので、ためずに 1 行ごとに書いて Flush する。
    // 大きさの上限に届いたら 1 つ前のファイル (.1) へ移して書き直す。消して書き直すと、上限の直後に起きたクラッシュの前の様子が失われるため。
    public sealed class JsonLinesTelemetrySink : ITelemetrySink
    {
        public const string PreviousSuffix = ".1";

        private static readonly UTF8Encoding _encoding = new UTF8Encoding(false);

        private readonly object _gate = new object();
        private readonly Action<string> _log;
        private FileStream _stream;
        private bool _disposed;

        public JsonLinesTelemetrySink(string path, int maxFileBytes, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be empty", nameof(path));
            }

            if (maxFileBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFileBytes), maxFileBytes, "maxFileBytes must be positive");
            }

            Path = System.IO.Path.GetFullPath(path);
            PreviousPath = Path + PreviousSuffix;
            MaxFileBytes = maxFileBytes;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Path { get; }

        public string PreviousPath { get; }

        public int MaxFileBytes { get; }

        public string Target => Path;

        public bool Write(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            // 改行を "\n" に決めるのは、JSON Lines を読む側が書いた OS で読み分けずに済むようにするため
            var bytes = _encoding.GetBytes(line + "\n");
            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                try
                {
                    var stream = Open();
                    if (stream.Length > 0 && stream.Length + bytes.Length > MaxFileBytes)
                    {
                        stream = RollOver();
                    }

                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                    return true;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
                {
                    // 開き直せば書けることがある (置き場所が一時的に読み取り専用だったなど) ので、閉じて次の行で開き直す
                    Close();
                    _log(TelemetryLog.WriteFailed(Path, e.Message));
                    return false;
                }
            }
        }

        public void Flush(bool wait)
        {
            lock (_gate)
            {
                if (_stream == null)
                {
                    return;
                }

                try
                {
                    _stream.Flush();
                }
                catch (IOException e)
                {
                    Close();
                    _log(TelemetryLog.WriteFailed(Path, e.Message));
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                Close();
            }
        }

        private FileStream Open()
        {
            if (_stream != null)
            {
                return _stream;
            }

            var folder = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // 書いている間もほかの道具でファイルを読めるよう、読み取りの共有を許す
            _stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
            return _stream;
        }

        private FileStream RollOver()
        {
            Close();
            if (File.Exists(PreviousPath))
            {
                File.Delete(PreviousPath);
            }

            File.Move(Path, PreviousPath);
            _log(TelemetryLog.RolledOver(Path, PreviousPath));
            return Open();
        }

        private void Close()
        {
            var stream = _stream;
            _stream = null;
            if (stream == null)
            {
                return;
            }

            try
            {
                stream.Dispose();
            }
            catch (IOException e)
            {
                _log(TelemetryLog.WriteFailed(Path, e.Message));
            }
        }
    }
}
