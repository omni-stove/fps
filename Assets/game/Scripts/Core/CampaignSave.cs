using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// キャンペーンのセーブデータ。チェックポイントに着いた時点のミッションの進み具合とプレイヤーの状態 (体力・武器ごとの残弾・持っていた武器) を持つ。不変。
    /// <see cref="ToJson"/> と <see cref="FromJson"/> で文字列にして <see cref="ISaveStorage"/> に置く。
    /// </summary>
    public sealed class CampaignSave
    {
        /// <summary>このコードが書き出すセーブデータの形式のバージョン。武器ごとの残弾を持つ。</summary>
        public const int CurrentVersion = 2;

        /// <summary>武器を 1 本しか持てなかった頃の形式のバージョン。読み込めるが書き出さない。</summary>
        public const int LegacySingleWeaponVersion = 1;

        /// <summary>
        /// <see cref="LegacySingleWeaponVersion"/> のセーブの残弾を持たせる武器 (rifle) の Id。
        /// その頃のプレイヤーは rifle だけを持っていた。rifle の Id は再ビルドしても変わらない (WeaponAssetBuilder.RifleId と同じ値)。
        /// </summary>
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

        private static readonly string[] _weaponKeys = { _weaponIdKey, _ammoInMagazineKey, _reserveAmmoKey };

        private static readonly string[] _objectiveKeys = { _objectiveIdKey, _objectiveStateKey, _objectiveCountKey };

        /// <summary>セーブデータの形式のバージョン。常に <see cref="CurrentVersion"/>。</summary>
        public int Version { get; }

        /// <summary>ミッションの Id。</summary>
        public string MissionId { get; }

        /// <summary>最後に通ったチェックポイントの Id。まだ通っていなければ空文字。</summary>
        public string CheckpointId { get; }

        /// <summary>プレイヤーの体力。1 以上。</summary>
        public int Health { get; }

        /// <summary>持っていた武器の Id。<see cref="Weapons"/> のどれかの Id か、分からなければ空文字。</summary>
        public string EquippedWeaponId { get; }

        /// <summary>武器ごとの残弾。持っていた並びの順。Id は重複しない。</summary>
        public IReadOnlyList<WeaponAmmo> Weapons { get; }

        /// <summary>各目標の状態と進み具合。<see cref="Mission.Restore"/> に渡す。</summary>
        public IReadOnlyList<ObjectiveProgress> Objectives { get; }

        /// <param name="missionId">ミッションの Id。空にはできない。</param>
        /// <param name="checkpointId">最後に通ったチェックポイントの Id。null は空文字 (まだ通っていない) として扱う。</param>
        /// <param name="health">プレイヤーの体力。1 以上。</param>
        /// <param name="weapons">武器ごとの残弾。null を含まず、Id が重複しないこと。</param>
        /// <param name="equippedWeaponId">持っていた武器の Id。null は空文字 (分からない) として扱う。空でなければ weapons のどれかの Id であること。</param>
        /// <param name="objectives">各目標の状態と進み具合 (<see cref="Mission.Snapshot"/> の結果)。null を含まず、Id が重複しないこと。</param>
        public CampaignSave(
            string missionId,
            string checkpointId,
            int health,
            IEnumerable<WeaponAmmo> weapons,
            string equippedWeaponId,
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

            Version = CurrentVersion;
            MissionId = missionId;
            CheckpointId = checkpointId ?? string.Empty;
            Health = health;
            Weapons = new ReadOnlyCollection<WeaponAmmo>(weaponList);
            EquippedWeaponId = equippedWeaponId;
            Objectives = new ReadOnlyCollection<ObjectiveProgress>(list);
        }

        /// <summary>Id が weaponId の武器の残弾。無ければ null。</summary>
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
                AppendKey(builder, _reserveAmmoKey).Append(weapon.ReserveAmmo.ToString(CultureInfo.InvariantCulture));
                builder.Append('}');
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

        /// <summary>
        /// <see cref="ToJson"/> で書き出した文字列から読む。<see cref="LegacySingleWeaponVersion"/> の形式 (武器 1 本ぶんの弾数) も読み、
        /// その弾数は <see cref="LegacyWeaponId"/> (rifle) の残弾として、持っていた武器も rifle として扱う。
        /// JSON として壊れている、項目が欠けている・余分・型違い・範囲外、バージョンがどちらでもないときは
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
            if (version != CurrentVersion && version != LegacySingleWeaponVersion)
            {
                throw new FormatException(
                    $"unsupported save data version {version.ToString(CultureInfo.InvariantCulture)} (expected {CurrentVersion.ToString(CultureInfo.InvariantCulture)} or {LegacySingleWeaponVersion.ToString(CultureInfo.InvariantCulture)})");
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
                    weapons = ReadWeapons(ReadArray(root, _weaponsKey));
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

                return new CampaignSave(missionId, checkpointId, health, weapons, equippedWeaponId, objectives);
            }
            catch (ArgumentException e)
            {
                // 値が範囲外などで組み立てられないのも、壊れたデータとして扱う。
                throw new FormatException("invalid save data: " + e.Message, e);
            }
        }

        private static List<WeaponAmmo> ReadWeapons(List<object> items)
        {
            var weapons = new List<WeaponAmmo>(items.Count);
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> entry))
                {
                    throw new FormatException($"invalid save data: each of '{_weaponsKey}' must be an object");
                }

                RejectUnknownKeys(entry, _weaponKeys);
                weapons.Add(new WeaponAmmo(
                    ReadString(entry, _weaponIdKey),
                    ReadInt(entry, _ammoInMagazineKey),
                    ReadInt(entry, _reserveAmmoKey)));
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
