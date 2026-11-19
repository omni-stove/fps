using System;
using System.IO;
using Shooter.Core;

namespace Shooter.Server
{
    // アカウントの ID ごとの持ち物庫を、起動で渡された置き場所のフォルダに <ID>.json (Core の StashSave の JSON) で持つ。
    // 読めないファイルは .broken に退けて新しく始める (オフラインの StashStore と同じ)。
    // 一時ファイルに書いてから置き換えるのは、書いている途中で落ちても前の中身を残すため
    internal sealed class ServerStashStore
    {
        public const string Extension = ".json";

        // オフラインの StashStore.BrokenSuffix と同じ。Runtime は Unity に頼るのでここからは引けない
        public const string BrokenSuffix = ".broken";

        public const string TemporarySuffix = ".tmp";

        // RejoinKey (64 文字) に Bot の印などの前置きを足しても収まる長さ
        public const int MaxAccountIdLength = 80;

        private const string _probeFileName = ".write-check" + TemporarySuffix;

        private readonly TextWriter _log;

        private ServerStashStore(string directory, TextWriter log)
        {
            Directory = directory;
            _log = log;
        }

        public string Directory { get; }

        // フォルダが無ければ作り、書けるかを確かめる。書けない置き場所で起動すると、脱出のたびに持ち帰った物を黙って失うため
        public static bool TryOpen(string directory, TextWriter log, out ServerStashStore store, out string error)
        {
            ArgumentNullException.ThrowIfNull(log);
            store = null;
            if (string.IsNullOrWhiteSpace(directory))
            {
                error = "the stash directory is empty";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(directory);
                System.IO.Directory.CreateDirectory(full);
                var probe = Path.Combine(full, _probeFileName);
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                error = "cannot use the stash directory " + directory + ": " + e.Message;
                return false;
            }

            store = new ServerStashStore(full, log);
            error = null;
            return true;
        }

        // ファイル名にするので、パスを区切る文字と、. で始まる名前 (.. や隠しファイル) を断る。大文字は RejoinKey と同じく小文字に直してから渡す
        public static bool IsAccountId(string accountId)
        {
            if (string.IsNullOrEmpty(accountId) || accountId.Length > MaxAccountIdLength || !IsLetterOrDigit(accountId[0]))
            {
                return false;
            }

            foreach (var c in accountId)
            {
                if (!IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                {
                    return false;
                }
            }

            return true;
        }

        public string PathOf(string accountId)
        {
            RequireAccountId(accountId);
            return Path.Combine(Directory, accountId + Extension);
        }

        public string BrokenPathOf(string accountId)
        {
            return PathOf(accountId) + BrokenSuffix;
        }

        public bool HasSave(string accountId)
        {
            return File.Exists(PathOf(accountId));
        }

        // ファイルが無ければ持ち物庫の決まり (StashSpec) の最初の物で始める。
        // 読めない (権限など) ときは IOException・UnauthorizedAccessException をそのまま投げる。新しく始めて上書きすると、置いてある物を消してしまうため
        public Stash Load(string accountId, StashSpec spec, ItemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(catalog);
            var path = PathOf(accountId);
            if (!File.Exists(path))
            {
                return Stash.Fresh(spec, catalog);
            }

            var text = File.ReadAllText(path);
            var stacks = StashSave.FromJson(text, out var problem);
            if (stacks == null)
            {
                // 次の保存で読めなかった中身が上書きされると直す手が無くなるので、別のファイルに退けてから始め直す
                var broken = BrokenPathOf(accountId);
                File.Move(path, broken, overwrite: true);
                _log.WriteLine(StashLog.BrokenSave(path, broken, problem));
                return Stash.Fresh(spec, catalog);
            }

            if (stacks.Count > spec.Capacity)
            {
                _log.WriteLine(StashLog.OverCapacity(path, stacks.Count, spec.Capacity));
            }

            return Stash.Restore(spec.Capacity, catalog, stacks);
        }

        public void Save(string accountId, Stash stash)
        {
            ArgumentNullException.ThrowIfNull(stash);
            var path = PathOf(accountId);
            var temporary = path + TemporarySuffix;
            File.WriteAllText(temporary, StashSave.ToJson(stash.Stacks));
            File.Move(temporary, path, overwrite: true);
        }

        private static void RequireAccountId(string accountId)
        {
            if (!IsAccountId(accountId))
            {
                throw new ArgumentException(
                    "account id must be 1-" + MaxAccountIdLength + " characters of [0-9a-z_.-] starting with a letter or digit (got '" + accountId + "')",
                    nameof(accountId));
            }
        }

        // 使える文字を ASCII に絞ってあるので、文化に左右されない手書きの判定で足りる
        private static bool IsLetterOrDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z');
        }
    }
}
