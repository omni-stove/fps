using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 操作設定。<see cref="HoldAction"/> ごとの <see cref="HoldMode"/> を、キーボード／マウスとゲームパッドで別々に持ち、
    /// 感度・視野角・音量 (<see cref="ControlValue"/>) を整数で持ち、オンラインの回線の表示を出すか (<see cref="ShowNetStats"/>) を持つ。不変。
    /// <see cref="ToJson"/> と <see cref="FromJson"/> で文字列にして保存する。
    /// </summary>
    public sealed class ControlSettings : IEquatable<ControlSettings>
    {
        /// <summary>このコードが書き出す形式のバージョン。</summary>
        public const int CurrentVersion = 3;

        /// <summary>
        /// 読める最も古い形式のバージョン。1 は Hold/Toggle だけを持ち、数値の設定は初期値で補う。
        /// 2 は回線の表示 (<see cref="ShowNetStats"/>) を持たず、初期値 (出さない) で補う。
        /// </summary>
        public const int OldestReadableVersion = 1;

        private const string _versionKey = "version";
        private const string _keyboardMouseKey = "keyboardMouse";
        private const string _gamepadKey = "gamepad";
        private const string _sprintKey = "sprint";
        private const string _crouchKey = "crouch";
        private const string _aimKey = "aim";
        private const string _mouseSensitivityKey = "mouseSensitivity";
        private const string _gamepadSensitivityKey = "gamepadSensitivity";
        private const string _adsSensitivityKey = "adsSensitivity";
        private const string _fieldOfViewKey = "fieldOfView";
        private const string _masterVolumeKey = "masterVolume";
        private const string _effectsVolumeKey = "effectsVolume";
        private const string _showNetStatsKey = "showNetStats";
        private const string _holdName = "hold";
        private const string _toggleName = "toggle";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };

        private static readonly ControlValue[] _values =
        {
            ControlValue.MouseSensitivity,
            ControlValue.GamepadSensitivity,
            ControlValue.AdsSensitivity,
            ControlValue.FieldOfView,
            ControlValue.MasterVolume,
            ControlValue.EffectsVolume,
        };

        // 添字は ValueIndex(value)。
        private static readonly ControlValueRange[] _ranges =
        {
            new ControlValueRange(10, 500, 100),
            new ControlValueRange(10, 500, 100),
            new ControlValueRange(10, 200, 100),
            // 縦の視野角。60 は Unity のカメラの既定値で、今までの見え方を初期値にする。90 を超えると周りが大きく歪む。
            new ControlValueRange(60, 90, 60),
            new ControlValueRange(0, 100, 100),
            new ControlValueRange(0, 100, 100),
        };

        private static readonly string[] _versionOneRootKeys = { _versionKey, _keyboardMouseKey, _gamepadKey };

        private static readonly string[] _versionTwoRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
        };

        private static readonly string[] _rootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _showNetStatsKey,
        };

        private static readonly string[] _actionKeys = { _sprintKey, _crouchKey, _aimKey };

        /// <summary>
        /// 初期値。キーボード／マウスはすべて Hold。
        /// ゲームパッドはスティックを押し込み続けにくいのでダッシュとしゃがみを Toggle にし、ADS はトリガーなので Hold。
        /// 数値は <see cref="RangeOf"/> の初期値。回線の表示は出さない。
        /// </summary>
        public static ControlSettings Default { get; } = new ControlSettings(
            new[]
            {
                HoldMode.Hold, HoldMode.Hold, HoldMode.Hold,
                HoldMode.Toggle, HoldMode.Toggle, HoldMode.Hold,
            },
            DefaultValues(),
            false);

        // 添字は Index(device, action)。
        private readonly HoldMode[] _modes;

        // 添字は ValueIndex(value)。
        private readonly int[] _amounts;

        private readonly bool _showNetStats;

        private ControlSettings(HoldMode[] modes, int[] amounts, bool showNetStats)
        {
            _modes = modes;
            _amounts = amounts;
            _showNetStats = showNetStats;
        }

        /// <summary>数値の設定をすべて、並べる順に返す。</summary>
        public static IReadOnlyList<ControlValue> Values { get; } = new ReadOnlyCollection<ControlValue>(_values);

        /// <summary>value が取れる範囲と初期値。</summary>
        public static ControlValueRange RangeOf(ControlValue value)
        {
            return _ranges[ValueIndex(value)];
        }

        /// <summary>device で action を操作するときのモード。</summary>
        public HoldMode Get(InputDeviceKind device, HoldAction action)
        {
            return _modes[Index(device, action)];
        }

        /// <summary>数値の設定 value の値。</summary>
        public int Get(ControlValue value)
        {
            return _amounts[ValueIndex(value)];
        }

        /// <summary>device の視点感度の倍率 (<see cref="ControlValue.MouseSensitivity"/> / <see cref="ControlValue.GamepadSensitivity"/> を 1 で 100%)。</summary>
        public float LookSensitivity(InputDeviceKind device)
        {
            switch (device)
            {
                case InputDeviceKind.KeyboardMouse:
                    return Percent(ControlValue.MouseSensitivity);
                case InputDeviceKind.Gamepad:
                    return Percent(ControlValue.GamepadSensitivity);
                default:
                    throw new ArgumentOutOfRangeException(nameof(device), device, "unknown input device kind");
            }
        }

        /// <summary>覗き込み中に視点感度へ掛ける倍率 (<see cref="ControlValue.AdsSensitivity"/> を 1 で 100%)。</summary>
        public float AdsSensitivity => Percent(ControlValue.AdsSensitivity);

        /// <summary>腰だめのカメラの縦の視野角 (度)。</summary>
        public float FieldOfView => Get(ControlValue.FieldOfView);

        /// <summary>全体の音量 (0〜1)。</summary>
        public float MasterVolume => Percent(ControlValue.MasterVolume);

        /// <summary>効果音の音量 (0〜1)。全体の音量とは別に、効果音を鳴らすときに掛ける。</summary>
        public float EffectsVolume => Percent(ControlValue.EffectsVolume);

        /// <summary>オンラインの HUD の右上に、サーバーとの回線の Ping・パケットロス・tick を出すか。</summary>
        public bool ShowNetStats => _showNetStats;

        /// <summary>device の action だけを mode に変えた設定を返す。この設定は変わらない。</summary>
        public ControlSettings With(InputDeviceKind device, HoldAction action, HoldMode mode)
        {
            if (mode != HoldMode.Hold && mode != HoldMode.Toggle)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown hold mode");
            }

            var index = Index(device, action);
            if (_modes[index] == mode)
            {
                return this;
            }

            var modes = (HoldMode[])_modes.Clone();
            modes[index] = mode;
            return new ControlSettings(modes, _amounts, _showNetStats);
        }

        /// <summary>数値の設定 value だけを amount に変えた設定を返す。この設定は変わらない。</summary>
        /// <exception cref="ArgumentOutOfRangeException">amount が <see cref="RangeOf"/> の範囲の外。</exception>
        public ControlSettings With(ControlValue value, int amount)
        {
            var index = ValueIndex(value);
            var range = _ranges[index];
            if (!range.Contains(amount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    $"{KeyOf(value)} must be between {Format(range.Min)} and {Format(range.Max)}");
            }

            if (_amounts[index] == amount)
            {
                return this;
            }

            var amounts = (int[])_amounts.Clone();
            amounts[index] = amount;
            return new ControlSettings(_modes, amounts, _showNetStats);
        }

        /// <summary>回線の表示 (<see cref="ShowNetStats"/>) だけを show に変えた設定を返す。この設定は変わらない。</summary>
        public ControlSettings WithShowNetStats(bool show)
        {
            return show == _showNetStats ? this : new ControlSettings(_modes, _amounts, show);
        }

        /// <summary>JSON の文字列に書き出す。<see cref="FromJson"/> で同じ内容に戻る。</summary>
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendKey(builder, _versionKey).Append(Format(CurrentVersion));
            foreach (var device in _devices)
            {
                builder.Append(',');
                AppendKey(builder, DeviceKey(device)).Append('{');
                for (var i = 0; i < _actions.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendKey(builder, ActionKey(_actions[i]));
                    SaveJson.AppendString(builder, ModeName(Get(device, _actions[i])));
                }

                builder.Append('}');
            }

            foreach (var value in _values)
            {
                builder.Append(',');
                AppendKey(builder, KeyOf(value)).Append(Format(Get(value)));
            }

            builder.Append(',');
            AppendKey(builder, _showNetStatsKey).Append(_showNetStats ? "true" : "false");
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// <see cref="ToJson"/> で書き出した文字列から読む。壊れていても例外にはせず、読めなかった所は <see cref="Default"/> の値にする。
        /// JSON として壊れている・バージョンが読めないときはすべて初期値、操作ごとの値が無い・型違い・知らない名前ならその操作だけ初期値にする。
        /// 数値の設定は、無い・整数でなければ初期値、範囲の外なら範囲に収めた値にする。
        /// 回線の表示は、無い・真偽値でなければ初期値にする。
        /// バージョン 1 (数値の設定が無い形式) は、数値の設定を何も言わずに初期値で補う。
        /// バージョン 1・2 (回線の表示が無い形式) は、回線の表示を何も言わずに初期値で補う。知らない項目は読み飛ばす。
        /// </summary>
        /// <param name="json">読む文字列。</param>
        /// <param name="problems">初期値に戻した所や読み飛ばした所の理由。問題が無ければ空。</param>
        public static ControlSettings FromJson(string json, out IReadOnlyList<string> problems)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var found = new List<string>();
            problems = new ReadOnlyCollection<string>(found);

            object parsed;
            try
            {
                parsed = SaveJson.Parse(json);
            }
            catch (FormatException e)
            {
                found.Add("not valid JSON: " + e.Message);
                return Default;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                found.Add("the root must be an object");
                return Default;
            }

            // 形式が変わったときに古い解釈で読み違えないよう、読めるバージョンでなければ中身は見ない。
            if (!root.TryGetValue(_versionKey, out var versionValue))
            {
                found.Add($"missing '{_versionKey}'");
                return Default;
            }

            if (!(versionValue is long version) || version < OldestReadableVersion || version > CurrentVersion)
            {
                found.Add(
                    $"unsupported version {Describe(versionValue)} (expected {Format(OldestReadableVersion)} to {Format(CurrentVersion)})");
                return Default;
            }

            var hasValues = version >= 2;
            var hasShowNetStats = version >= 3;
            var knownKeys = hasShowNetStats ? _rootKeys : hasValues ? _versionTwoRootKeys : _versionOneRootKeys;
            ReportUnknownKeys(root, knownKeys, string.Empty, found);
            var modes = (HoldMode[])Default._modes.Clone();
            ReadModes(root, modes, found);
            var amounts = (int[])Default._amounts.Clone();
            if (hasValues)
            {
                ReadValues(root, amounts, found);
            }

            var showNetStats = hasShowNetStats ? ReadShowNetStats(root, found) : Default._showNetStats;
            return new ControlSettings(modes, amounts, showNetStats);
        }

        public bool Equals(ControlSettings other)
        {
            if (other is null)
            {
                return false;
            }

            for (var i = 0; i < _modes.Length; i++)
            {
                if (_modes[i] != other._modes[i])
                {
                    return false;
                }
            }

            for (var i = 0; i < _amounts.Length; i++)
            {
                if (_amounts[i] != other._amounts[i])
                {
                    return false;
                }
            }

            return _showNetStats == other._showNetStats;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ControlSettings);
        }

        public override int GetHashCode()
        {
            var hash = 0;
            foreach (var mode in _modes)
            {
                hash = (hash << 1) | (int)mode;
            }

            foreach (var amount in _amounts)
            {
                hash = unchecked((hash * 31) + amount);
            }

            return unchecked((hash * 31) + (_showNetStats ? 1 : 0));
        }

        public override string ToString()
        {
            return ToJson();
        }

        private static void ReadModes(Dictionary<string, object> root, HoldMode[] modes, List<string> problems)
        {
            foreach (var device in _devices)
            {
                var deviceKey = DeviceKey(device);
                if (!root.TryGetValue(deviceKey, out var deviceValue))
                {
                    problems.Add($"missing '{deviceKey}'");
                    continue;
                }

                if (!(deviceValue is Dictionary<string, object> fields))
                {
                    problems.Add($"'{deviceKey}' must be an object");
                    continue;
                }

                ReportUnknownKeys(fields, _actionKeys, deviceKey + ".", problems);
                foreach (var action in _actions)
                {
                    var path = deviceKey + "." + ActionKey(action);
                    if (!fields.TryGetValue(ActionKey(action), out var modeValue))
                    {
                        problems.Add($"missing '{path}'");
                    }
                    else if (!(modeValue is string name) || !TryParseMode(name, out var mode))
                    {
                        problems.Add(
                            $"'{path}' must be \"{_holdName}\" or \"{_toggleName}\" but was {Describe(modeValue)}");
                    }
                    else
                    {
                        modes[Index(device, action)] = mode;
                    }
                }
            }
        }

        private static void ReadValues(Dictionary<string, object> root, int[] amounts, List<string> problems)
        {
            foreach (var value in _values)
            {
                var key = KeyOf(value);
                var index = ValueIndex(value);
                var range = _ranges[index];
                if (!root.TryGetValue(key, out var raw))
                {
                    problems.Add($"missing '{key}'");
                }
                else if (!(raw is long number))
                {
                    problems.Add($"'{key}' must be an integer but was {Describe(raw)}");
                }
                else if (!range.Contains(number))
                {
                    // 範囲を狭めた後の古い値でも、初期値に戻すより近い値の方が遊んでいた感覚に近い。
                    amounts[index] = range.Clamp(number);
                    problems.Add(
                        $"'{key}' {Format(number)} is outside {Format(range.Min)} to {Format(range.Max)}; using {Format(amounts[index])}");
                }
                else
                {
                    amounts[index] = (int)number;
                }
            }
        }

        private static bool ReadShowNetStats(Dictionary<string, object> root, List<string> problems)
        {
            if (!root.TryGetValue(_showNetStatsKey, out var raw))
            {
                problems.Add($"missing '{_showNetStatsKey}'");
                return Default._showNetStats;
            }

            if (!(raw is bool show))
            {
                problems.Add($"'{_showNetStatsKey}' must be true or false but was {Describe(raw)}");
                return Default._showNetStats;
            }

            return show;
        }

        private float Percent(ControlValue value)
        {
            return Get(value) / 100f;
        }

        private static int[] DefaultValues()
        {
            var amounts = new int[_ranges.Length];
            for (var i = 0; i < _ranges.Length; i++)
            {
                amounts[i] = _ranges[i].Default;
            }

            return amounts;
        }

        private static int Index(InputDeviceKind device, HoldAction action)
        {
            int deviceIndex;
            switch (device)
            {
                case InputDeviceKind.KeyboardMouse:
                    deviceIndex = 0;
                    break;
                case InputDeviceKind.Gamepad:
                    deviceIndex = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(device), device, "unknown input device kind");
            }

            int actionIndex;
            switch (action)
            {
                case HoldAction.Sprint:
                    actionIndex = 0;
                    break;
                case HoldAction.Crouch:
                    actionIndex = 1;
                    break;
                case HoldAction.Aim:
                    actionIndex = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "unknown hold action");
            }

            return (deviceIndex * _actions.Length) + actionIndex;
        }

        private static int ValueIndex(ControlValue value)
        {
            var index = Array.IndexOf(_values, value);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }

            return index;
        }

        // 保存した文字列での名前。enum の名前を変えても形式が変わらないよう、ここで固定する。
        private static string DeviceKey(InputDeviceKind device)
        {
            return device == InputDeviceKind.Gamepad ? _gamepadKey : _keyboardMouseKey;
        }

        private static string ActionKey(HoldAction action)
        {
            switch (action)
            {
                case HoldAction.Sprint:
                    return _sprintKey;
                case HoldAction.Crouch:
                    return _crouchKey;
                default:
                    return _aimKey;
            }
        }

        private static string KeyOf(ControlValue value)
        {
            switch (value)
            {
                case ControlValue.MouseSensitivity:
                    return _mouseSensitivityKey;
                case ControlValue.GamepadSensitivity:
                    return _gamepadSensitivityKey;
                case ControlValue.AdsSensitivity:
                    return _adsSensitivityKey;
                case ControlValue.FieldOfView:
                    return _fieldOfViewKey;
                case ControlValue.MasterVolume:
                    return _masterVolumeKey;
                case ControlValue.EffectsVolume:
                    return _effectsVolumeKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }
        }

        private static string ModeName(HoldMode mode)
        {
            return mode == HoldMode.Toggle ? _toggleName : _holdName;
        }

        private static bool TryParseMode(string name, out HoldMode mode)
        {
            switch (name)
            {
                case _holdName:
                    mode = HoldMode.Hold;
                    return true;
                case _toggleName:
                    mode = HoldMode.Toggle;
                    return true;
                default:
                    mode = default;
                    return false;
            }
        }

        private static string Format(long number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        private static StringBuilder AppendKey(StringBuilder builder, string key)
        {
            SaveJson.AppendString(builder, key);
            return builder.Append(':');
        }

        private static void ReportUnknownKeys(
            Dictionary<string, object> fields,
            string[] known,
            string prefix,
            List<string> problems)
        {
            foreach (var key in fields.Keys)
            {
                if (Array.IndexOf(known, key) < 0)
                {
                    problems.Add($"ignored unknown key '{prefix}{key}'");
                }
            }
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    var builder = new StringBuilder();
                    SaveJson.AppendString(builder, text);
                    return builder.ToString();
                case long number:
                    return Format(number);
                case bool flag:
                    return flag ? "true" : "false";
                case List<object> _:
                    return "an array";
                default:
                    return "an object";
            }
        }
    }
}
