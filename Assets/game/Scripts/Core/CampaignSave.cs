using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// キャンペーンのセーブデータ。チェックポイントに着いた時点のミッションの進み具合とプレイヤーの状態を持つ。不変。
    /// <see cref="ToJson"/> と <see cref="FromJson"/> で文字列にして <see cref="ISaveStorage"/> に置く。
    /// </summary>
    public sealed class CampaignSave
    {
        /// <summary>このコードが書き出し、読み込めるセーブデータの形式のバージョン。</summary>
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _missionIdKey = "missionId";
        private const string _checkpointIdKey = "checkpointId";
        private const string _healthKey = "health";
        private const string _ammoInMagazineKey = "ammoInMagazine";
        private const string _reserveAmmoKey = "reserveAmmo";
        private const string _objectivesKey = "objectives";
        private const string _objectiveIdKey = "id";
        private const string _objectiveStateKey = "state";
        private const string _objectiveCountKey = "count";

        private static readonly string[] _rootKeys =
        {
            _versionKey, _missionIdKey, _checkpointIdKey, _healthKey, _ammoInMagazineKey, _reserveAmmoKey, _objectivesKey,
        };

        private static readonly string[] _objectiveKeys = { _objectiveIdKey, _objectiveStateKey, _objectiveCountKey };

        /// <summary>セーブデータの形式のバージョン。常に <see cref="CurrentVersion"/>。</summary>
        public int Version { get; }

        /// <summary>ミッションの Id。</summary>
        public string MissionId { get; }

        /// <summary>最後に通ったチェックポイントの Id。まだ通っていなければ空文字。</summary>
        public string CheckpointId { get; }

        /// <summary>プレイヤーの体力。1 以上。</summary>
        public int Health { get; }

        /// <summary>弾倉の弾数。0 以上。</summary>
        public int AmmoInMagazine { get; }

        /// <summary>予備弾の数。0 以上。</summary>
        public int ReserveAmmo { get; }

        /// <summary>各目標の状態と進み具合。<see cref="Mission.Restore"/> に渡す。</summary>
        public IReadOnlyList<ObjectiveProgress> Objectives { get; }

        /// <param name="missionId">ミッションの Id。空にはできない。</param>
        /// <param name="checkpointId">最後に通ったチェックポイントの Id。null は空文字 (まだ通っていない) として扱う。</param>
        /// <param name="health">プレイヤーの体力。1 以上。</param>
        /// <param name="ammoInMagazine">弾倉の弾数。0 以上。</param>
        /// <param name="reserveAmmo">予備弾の数。0 以上。</param>
        /// <param name="objectives">各目標の状態と進み具合 (<see cref="Mission.Snapshot"/> の結果)。null を含まず、Id が重複しないこと。</param>
        public CampaignSave(
            string missionId,
            string checkpointId,
            int health,
            int ammoInMagazine,
            int reserveAmmo,
            IEnumerable<ObjectiveProgress> objectives)
        {
            if (missionId == null)
            {
                throw new ArgumentNullException(nameof(missionId));
            }

            if (missionId.Length == 0)
            {
                throw new ArgumentException("missionId must not be empty", nameof(missionId));
            }

            if (health <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(health), health, "health must be positive");
            }

            if (ammoInMagazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoInMagazine), ammoInMagazine, "ammoInMagazine must not be negative");
            }

            if (reserveAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveAmmo), reserveAmmo, "reserveAmmo must not be negative");
            }

            if (objectives == null)
            {
                throw new ArgumentNullException(nameof(objectives));
            }

            var list = new List<ObjectiveProgress>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var objective in objectives)
            {
                if (objective == null)
                {
                    throw new ArgumentException("objectives must not contain null", nameof(objectives));
                }

                if (!ids.Add(objective.Id))
                {
                    throw new ArgumentException($"duplicate objective id '{objective.Id}'", nameof(objectives));
                }

                list.Add(objective);
            }

            Version = CurrentVersion;
            MissionId = missionId;
            CheckpointId = checkpointId ?? string.Empty;
            Health = health;
            AmmoInMagazine = ammoInMagazine;
            ReserveAmmo = reserveAmmo;
            Objectives = new ReadOnlyCollection<ObjectiveProgress>(list);
        }

        /// <summary>JSON の文字列に書き出す。<see cref="FromJson"/> で同じ内容に戻る。</summary>
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendKey(builder, _versionKey).Append(Version.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendKey(builder, _missionIdKey);
            SaveJson.AppendString(builder, MissionId);
            builder.Append(',');
            AppendKey(builder, _checkpointIdKey);
            SaveJson.AppendString(builder, CheckpointId);
            builder.Append(',');
            AppendKey(builder, _healthKey).Append(Health.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendKey(builder, _ammoInMagazineKey).Append(AmmoInMagazine.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendKey(builder, _reserveAmmoKey).Append(ReserveAmmo.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendKey(builder, _objectivesKey).Append('[');
            for (var i = 0; i < Objectives.Count; i++)
            {
                var objective = Objectives[i];
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendKey(builder, _objectiveIdKey);
                SaveJson.AppendString(builder, objective.Id);
                builder.Append(',');
                AppendKey(builder, _objectiveStateKey);
                SaveJson.AppendString(builder, StateName(objective.State));
                builder.Append(',');
                AppendKey(builder, _objectiveCountKey).Append(objective.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }

        /// <summary>
        /// <see cref="ToJson"/> で書き出した文字列から読む。
        /// JSON として壊れている、項目が欠けている・余分・型違い・範囲外、バージョンが <see cref="CurrentVersion"/> と違うときは
        /// <see cref="FormatException"/> を投げる。
        /// </summary>
        public static CampaignSave FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (!(SaveJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("invalid save data: the root must be an object");
            }

            // 形式が変わっても判別できるよう、バージョンは他の項目より先に確かめる。
            var version = ReadInt(root, _versionKey);
            if (version != CurrentVersion)
            {
                throw new FormatException(
                    $"unsupported save data version {version.ToString(CultureInfo.InvariantCulture)} (expected {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            RejectUnknownKeys(root, _rootKeys);
            var missionId = ReadString(root, _missionIdKey);
            var checkpointId = ReadString(root, _checkpointIdKey);
            var health = ReadInt(root, _healthKey);
            var ammoInMagazine = ReadInt(root, _ammoInMagazineKey);
            var reserveAmmo = ReadInt(root, _reserveAmmoKey);
            if (!(ReadValue(root, _objectivesKey) is List<object> items))
            {
                throw new FormatException($"invalid save data: '{_objectivesKey}' must be an array");
            }

            var objectives = new List<ObjectiveProgress>(items.Count);
            try
            {
                foreach (var item in items)
                {
                    if (!(item is Dictionary<string, object> entry))
                    {
                        throw new FormatException($"invalid save data: each of '{_objectivesKey}' must be an object");
                    }

                    RejectUnknownKeys(entry, _objectiveKeys);
                    objectives.Add(new ObjectiveProgress(
                        ReadString(entry, _objectiveIdKey),
                        ParseState(ReadString(entry, _objectiveStateKey)),
                        ReadInt(entry, _objectiveCountKey)));
                }

                return new CampaignSave(missionId, checkpointId, health, ammoInMagazine, reserveAmmo, objectives);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外などで組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid save data: " + e.Message, e);
            }
        }

        private static StringBuilder AppendKey(StringBuilder builder, string key)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':');
        }

        /// <summary>セーブデータでの状態の名前。enum の名前を変えても形式が変わらないよう、ここで固定する。</summary>
        private static string StateName(ObjectiveState state)
        {
            switch (state)
            {
                case ObjectiveState.Locked:
                    return "locked";
                case ObjectiveState.Active:
                    return "active";
                case ObjectiveState.Completed:
                    return "completed";
                case ObjectiveState.Failed:
                    return "failed";
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "unknown objective state");
            }
        }

        private static ObjectiveState ParseState(string name)
        {
            switch (name)
            {
                case "locked":
                    return ObjectiveState.Locked;
                case "active":
                    return ObjectiveState.Active;
                case "completed":
                    return ObjectiveState.Completed;
                case "failed":
                    return ObjectiveState.Failed;
                default:
                    throw new FormatException($"invalid save data: unknown objective state '{name}'");
            }
        }

        private static object ReadValue(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value))
            {
                throw new FormatException($"invalid save data: missing '{key}'");
            }

            return value;
        }

        private static string ReadString(Dictionary<string, object> fields, string key)
        {
            if (!(ReadValue(fields, key) is string value))
            {
                throw new FormatException($"invalid save data: '{key}' must be a string");
            }

            return value;
        }

        private static int ReadInt(Dictionary<string, object> fields, string key)
        {
            if (!(ReadValue(fields, key) is long value) || value < int.MinValue || value > int.MaxValue)
            {
                throw new FormatException($"invalid save data: '{key}' must be a 32-bit integer");
            }

            return (int)value;
        }

        private static void RejectUnknownKeys(Dictionary<string, object> fields, string[] known)
        {
            foreach (var key in fields.Keys)
            {
                if (Array.IndexOf(known, key) < 0)
                {
                    throw new FormatException($"invalid save data: unknown key '{key}'");
                }
            }
        }
    }
}
