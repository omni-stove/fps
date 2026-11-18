using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 保存先のフォルダの *.replay を新しい順に並べる。Id はファイル名 (拡張子を除く) で、画面からの Id はこの一覧にあるものだけを受ける。
    // 再生を始める口 (play) は外から受ける。Runtime は通信路 (NetV2) を参照できず、ファイルを読む IMatchTransport を作れないため。
    public sealed class ReplayLibrary : IReplayLibrary, IDisposable
    {
        public const string DefaultFolderName = "Replays";

        public const int DefaultMaxReplays = 20;

        private const string IdFormat = "yyyyMMdd-HHmmss";

        private readonly Func<string, bool> _play;
        private readonly List<ReplayEntry> _entries = new List<ReplayEntry>();
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _disposed;

        public ReplayLibrary(string directory, Func<string, bool> play, int maxReplays = DefaultMaxReplays)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("directory must not be empty", nameof(directory));
            }

            if (maxReplays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReplays), maxReplays, "maxReplays must be at least 1");
            }

            Directory = directory;
            _play = play ?? throw new ArgumentNullException(nameof(play));
            MaxReplays = maxReplays;
        }

        public event Action Changed;

        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, DefaultFolderName);

        public string Directory { get; }

        public int MaxReplays { get; }

        public IReadOnlyList<ReplayEntry> Entries => _entries;

        public void Publish()
        {
            GameSignals.Replays.Publish(this);
        }

        public void Refresh()
        {
            _entries.Clear();
            _paths.Clear();
            foreach (var path in ListFiles())
            {
                var id = Path.GetFileNameWithoutExtension(path);
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        _entries.Add(ReplayFile.ReadEntry(id, stream, out _));
                        _paths[id] = path;
                    }
                }
                catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                {
                    // 書いている途中 (試合中) や別の版のファイルは並べずに飛ばし、ほかのリプレイは出す
                    Debug.LogWarning(ReplayLog.ListSkipped(path, e.Message));
                }
            }

            _entries.Sort(NewestFirst);
            Debug.Log(ReplayLog.Listed(Directory, _entries.Count));
            Changed?.Invoke();
        }

        public bool TryGetPath(string replayId, out string path)
        {
            path = null;
            return replayId != null && _paths.TryGetValue(replayId, out path);
        }

        public bool Play(string replayId)
        {
            if (!TryGetPath(replayId, out var path))
            {
                Debug.LogWarning(ReplayLog.NotFound(replayId ?? string.Empty));
                return false;
            }

            Debug.Log(ReplayLog.PlayRequested(replayId, path));
            try
            {
                if (_play(path))
                {
                    return true;
                }

                Debug.LogWarning(ReplayLog.PlayFailed(replayId, "the player refused the file"));
                return false;
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                Debug.LogWarning(ReplayLog.PlayFailed(replayId, e.Message));
                return false;
            }
        }

        public bool Delete(string replayId)
        {
            if (!TryGetPath(replayId, out var path))
            {
                Debug.LogWarning(ReplayLog.NotFound(replayId ?? string.Empty));
                return false;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(ReplayLog.DeleteFailed(replayId, e.Message));
                return false;
            }

            Debug.Log(ReplayLog.Deleted(replayId));
            Refresh();
            return true;
        }

        // 記録を始める前に呼ぶ。古いものを消して数を MaxReplays - 1 に収めてから、新しいファイルの場所を返す。
        // 同じ秒に二つ始めても上書きしないよう、名前が埋まっていれば番号を足す。
        public string NewRecordingPath(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("utcNow must be a UTC time", nameof(utcNow));
            }

            System.IO.Directory.CreateDirectory(Directory);
            Prune(MaxReplays - 1);
            var stem = utcNow.ToString(IdFormat, CultureInfo.InvariantCulture);
            var path = Path.Combine(Directory, stem + ReplayFile.Extension);
            for (var n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(Directory, stem + "-" + n.ToString(CultureInfo.InvariantCulture) + ReplayFile.Extension);
            }

            return path;
        }

        public void Prune(int keep)
        {
            if (keep < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keep), keep, "keep must not be negative");
            }

            Refresh();
            if (_entries.Count <= keep)
            {
                return;
            }

            for (var i = _entries.Count - 1; i >= keep; i--)
            {
                var id = _entries[i].Id;
                try
                {
                    File.Delete(_paths[id]);
                    Debug.Log(ReplayLog.Pruned(id, keep));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning(ReplayLog.DeleteFailed(id, e.Message));
                }
            }

            Refresh();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.Replays.ClearIfCurrent(this);
            Changed = null;
        }

        private IEnumerable<string> ListFiles()
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return Array.Empty<string>();
            }

            try
            {
                return System.IO.Directory.GetFiles(Directory, "*" + ReplayFile.Extension);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(ReplayLog.ListFailed(Directory, e.Message));
                return Array.Empty<string>();
            }
        }

        private static int NewestFirst(ReplayEntry a, ReplayEntry b)
        {
            var byTime = b.RecordedAtUtc.CompareTo(a.RecordedAtUtc);
            return byTime != 0 ? byTime : string.CompareOrdinal(b.Id, a.Id);
        }
    }
}
