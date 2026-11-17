using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public sealed class CampaignSave
    {
        public const int CurrentVersion = 3;

        public const int WithoutAttachmentsVersion = 2;

        public const int LegacySingleWeaponVersion = 1;

        // LegacySingleWeaponVersion の頃のプレイヤーは rifle だけを持っていたので、その残弾を rifle に持たせる。
        // rifle の Id は再ビルドしても変わらない (WeaponAssetBuilder.RifleId と同じ値) ので、ここに書いておける。
        public const string LegacyWeaponId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";

        private const string _versionKey = "version";
        private const string _missionIdKey = "missionId";
        private const string _checkpointIdKey = "checkpointId";
        private const string _healthKey = "health";
        private const string _ammoInMagazineKey = "ammoInMagazine";
        private const string _reserveAmmoKey = "reserveAmmo";
        private const string _equippedWeaponIdKey = "equippedWeaponId";
        private const string _weaponsKey = "weapons";
        private const string _weaponIdKey = "id";
        private const string _attachmentsKey = "attachments";
        private const string _objectivesKey = "objectives";
        private const string _objectiveIdKey = "id";
        private const string _objectiveStateKey = "state";
        private const string _objectiveCountKey = "count";

        private static readonly string[] _rootKeys =
        {
            _versionKey, _missionIdKey, _checkpointIdKey, _healthKey, _equippedWeaponIdKey, _weaponsKey, _objectivesKey,
        };

        private static readonly string[] _legacyRootKeys =
        {
            _versionKey, _missionIdKey, _checkpointIdKey, _healthKey, _ammoInMagazineKey, _reserveAmmoKey, _objectivesKey,
        };

        private static readonly string[] _weaponKeys = { _weaponIdKey, _ammoInMagazineKey, _reserveAmmoKey, _attachmentsKey };

        private static readonly string[] _withoutAttachmentsWeaponKeys = { _weaponIdKey, _ammoInMagazineKey, _reserveAmmoKey };

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _noAttachments =
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>());

        private static readonly string[] _objectiveKeys = { _objectiveIdKey, _objectiveStateKey, _objectiveCountKey };

        public int Version { get; }

        public string MissionId { get; }

        public string CheckpointId { get; }

        public int Health { get; }

        public string EquippedWeaponId { get; }

        public IReadOnlyList<WeaponAmmo> Weapons { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Attachments { get; }

        public IReadOnlyList<ObjectiveProgress> Objectives { get; }

        public CampaignSave(
            string missionId,
            string checkpointId,
            int health,
            IEnumerable<WeaponAmmo> weapons,
            string equippedWeaponId,
            IEnumerable<ObjectiveProgress> objectives,
            IReadOnlyDictionary<string, IReadOnlyList<string>> attachments = null)
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

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            var weaponList = new List<WeaponAmmo>();
            var weaponIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var weapon in weapons)
            {
                if (weapon == null)
                {
                    throw new ArgumentException("weapons must not contain null", nameof(weapons));
                }

                if (!weaponIds.Add(weapon.WeaponId))
                {
                    throw new ArgumentException($"duplicate weapon id '{weapon.WeaponId}'", nameof(weapons));
                }

                weaponList.Add(weapon);
            }

            equippedWeaponId ??= string.Empty;
            if (equippedWeaponId.Length > 0 && !weaponIds.Contains(equippedWeaponId))
            {
                throw new ArgumentException(
                    $"equipped weapon id '{equippedWeaponId}' is not one of the saved weapons", nameof(equippedWeaponId));
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

            var attached = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            if (attachments != null)
            {
                foreach (var pair in attachments)
                {
                    if (!weaponIds.Contains(pair.Key ?? string.Empty))
                    {
                        throw new ArgumentException($"attachments are for weapon '{pair.Key}', which is not one of the saved weapons", nameof(attachments));
                    }

                    var itemIds = new List<string>();
                    foreach (var itemId in pair.Value ?? WeaponAttachments.None)
                    {
                        if (string.IsNullOrEmpty(itemId))
                        {
                            throw new ArgumentException($"attachments of weapon '{pair.Key}' must not contain an empty id", nameof(attachments));
                        }

                        itemIds.Add(itemId);
                    }

                    if (itemIds.Count > 0)
                    {
                        attached[pair.Key] = itemIds.AsReadOnly();
                    }
                }
            }

            Version = CurrentVersion;
            Attachments = attached.Count > 0 ? new ReadOnlyDictionary<string, IReadOnlyList<string>>(attached) : _noAttachments;
            MissionId = missionId;
            CheckpointId = checkpointId ?? string.Empty;
            Health = health;
            Weapons = new ReadOnlyCollection<WeaponAmmo>(weaponList);
            EquippedWeaponId = equippedWeaponId;
            Objectives = new ReadOnlyCollection<ObjectiveProgress>(list);
        }

        public WeaponAmmo FindWeapon(string weaponId)
        {
            foreach (var weapon in Weapons)
            {
                if (string.Equals(weapon.WeaponId, weaponId, StringComparison.Ordinal))
                {
                    return weapon;
                }
            }

            return null;
        }

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
            AppendKey(builder, _equippedWeaponIdKey);
            SaveJson.AppendString(builder, EquippedWeaponId);
            builder.Append(',');
            AppendKey(builder, _weaponsKey).Append('[');
            for (var i = 0; i < Weapons.Count; i++)
            {
                var weapon = Weapons[i];
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendKey(builder, _weaponIdKey);
                SaveJson.AppendString(builder, weapon.WeaponId);
                builder.Append(',');
                AppendKey(builder, _ammoInMagazineKey).Append(weapon.AmmoInMagazine.ToString(CultureInfo.InvariantCulture)).Append(',');
                AppendKey(builder, _reserveAmmoKey).Append(weapon.ReserveAmmo.ToString(CultureInfo.InvariantCulture)).Append(',');
                AppendKey(builder, _attachmentsKey).Append('[');
                if (Attachments.TryGetValue(weapon.WeaponId, out var attached))
                {
                    for (var a = 0; a < attached.Count; a++)
                    {
                        if (a > 0)
                        {
                            builder.Append(',');
                        }

                        SaveJson.AppendString(builder, attached[a]);
                    }
                }

                builder.Append("]}");
            }

            builder.Append("],");
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
            if (version != CurrentVersion && version != WithoutAttachmentsVersion && version != LegacySingleWeaponVersion)
            {
                throw new FormatException(
                    $"unsupported save data version {version.ToString(CultureInfo.InvariantCulture)} (expected {CurrentVersion.ToString(CultureInfo.InvariantCulture)}, "
                    + $"{WithoutAttachmentsVersion.ToString(CultureInfo.InvariantCulture)} or {LegacySingleWeaponVersion.ToString(CultureInfo.InvariantCulture)})");
            }

            var legacy = version == LegacySingleWeaponVersion;
            RejectUnknownKeys(root, legacy ? _legacyRootKeys : _rootKeys);
            var missionId = ReadString(root, _missionIdKey);
            var checkpointId = ReadString(root, _checkpointIdKey);
            var health = ReadInt(root, _healthKey);
            var items = ReadArray(root, _objectivesKey);

            try
            {
                List<WeaponAmmo> weapons;
                string equippedWeaponId;
                var attachments = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                if (legacy)
                {
                    weapons = new List<WeaponAmmo>
                    {
                        new WeaponAmmo(LegacyWeaponId, ReadInt(root, _ammoInMagazineKey), ReadInt(root, _reserveAmmoKey)),
                    };
                    equippedWeaponId = LegacyWeaponId;
                }
                else
                {
                    equippedWeaponId = ReadString(root, _equippedWeaponIdKey);
                    weapons = ReadWeapons(ReadArray(root, _weaponsKey), version == CurrentVersion ? attachments : null);
                }

                var objectives = new List<ObjectiveProgress>(items.Count);
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

                return new CampaignSave(missionId, checkpointId, health, weapons, equippedWeaponId, objectives, attachments);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外などで組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid save data: " + e.Message, e);
            }
        }

        private static List<WeaponAmmo> ReadWeapons(List<object> items, Dictionary<string, IReadOnlyList<string>> attachments)
        {
            var weapons = new List<WeaponAmmo>(items.Count);
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> entry))
                {
                    throw new FormatException($"invalid save data: each of '{_weaponsKey}' must be an object");
                }

                RejectUnknownKeys(entry, attachments != null ? _weaponKeys : _withoutAttachmentsWeaponKeys);
                var weaponId = ReadString(entry, _weaponIdKey);
                weapons.Add(new WeaponAmmo(weaponId, ReadInt(entry, _ammoInMagazineKey), ReadInt(entry, _reserveAmmoKey)));
                if (attachments == null)
                {
                    continue;
                }

                var itemIds = new List<string>();
                foreach (var attached in ReadArray(entry, _attachmentsKey))
                {
                    if (!(attached is string itemId))
                    {
                        throw new FormatException($"invalid save data: each of '{_attachmentsKey}' must be a string");
                    }

                    itemIds.Add(itemId);
                }

                if (itemIds.Count > 0)
                {
                    attachments[weaponId] = itemIds;
                }
            }

            return weapons;
        }

        private static List<object> ReadArray(Dictionary<string, object> fields, string key)
        {
            if (!(ReadValue(fields, key) is List<object> items))
            {
                throw new FormatException($"invalid save data: '{key}' must be an array");
            }

            return items;
        }

        private static StringBuilder AppendKey(StringBuilder builder, string key)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':');
        }

        // enum の名前を変えてもセーブデータの形式が変わらないよう、名前をここで固定する。
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
