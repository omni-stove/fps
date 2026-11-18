using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Shooter.Server
{
    // 読み書きは試合のループのスレッドだけで行う (ServerAdminConsole も Tick の中で触る) ので鍵を掛けない。
    internal sealed class ServerBanList
    {
        public const string BansItem = "bans";
        public const string ProductUserIdItem = "productUserId";
        public const string AddressItem = "address";
        public const string ReasonItem = "reason";
        public const string BannedAtItem = "bannedAt";

        public const int MaxReasonLength = 200;

        private static readonly JsonDocumentOptions _readOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        // 改行を OS に任せると、Windows で書いたファイルを Linux のサーバーに写したときに差分が全行に出るので "\n" に決める
        private static readonly JsonWriterOptions _writeOptions = new JsonWriterOptions { Indented = true, NewLine = "\n" };

        private readonly List<ServerBanEntry> _entries;

        private ServerBanList(string path, List<ServerBanEntry> entries)
        {
            Path = path;
            _entries = entries;
        }

        public string Path { get; }

        public IReadOnlyList<ServerBanEntry> Entries => _entries;

        public static ServerBanList InMemory()
        {
            return new ServerBanList(null, new List<ServerBanEntry>());
        }

        // まだ無いファイルは空の一覧として読み、最初の ban で作る。運用で空のファイルを先に置かなくてよいように
        public static bool TryLoad(string path, out ServerBanList list, out string error)
        {
            list = null;
            if (path == null)
            {
                list = InMemory();
                error = null;
                return true;
            }

            string text;
            try
            {
                if (!File.Exists(path))
                {
                    list = new ServerBanList(path, new List<ServerBanEntry>());
                    error = null;
                    return true;
                }

                text = File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = "cannot read the ban list " + path + ": " + e.Message;
                return false;
            }

            if (!TryParse(text, path, out var entries, out error))
            {
                return false;
            }

            list = new ServerBanList(path, entries);
            return true;
        }

        public static bool TryParse(string text, string source, out List<ServerBanEntry> entries, out string error)
        {
            entries = null;
            var where = "ban list " + source;
            if (string.IsNullOrWhiteSpace(text))
            {
                entries = new List<ServerBanEntry>();
                error = null;
                return true;
            }

            try
            {
                using (var document = JsonDocument.Parse(text, _readOptions))
                {
                    return TryRead(document.RootElement, where, out entries, out error);
                }
            }
            catch (JsonException e)
            {
                error = where + " is not valid JSON: " + e.Message;
                return false;
            }
        }

        private static bool TryRead(JsonElement root, string where, out List<ServerBanEntry> entries, out string error)
        {
            entries = null;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = where + " must be a JSON object with a \"" + BansItem + "\" array";
                return false;
            }

            JsonElement bans = default;
            var found = false;
            foreach (var item in root.EnumerateObject())
            {
                if (!string.Equals(item.Name, BansItem, StringComparison.Ordinal))
                {
                    error = where + " has an unknown item \"" + item.Name + "\" (expected \"" + BansItem + "\")";
                    return false;
                }

                bans = item.Value;
                found = true;
            }

            var read = new List<ServerBanEntry>();
            if (!found || bans.ValueKind == JsonValueKind.Null)
            {
                entries = read;
                error = null;
                return true;
            }

            if (bans.ValueKind != JsonValueKind.Array)
            {
                error = where + ": \"" + BansItem + "\" must be an array";
                return false;
            }

            var index = 0;
            foreach (var element in bans.EnumerateArray())
            {
                var at = where + ": " + BansItem + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (!TryReadEntry(element, at, out var entry, out error))
                {
                    return false;
                }

                // 同じ相手が 2 度書かれていても断る相手は変わらないので、起動を止めずに先のものを残す
                if (Find(read, entry.Kind, entry.Value) == null)
                {
                    read.Add(entry);
                }

                index++;
            }

            entries = read;
            error = null;
            return true;
        }

        private static bool TryReadEntry(JsonElement element, string at, out ServerBanEntry entry, out string error)
        {
            entry = null;
            if (element.ValueKind != JsonValueKind.Object)
            {
                error = at + " must be an object";
                return false;
            }

            string productUserId = null;
            string address = null;
            string reason = null;
            DateTime? bannedAt = null;
            foreach (var item in element.EnumerateObject())
            {
                switch (item.Name)
                {
                    case ProductUserIdItem:
                        if (!TryReadString(item, at, out productUserId, out error))
                        {
                            return false;
                        }

                        break;
                    case AddressItem:
                        if (!TryReadString(item, at, out address, out error))
                        {
                            return false;
                        }

                        break;
                    case ReasonItem:
                        if (!TryReadString(item, at, out reason, out error))
                        {
                            return false;
                        }

                        if (reason != null && reason.Length > MaxReasonLength)
                        {
                            error = at + "." + ReasonItem + " must be at most " + MaxReasonLength.ToString(CultureInfo.InvariantCulture) + " characters";
                            return false;
                        }

                        break;
                    case BannedAtItem:
                        if (!TryReadString(item, at, out var time, out error))
                        {
                            return false;
                        }

                        if (time != null)
                        {
                            if (!DateTime.TryParse(
                                time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                            {
                                error = at + "." + BannedAtItem + " must be a UTC time such as 2026-11-18T09:00:00Z (got '" + time + "')";
                                return false;
                            }

                            bannedAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                        }

                        break;
                    default:
                        error = at + " has an unknown item \"" + item.Name + "\" (expected " + ProductUserIdItem + ", " + AddressItem + ", "
                            + ReasonItem + " or " + BannedAtItem + ")";
                        return false;
                }
            }

            if ((productUserId == null) == (address == null))
            {
                error = at + " must have exactly one of \"" + ProductUserIdItem + "\" and \"" + AddressItem + "\"";
                return false;
            }

            var kind = productUserId != null ? ServerBanKind.ProductUserId : ServerBanKind.Address;
            var value = productUserId ?? address;
            if (!ServerBanEntry.TryNormalize(kind, value, out _))
            {
                error = at + "." + (productUserId != null ? ProductUserIdItem : AddressItem) + " is not a valid " + ServerBanEntry.KindName(kind)
                    + " (got '" + value + "')";
                return false;
            }

            entry = new ServerBanEntry(kind, value, reason, bannedAt);
            error = null;
            return true;
        }

        private static bool TryReadString(JsonProperty item, string at, out string value, out string error)
        {
            value = null;
            error = null;
            switch (item.Value.ValueKind)
            {
                case JsonValueKind.Null:
                    return true;
                case JsonValueKind.String:
                    value = item.Value.GetString();
                    return true;
                default:
                    error = at + "." + item.Name + " must be a string";
                    return false;
            }
        }

        public bool TryFind(string productUserId, string address, out ServerBanEntry entry)
        {
            foreach (var candidate in _entries)
            {
                if (candidate.Matches(productUserId, address))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public ServerBanEntry Find(ServerBanKind kind, string value)
        {
            return Find(_entries, kind, value);
        }

        private static ServerBanEntry Find(List<ServerBanEntry> entries, ServerBanKind kind, string value)
        {
            foreach (var entry in entries)
            {
                if (entry.Targets(kind, value))
                {
                    return entry;
                }
            }

            return null;
        }

        // 同じ相手が載っていれば足さずに false を返し、先の理由と日時を残す
        public bool Add(ServerBanEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (Find(entry.Kind, entry.Value) != null)
            {
                return false;
            }

            _entries.Add(entry);
            return true;
        }

        public ServerBanEntry Remove(ServerBanKind kind, string value)
        {
            var entry = Find(kind, value);
            if (entry != null)
            {
                _entries.Remove(entry);
            }

            return entry;
        }

        public string ToJson()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, _writeOptions))
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray(BansItem);
                    foreach (var entry in _entries)
                    {
                        writer.WriteStartObject();
                        writer.WriteString(entry.Kind == ServerBanKind.ProductUserId ? ProductUserIdItem : AddressItem, entry.Value);
                        if (entry.Reason != null)
                        {
                            writer.WriteString(ReasonItem, entry.Reason);
                        }

                        if (entry.BannedAt.HasValue)
                        {
                            writer.WriteString(BannedAtItem, entry.BannedAt.Value.ToString(ServerBanEntry.TimeFormat, CultureInfo.InvariantCulture));
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
            }
        }

        // 書いている途中で止まっても前の一覧が壊れないよう、隣に書いてから置き換える
        public bool TrySave(out string error)
        {
            error = null;
            if (Path == null)
            {
                return true;
            }

            var temporary = Path + ".tmp";
            try
            {
                var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporary, ToJson(), new UTF8Encoding(false));
                File.Move(temporary, Path, true);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = "cannot write the ban list " + Path + ": " + e.Message;
                return false;
            }
        }
    }
}
