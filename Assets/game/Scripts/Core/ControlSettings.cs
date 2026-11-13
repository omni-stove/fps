using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 操作設定。<see cref="HoldAction"/> ごとの <see cref="HoldMode"/> を、キーボード／マウスとゲームパッドで別々に持つ。不変。
    /// <see cref="ToJson"/> と <see cref="FromJson"/> で文字列にして保存する。
    /// </summary>
    public sealed class ControlSettings : IEquatable<ControlSettings>
    {
        /// <summary>このコードが書き出し、読み込める形式のバージョン。</summary>
        public const int CurrentVersion = 1;

        private const string _versionKey = "version";
        private const string _keyboardMouseKey = "keyboardMouse";
        private const string _gamepadKey = "gamepad";
        private const string _sprintKey = "sprint";
        private const string _crouchKey = "crouch";
        private const string _aimKey = "aim";
        private const string _holdName = "hold";
        private const string _toggleName = "toggle";

        private static readonly InputDeviceKind[] _devices = { InputDeviceKind.KeyboardMouse, InputDeviceKind.Gamepad };
        private static readonly HoldAction[] _actions = { HoldAction.Sprint, HoldAction.Crouch, HoldAction.Aim };
        private static readonly string[] _rootKeys = { _versionKey, _keyboardMouseKey, _gamepadKey };
        private static readonly string[] _actionKeys = { _sprintKey, _crouchKey, _aimKey };

        /// <summary>
        /// 初期値。キーボード／マウスはすべて Hold。
        /// ゲームパッドはスティックを押し込み続けにくいのでダッシュとしゃがみを Toggle にし、ADS はトリガーなので Hold。
        /// </summary>
        public static ControlSettings Default { get; } = new ControlSettings(new[]
        {
            HoldMode.Hold, HoldMode.Hold, HoldMode.Hold,
            HoldMode.Toggle, HoldMode.Toggle, HoldMode.Hold,
        });

        // 添字は Index(device, action)。
        private readonly HoldMode[] _modes;

        private ControlSettings(HoldMode[] modes)
        {
            _modes = modes;
        }

        /// <summary>device で action を操作するときのモード。</summary>
        public HoldMode Get(InputDeviceKind device, HoldAction action)
        {
            return _modes[Index(device, action)];
        }

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
            return new ControlSettings(modes);
        }

        /// <summary>JSON の文字列に書き出す。<see cref="FromJson"/> で同じ内容に戻る。</summary>
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendKey(builder, _versionKey).Append(CurrentVersion.ToString(CultureInfo.InvariantCulture));
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

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// <see cref="ToJson"/> で書き出した文字列から読む。壊れていても例外にはせず、読めなかった所は <see cref="Default"/> の値にする。
        /// JSON として壊れている・バージョンが違うときはすべて初期値、操作ごとの値が無い・型違い・知らない名前ならその操作だけ初期値にする。
        /// 知らない項目は読み飛ばす。
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

            // 形式が変わったときに古い解釈で読み違えないよう、バージョンが合わなければ中身は見ない。
            if (!root.TryGetValue(_versionKey, out var versionValue))
            {
                found.Add($"missing '{_versionKey}'");
                return Default;
            }

            if (!(versionValue is long version) || version != CurrentVersion)
            {
                found.Add(
                    $"unsupported version {Describe(versionValue)} (expected {CurrentVersion.ToString(CultureInfo.InvariantCulture)})");
                return Default;
            }

            ReportUnknownKeys(root, _rootKeys, string.Empty, found);
            var modes = (HoldMode[])Default._modes.Clone();
            foreach (var device in _devices)
            {
                var deviceKey = DeviceKey(device);
                if (!root.TryGetValue(deviceKey, out var deviceValue))
                {
                    found.Add($"missing '{deviceKey}'");
                    continue;
                }

                if (!(deviceValue is Dictionary<string, object> fields))
                {
                    found.Add($"'{deviceKey}' must be an object");
                    continue;
                }

                ReportUnknownKeys(fields, _actionKeys, deviceKey + ".", found);
                foreach (var action in _actions)
                {
                    var path = deviceKey + "." + ActionKey(action);
                    if (!fields.TryGetValue(ActionKey(action), out var modeValue))
                    {
                        found.Add($"missing '{path}'");
                    }
                    else if (!(modeValue is string name) || !TryParseMode(name, out var mode))
                    {
                        found.Add(
                            $"'{path}' must be \"{_holdName}\" or \"{_toggleName}\" but was {Describe(modeValue)}");
                    }
                    else
                    {
                        modes[Index(device, action)] = mode;
                    }
                }
            }

            return new ControlSettings(modes);
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

            return true;
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

            return hash;
        }

        public override string ToString()
        {
            return ToJson();
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
                    return number.ToString(CultureInfo.InvariantCulture);
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
