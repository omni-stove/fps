using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 画面の言語は持たない。何をどの言語で出すかは game 側の UI が決め、自分で保存する。
    public sealed class ControlSettings : IEquatable<ControlSettings>
    {
        public const int CurrentVersion = 11;

        // 保存した文字列を見て地域を決めていないと分かるよう、空文字や null でなく名前で持つ。
        public const string AutoRegion = "auto";

        public const int MaxRegionLength = 32;

        // 6・7 は画面の言語 (language) も持つが、言語は game 側が自分で保存するので読み飛ばす。
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
        private const string _paletteKey = "colorPalette";
        private const string _showSubtitlesKey = "showSubtitles";
        private const string _languageKey = "language";
        private const string _cameraShakeKey = "cameraShake";
        private const string _viewmodelMotionKey = "viewmodelMotion";
        private const string _showBloodKey = "showBlood";
        private const string _aimAssistKey = "aimAssist";
        private const string _invertLookYKey = "invertLookY";
        private const string _stickDeadzoneKey = "stickDeadzone";
        private const string _rumbleKey = "rumble";
        private const string _rumbleStrengthKey = "rumbleStrength";
        private const string _quickMatchRegionKey = "quickMatchRegion";
        private const string _telemetryConsentKey = "telemetryConsent";
        private const string _standardPaletteName = "standard";
        private const string _redGreenPaletteName = "redGreen";
        private const string _blueYellowPaletteName = "blueYellow";
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
            ControlValue.CameraShakeIntensity,
            ControlValue.ViewmodelMotionIntensity,
            ControlValue.StickDeadzone,
            ControlValue.RumbleStrength,
        };

        private static readonly ControlValueRange[] _ranges =
        {
            new ControlValueRange(10, 500, 100),
            new ControlValueRange(10, 500, 100),
            new ControlValueRange(10, 200, 100),
            // 縦の視野角。60 は Unity のカメラの既定値で、今までの見え方を初期値にする。90 を超えると周りが大きく歪む。
            new ControlValueRange(60, 90, 60),
            new ControlValueRange(0, 100, 100),
            new ControlValueRange(0, 100, 100),
            new ControlValueRange(0, 100, 100),
            new ControlValueRange(0, 100, 100),
            // 0.125 を整数で持てるよう ‰ にする。0.125 は Input System のスティックの既定のデッドゾーンで、今までの効き方を初期値にする。
            new ControlValueRange(0, 500, 125),
            new ControlValueRange(0, 100, 100),
        };

        // 古い形式に無い値を、欠けた値として警告しないために、その値を書き始めた形式のバージョンを持つ。
        private static readonly int[] _valueSinceVersions = { 2, 2, 2, 2, 2, 2, 7, 7, 10, 10 };

        private static readonly string[] _versionOneRootKeys = { _versionKey, _keyboardMouseKey, _gamepadKey };

        private static readonly string[] _versionTwoRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
        };

        private static readonly string[] _versionThreeRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _showNetStatsKey,
        };

        private static readonly string[] _versionFourRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _showNetStatsKey, _paletteKey,
        };

        private static readonly string[] _versionFiveRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey,
        };

        private static readonly string[] _versionSixRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey, _languageKey,
        };

        private static readonly string[] _versionSevenRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _cameraShakeKey, _viewmodelMotionKey, _showBloodKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey, _languageKey,
        };

        private static readonly string[] _versionEightRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _cameraShakeKey, _viewmodelMotionKey, _showBloodKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey,
        };

        private static readonly string[] _versionNineRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _cameraShakeKey, _viewmodelMotionKey, _showBloodKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey, _aimAssistKey,
        };

        private static readonly string[] _versionTenRootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _cameraShakeKey, _viewmodelMotionKey, _showBloodKey,
            _invertLookYKey, _stickDeadzoneKey, _rumbleKey, _rumbleStrengthKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey, _aimAssistKey,
        };

        private static readonly string[] _rootKeys =
        {
            _versionKey, _keyboardMouseKey, _gamepadKey,
            _mouseSensitivityKey, _gamepadSensitivityKey, _adsSensitivityKey,
            _fieldOfViewKey, _masterVolumeKey, _effectsVolumeKey,
            _cameraShakeKey, _viewmodelMotionKey, _showBloodKey,
            _invertLookYKey, _stickDeadzoneKey, _rumbleKey, _rumbleStrengthKey,
            _showNetStatsKey, _paletteKey, _showSubtitlesKey, _aimAssistKey,
            _quickMatchRegionKey, _telemetryConsentKey,
        };

        private static readonly string[] _actionKeys = { _sprintKey, _crouchKey, _aimKey };

        // ゲームパッドはスティックを押し込み続けにくいのでダッシュとしゃがみを Toggle にし、ADS はトリガーなので Hold にする。
        public static ControlSettings Default { get; } = new ControlSettings(
            new[]
            {
                HoldMode.Hold, HoldMode.Hold, HoldMode.Hold,
                HoldMode.Toggle, HoldMode.Toggle, HoldMode.Hold,
            },
            DefaultValues(),
            false,
            ColorPalette.Standard,
            false,
            true,
            true,
            false,
            true,
            AutoRegion,
            false);

        private readonly HoldMode[] _modes;

        private readonly int[] _amounts;

        private readonly bool _showNetStats;

        private readonly ColorPalette _palette;

        private readonly bool _showSubtitles;

        private readonly bool _showBlood;

        private readonly bool _aimAssist;

        private readonly bool _invertLookY;

        private readonly bool _rumbleEnabled;

        private readonly string _quickMatchRegion;

        private readonly bool _telemetryConsent;

        private ControlSettings(
            HoldMode[] modes,
            int[] amounts,
            bool showNetStats,
            ColorPalette palette,
            bool showSubtitles,
            bool showBlood,
            bool aimAssist,
            bool invertLookY,
            bool rumbleEnabled,
            string quickMatchRegion,
            bool telemetryConsent)
        {
            _quickMatchRegion = quickMatchRegion;
            _telemetryConsent = telemetryConsent;
            _modes = modes;
            _amounts = amounts;
            _showNetStats = showNetStats;
            _palette = palette;
            _showSubtitles = showSubtitles;
            _showBlood = showBlood;
            _aimAssist = aimAssist;
            _invertLookY = invertLookY;
            _rumbleEnabled = rumbleEnabled;
        }

        public static IReadOnlyList<ControlValue> Values { get; } = new ReadOnlyCollection<ControlValue>(_values);

        public static ControlValueRange RangeOf(ControlValue value)
        {
            return _ranges[ValueIndex(value)];
        }

        public HoldMode Get(InputDeviceKind device, HoldAction action)
        {
            return _modes[Index(device, action)];
        }

        public int Get(ControlValue value)
        {
            return _amounts[ValueIndex(value)];
        }

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

        public float AdsSensitivity => Percent(ControlValue.AdsSensitivity);

        public float FieldOfView => Get(ControlValue.FieldOfView);

        public float MasterVolume => Percent(ControlValue.MasterVolume);

        public float EffectsVolume => Percent(ControlValue.EffectsVolume);

        public bool ShowNetStats => _showNetStats;

        public ColorPalette Palette => _palette;

        public bool ShowSubtitles => _showSubtitles;

        public float CameraShakeIntensity => Percent(ControlValue.CameraShakeIntensity);

        public float ViewmodelMotionIntensity => Percent(ControlValue.ViewmodelMotionIntensity);

        public bool ShowBlood => _showBlood;

        public bool AimAssist => _aimAssist;

        public bool InvertLookY => _invertLookY;

        public float ApplyInvertLookY(float lookUp)
        {
            return _invertLookY ? -lookUp : lookUp;
        }

        public float StickDeadzone => Get(ControlValue.StickDeadzone) / 1000f;

        public bool RumbleEnabled => _rumbleEnabled;

        public float RumbleStrength => Percent(ControlValue.RumbleStrength);

        public string QuickMatchRegion => _quickMatchRegion;

        public bool PrefersAnyRegion => _quickMatchRegion == AutoRegion;

        public bool TelemetryConsent => _telemetryConsent;

        // サーバーが --region で載せる名前と突き合わせるので、大文字小文字や空白の揺れで別の地域にならないよう、書ける文字を絞る。
        public static bool IsValidRegion(string region)
        {
            if (string.IsNullOrEmpty(region) || region.Length > MaxRegionLength)
            {
                return false;
            }

            for (var i = 0; i < region.Length; i++)
            {
                var c = region[i];
                var letterOrDigit = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!letterOrDigit && (c != '-' || i == 0 || i == region.Length - 1))
                {
                    return false;
                }
            }

            return true;
        }

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
            return new ControlSettings(
                modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                _quickMatchRegion, _telemetryConsent);
        }

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
            return new ControlSettings(
                _modes, amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithShowNetStats(bool show)
        {
            return show == _showNetStats
                ? this
                : new ControlSettings(
                    _modes, _amounts, show, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithPalette(ColorPalette palette)
        {
            PaletteName(palette);
            return palette == _palette
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithShowSubtitles(bool show)
        {
            return show == _showSubtitles
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, show, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithShowBlood(bool show)
        {
            return show == _showBlood
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, show, _aimAssist, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithAimAssist(bool enabled)
        {
            return enabled == _aimAssist
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, enabled, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithInvertLookY(bool invert)
        {
            return invert == _invertLookY
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, invert, _rumbleEnabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithStickDeadzone(int permille)
        {
            return With(ControlValue.StickDeadzone, permille);
        }

        public ControlSettings WithRumbleEnabled(bool enabled)
        {
            return enabled == _rumbleEnabled
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, enabled,
                    _quickMatchRegion, _telemetryConsent);
        }

        public ControlSettings WithRumbleStrength(int percent)
        {
            return With(ControlValue.RumbleStrength, percent);
        }

        public ControlSettings WithQuickMatchRegion(string region)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            if (!IsValidRegion(region))
            {
                throw new ArgumentException(
                    $"'{region}' is not a region name ({RegionRule()})",
                    nameof(region));
            }

            return region == _quickMatchRegion
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                    region, _telemetryConsent);
        }

        public ControlSettings WithTelemetryConsent(bool consent)
        {
            return consent == _telemetryConsent
                ? this
                : new ControlSettings(
                    _modes, _amounts, _showNetStats, _palette, _showSubtitles, _showBlood, _aimAssist, _invertLookY, _rumbleEnabled,
                    _quickMatchRegion, consent);
        }

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
                // 版 10 で足した数値は、版 9 までの並びを崩さないよう、同じ版で足した真偽値と並べて後で書く。
                if (value == ControlValue.StickDeadzone || value == ControlValue.RumbleStrength)
                {
                    continue;
                }

                builder.Append(',');
                AppendKey(builder, KeyOf(value)).Append(Format(Get(value)));
            }

            builder.Append(',');
            AppendKey(builder, _showBloodKey).Append(_showBlood ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _invertLookYKey).Append(_invertLookY ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _stickDeadzoneKey).Append(Format(Get(ControlValue.StickDeadzone)));
            builder.Append(',');
            AppendKey(builder, _rumbleKey).Append(_rumbleEnabled ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _rumbleStrengthKey).Append(Format(Get(ControlValue.RumbleStrength)));
            builder.Append(',');
            AppendKey(builder, _showNetStatsKey).Append(_showNetStats ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _paletteKey);
            SaveJson.AppendString(builder, PaletteName(_palette));
            builder.Append(',');
            AppendKey(builder, _showSubtitlesKey).Append(_showSubtitles ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _aimAssistKey).Append(_aimAssist ? "true" : "false");
            builder.Append(',');
            AppendKey(builder, _quickMatchRegionKey);
            SaveJson.AppendString(builder, _quickMatchRegion);
            builder.Append(',');
            AppendKey(builder, _telemetryConsentKey).Append(_telemetryConsent ? "true" : "false");
            builder.Append('}');
            return builder.ToString();
        }

        public static ControlSettings FromJson(string json, out IReadOnlyList<string> problems)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var fallback = Default;
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
                return fallback;
            }

            if (!(parsed is Dictionary<string, object> root))
            {
                found.Add("the root must be an object");
                return fallback;
            }

            // 形式が変わったときに古い解釈で読み違えないよう、読めるバージョンでなければ中身は見ない。
            if (!root.TryGetValue(_versionKey, out var versionValue))
            {
                found.Add($"missing '{_versionKey}'");
                return fallback;
            }

            if (!(versionValue is long version) || version < OldestReadableVersion || version > CurrentVersion)
            {
                found.Add(
                    $"unsupported version {Describe(versionValue)} (expected {Format(OldestReadableVersion)} to {Format(CurrentVersion)})");
                return fallback;
            }

            var hasValues = version >= 2;
            var hasShowNetStats = version >= 3;
            var hasPalette = version >= 4;
            var hasShowSubtitles = version >= 5;
            var hasPresentation = version >= 7;
            var hasAimAssist = version >= 9;
            var hasGamepadFeel = version >= 10;
            var hasOnline = version >= 11;
            var knownKeys = hasOnline ? _rootKeys
                : hasGamepadFeel ? _versionTenRootKeys
                : hasAimAssist ? _versionNineRootKeys
                : version >= 8 ? _versionEightRootKeys
                : hasPresentation ? _versionSevenRootKeys
                : version >= 6 ? _versionSixRootKeys
                : hasShowSubtitles ? _versionFiveRootKeys
                : hasPalette ? _versionFourRootKeys
                : hasShowNetStats ? _versionThreeRootKeys
                : hasValues ? _versionTwoRootKeys
                : _versionOneRootKeys;
            ReportUnknownKeys(root, knownKeys, string.Empty, found);
            var modes = (HoldMode[])Default._modes.Clone();
            ReadModes(root, modes, found);
            var amounts = (int[])Default._amounts.Clone();
            if (hasValues)
            {
                ReadValues(root, version, amounts, found);
            }

            var showNetStats = hasShowNetStats ? ReadFlag(root, _showNetStatsKey, Default._showNetStats, found) : Default._showNetStats;
            var palette = hasPalette ? ReadPalette(root, found) : Default._palette;
            var showSubtitles = hasShowSubtitles
                ? ReadFlag(root, _showSubtitlesKey, Default._showSubtitles, found)
                : Default._showSubtitles;
            var showBlood = hasPresentation ? ReadFlag(root, _showBloodKey, Default._showBlood, found) : Default._showBlood;
            var aimAssist = hasAimAssist ? ReadFlag(root, _aimAssistKey, Default._aimAssist, found) : Default._aimAssist;
            var invertLookY = hasGamepadFeel ? ReadFlag(root, _invertLookYKey, Default._invertLookY, found) : Default._invertLookY;
            var rumbleEnabled = hasGamepadFeel ? ReadFlag(root, _rumbleKey, Default._rumbleEnabled, found) : Default._rumbleEnabled;
            var region = hasOnline ? ReadRegion(root, found) : Default._quickMatchRegion;
            var telemetryConsent = hasOnline
                ? ReadFlag(root, _telemetryConsentKey, Default._telemetryConsent, found)
                : Default._telemetryConsent;
            return new ControlSettings(
                modes, amounts, showNetStats, palette, showSubtitles, showBlood, aimAssist, invertLookY, rumbleEnabled,
                region, telemetryConsent);
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

            return _showNetStats == other._showNetStats
                && _palette == other._palette
                && _showSubtitles == other._showSubtitles
                && _showBlood == other._showBlood
                && _aimAssist == other._aimAssist
                && _invertLookY == other._invertLookY
                && _rumbleEnabled == other._rumbleEnabled
                && string.Equals(_quickMatchRegion, other._quickMatchRegion, StringComparison.Ordinal)
                && _telemetryConsent == other._telemetryConsent;
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

            hash = unchecked((hash * 31) + (_showNetStats ? 1 : 0));
            hash = unchecked((hash * 31) + (int)_palette);
            hash = unchecked((hash * 31) + (_showSubtitles ? 1 : 0));
            hash = unchecked((hash * 31) + (_showBlood ? 1 : 0));
            hash = unchecked((hash * 31) + (_aimAssist ? 1 : 0));
            hash = unchecked((hash * 31) + (_invertLookY ? 1 : 0));
            hash = unchecked((hash * 31) + (_rumbleEnabled ? 1 : 0));
            hash = unchecked((hash * 31) + StringComparer.Ordinal.GetHashCode(_quickMatchRegion));
            return unchecked((hash * 31) + (_telemetryConsent ? 1 : 0));
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

        private static void ReadValues(Dictionary<string, object> root, long version, int[] amounts, List<string> problems)
        {
            foreach (var value in _values)
            {
                var key = KeyOf(value);
                var index = ValueIndex(value);
                if (version < _valueSinceVersions[index])
                {
                    continue;
                }

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

        private static bool ReadFlag(Dictionary<string, object> root, string key, bool fallback, List<string> problems)
        {
            if (!root.TryGetValue(key, out var raw))
            {
                problems.Add($"missing '{key}'");
                return fallback;
            }

            if (!(raw is bool flag))
            {
                problems.Add($"'{key}' must be true or false but was {Describe(raw)}");
                return fallback;
            }

            return flag;
        }

        private static ColorPalette ReadPalette(Dictionary<string, object> root, List<string> problems)
        {
            if (!root.TryGetValue(_paletteKey, out var raw))
            {
                problems.Add($"missing '{_paletteKey}'");
                return Default._palette;
            }

            if (!(raw is string name) || !TryParsePalette(name, out var palette))
            {
                problems.Add(
                    $"'{_paletteKey}' must be \"{_standardPaletteName}\", \"{_redGreenPaletteName}\" or \"{_blueYellowPaletteName}\" but was {Describe(raw)}");
                return Default._palette;
            }

            return palette;
        }

        private static string ReadRegion(Dictionary<string, object> root, List<string> problems)
        {
            if (!root.TryGetValue(_quickMatchRegionKey, out var raw))
            {
                problems.Add($"missing '{_quickMatchRegionKey}'");
                return Default._quickMatchRegion;
            }

            if (!(raw is string region) || !IsValidRegion(region))
            {
                problems.Add($"'{_quickMatchRegionKey}' must be a region name ({RegionRule()}) but was {Describe(raw)}");
                return Default._quickMatchRegion;
            }

            return region;
        }

        private static string RegionRule()
        {
            return $"1 to {Format(MaxRegionLength)} of a-z, 0-9 and '-', not starting or ending with '-'";
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
                case ControlValue.CameraShakeIntensity:
                    return _cameraShakeKey;
                case ControlValue.ViewmodelMotionIntensity:
                    return _viewmodelMotionKey;
                case ControlValue.StickDeadzone:
                    return _stickDeadzoneKey;
                case ControlValue.RumbleStrength:
                    return _rumbleStrengthKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown control value");
            }
        }

        // 保存した文字列での名前。enum の名前を変えても形式が変わらないよう、ここで固定する。
        private static string PaletteName(ColorPalette palette)
        {
            switch (palette)
            {
                case ColorPalette.Standard:
                    return _standardPaletteName;
                case ColorPalette.RedGreen:
                    return _redGreenPaletteName;
                case ColorPalette.BlueYellow:
                    return _blueYellowPaletteName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(palette), palette, "unknown color palette");
            }
        }

        private static bool TryParsePalette(string name, out ColorPalette palette)
        {
            switch (name)
            {
                case _standardPaletteName:
                    palette = ColorPalette.Standard;
                    return true;
                case _redGreenPaletteName:
                    palette = ColorPalette.RedGreen;
                    return true;
                case _blueYellowPaletteName:
                    palette = ColorPalette.BlueYellow;
                    return true;
                default:
                    palette = default;
                    return false;
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
