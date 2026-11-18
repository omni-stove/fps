using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    public sealed class NetV2WeatherPart : INetV2ClientPart
    {
        public const float SearchIntervalSeconds = 1f;

        private readonly Func<WeatherController> _find;
        private readonly Action<string> _log;
        private WeatherController _appliedTo;
        private string _weatherId;
        private string _timeOfDayId;
        private bool _received;
        private bool _missingLogged;
        private float _untilSearch;
        private bool _disposed;

        public NetV2WeatherPart(Func<WeatherController> find = null, Action<string> log = null)
        {
            _find = find ?? FindInScene;
            _log = log ?? (line => Debug.Log(line));
        }

        public WeatherController AppliedTo => _appliedTo;

        public string WeatherId => _weatherId;

        public string TimeOfDayId => _timeOfDayId;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            var rules = snapshot.Match.Rules;
            var weatherId = Normalized(rules.WeatherId);
            var timeOfDayId = Normalized(rules.TimeOfDayId);
            var changed = !_received
                || !string.Equals(weatherId, _weatherId, StringComparison.Ordinal)
                || !string.Equals(timeOfDayId, _timeOfDayId, StringComparison.Ordinal);
            _received = true;
            _weatherId = weatherId;
            _timeOfDayId = timeOfDayId;

            // マップが替わるとシーンごと WeatherController も作り直されるので、Unity の破棄済みの参照は待たずに探し直す。
            var controller = _appliedTo;
            if (controller == null)
            {
                var lost = !ReferenceEquals(controller, null);
                _appliedTo = null;
                if (!changed && !lost && _untilSearch > 0f)
                {
                    return;
                }

                _untilSearch = SearchIntervalSeconds;
                controller = _find();
                if (controller == null)
                {
                    if (!_missingLogged)
                    {
                        _missingLogged = true;
                        _log(WeatherLog.NoController(weatherId, timeOfDayId));
                    }

                    return;
                }
            }
            else if (!changed)
            {
                return;
            }

            _appliedTo = controller;
            _missingLogged = false;
            controller.Select(weatherId, timeOfDayId);
        }

        public void Tick(float deltaSeconds)
        {
            if (!_disposed && _untilSearch > 0f)
            {
                _untilSearch -= deltaSeconds;
            }
        }

        // 選んだ空は WeatherController が自分の破棄で元に戻すので、部品からは手を引くだけにする。
        public void Dispose()
        {
            _disposed = true;
            _appliedTo = null;
        }

        private static string Normalized(string id)
        {
            return WeatherChoice.IsUnset(id) ? null : id.Trim();
        }

        private static WeatherController FindInScene()
        {
            return UnityEngine.Object.FindAnyObjectByType<WeatherController>();
        }
    }
}
