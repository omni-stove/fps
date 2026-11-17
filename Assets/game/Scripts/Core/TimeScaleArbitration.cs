using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // Core から Unity の時間には触らないので、書く値は Step が返し、Runtime の TimeScaleArbiter が書く。
    // 秒数は実時間で数える (遅くした時間で数えると、遅くするほど長引くため)。
    // ポーズ中は秒数を数えず、速さも書かない (ポーズが止めた速さを優先する)。ポーズを閉じた側が開く前の速さへ戻しても、
    // 次の Step で今の要求の速さを書き直すので、ポーズ中に要求が増えたり消えたりしても解除後に正しい速さになる。
    public sealed class TimeScaleArbitration
    {
        public const string LogTag = "[TimeScale]";

        public const float NormalScale = 1f;

        // Unity が受け付ける fixedDeltaTime の下限なので、とても遅い倍率でもこれより小さくしない。
        public const float MinFixedDeltaTime = 0.0001f;

        // 書いた値を読み直すと float の丸めで僅かにずれることがあるので、毎フレーム書き直さないための幅。
        private const float _tolerance = 1e-6f;

        private readonly List<Entry> _requests = new List<Entry>();
        private bool _applied;
        private float _baseTimeScale = NormalScale;
        private float _baseFixedDeltaTime;

        public float Scale
        {
            get
            {
                var slowest = NormalScale;
                foreach (var entry in _requests)
                {
                    slowest = Math.Min(slowest, entry.Scale);
                }

                return slowest;
            }
        }

        public bool HasRequests => _requests.Count > 0;

        public bool IsApplied => _applied;

        public bool IsPaused { get; private set; }

        public float BaseTimeScale => _baseTimeScale;

        public float BaseFixedDeltaTime => _baseFixedDeltaTime;

        public IReadOnlyList<TimeScaleRequest> Requests
        {
            get
            {
                var copy = new TimeScaleRequest[_requests.Count];
                for (var i = 0; i < copy.Length; i++)
                {
                    copy[i] = _requests[i].ToRequest();
                }

                return copy;
            }
        }

        public static TimeScaleRefusal Check(SlowMotion rule, bool networkSession)
        {
            if (networkSession)
            {
                return TimeScaleRefusal.NetworkSession;
            }

            return rule == SlowMotion.On ? TimeScaleRefusal.None : TimeScaleRefusal.RuleOff;
        }

        public static void Validate(string reason, float scale, float seconds)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("a time scale request needs a reason", nameof(reason));
            }

            // 0 はポーズと見分けが付かず、1 より速くすると「一番遅いもの」を選ぶ意味が無くなる。
            if (!(scale > 0f) || scale > NormalScale)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "time scale must be greater than 0 and at most 1");
            }

            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a positive finite number");
            }
        }

        public bool Request(string reason, float scale, float seconds)
        {
            Validate(reason, scale, seconds);

            var existing = Find(reason);
            if (existing != null)
            {
                existing.Scale = scale;
                existing.RemainingSeconds = seconds;
                return false;
            }

            _requests.Add(new Entry(reason, scale, seconds));
            return true;
        }

        public bool Cancel(string reason)
        {
            var existing = Find(reason);
            if (existing == null)
            {
                return false;
            }

            _requests.Remove(existing);
            return true;
        }

        public IReadOnlyList<TimeScaleRequest> CancelAll()
        {
            var cancelled = Requests;
            _requests.Clear();
            return cancelled;
        }

        // 速さは戻さないので、Unity の時間が既に元に戻っているとき (Play の開始) にだけ使う。
        public void Reset()
        {
            _requests.Clear();
            _applied = false;
            _baseTimeScale = NormalScale;
            _baseFixedDeltaTime = 0f;
            IsPaused = false;
        }

        public bool Step(
            float realDeltaSeconds,
            bool paused,
            float currentTimeScale,
            float currentFixedDeltaTime,
            ICollection<TimeScaleRequest> expired,
            out float timeScale,
            out float fixedDeltaTime)
        {
            if (!(realDeltaSeconds >= 0f) || float.IsInfinity(realDeltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(realDeltaSeconds), realDeltaSeconds, "delta must be a non-negative finite number");
            }

            if (!(currentTimeScale >= 0f) || float.IsInfinity(currentTimeScale))
            {
                throw new ArgumentOutOfRangeException(nameof(currentTimeScale), currentTimeScale, "time scale must be a non-negative finite number");
            }

            if (!(currentFixedDeltaTime > 0f) || float.IsInfinity(currentFixedDeltaTime))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentFixedDeltaTime), currentFixedDeltaTime, "fixed delta time must be a positive finite number");
            }

            timeScale = currentTimeScale;
            fixedDeltaTime = currentFixedDeltaTime;
            IsPaused = paused;
            if (paused)
            {
                return false;
            }

            Advance(realDeltaSeconds, expired);

            if (_requests.Count > 0)
            {
                if (!_applied)
                {
                    _applied = true;
                    _baseTimeScale = currentTimeScale;
                    _baseFixedDeltaTime = currentFixedDeltaTime;
                }

                var scale = Scale;
                var targetFixed = Math.Max(MinFixedDeltaTime, _baseFixedDeltaTime * scale);
                if (Differs(currentTimeScale, scale) || Differs(currentFixedDeltaTime, targetFixed))
                {
                    timeScale = scale;
                    fixedDeltaTime = targetFixed;
                    return true;
                }

                return false;
            }

            if (!_applied)
            {
                return false;
            }

            _applied = false;
            timeScale = _baseTimeScale;
            fixedDeltaTime = _baseFixedDeltaTime;
            return Differs(currentTimeScale, timeScale) || Differs(currentFixedDeltaTime, fixedDeltaTime);
        }

        public static string FormatRequest(TimeScaleRequest request)
        {
            return $"reason={request.Reason} scale={Number(request.Scale)} remaining={Number(request.RemainingSeconds)}";
        }

        public static string FormatAccepted(string reason, float scale, float seconds, float slowest)
        {
            return $"{LogTag} accepted reason={reason} scale={Number(scale)} seconds={Number(seconds)} slowest={Number(slowest)}";
        }

        public static string FormatRefused(string reason, float scale, float seconds, TimeScaleRefusal refusal)
        {
            return $"{LogTag} refused reason={reason} scale={Number(scale)} seconds={Number(seconds)} because={RefusalName(refusal)}";
        }

        public static string FormatExpired(TimeScaleRequest request)
        {
            return $"{LogTag} expired reason={request.Reason}";
        }

        public static string FormatCancelled(TimeScaleRequest request, string because)
        {
            return $"{LogTag} cancelled reason={request.Reason} because={because}";
        }

        public static string FormatSet(float timeScale, float fixedDeltaTime)
        {
            return $"{LogTag} set timeScale={Number(timeScale)} fixedDeltaTime={Number(fixedDeltaTime)}";
        }

        public static string FormatRestored(float timeScale, float fixedDeltaTime)
        {
            return $"{LogTag} restored timeScale={Number(timeScale)} fixedDeltaTime={Number(fixedDeltaTime)}";
        }

        public static string FormatPause(bool paused, int requestCount)
        {
            return paused
                ? $"{LogTag} paused; holding {requestCount} request(s)"
                : $"{LogTag} resumed; {requestCount} request(s) remain";
        }

        public static string RefusalName(TimeScaleRefusal refusal)
        {
            switch (refusal)
            {
                case TimeScaleRefusal.None: return "none";
                case TimeScaleRefusal.RuleOff: return "slowMotionOff";
                case TimeScaleRefusal.NetworkSession: return "networkSession";
                default: return refusal.ToString();
            }
        }

        private void Advance(float realDeltaSeconds, ICollection<TimeScaleRequest> expired)
        {
            var i = 0;
            while (i < _requests.Count)
            {
                var entry = _requests[i];
                entry.RemainingSeconds -= realDeltaSeconds;
                if (entry.RemainingSeconds > 0f)
                {
                    i++;
                    continue;
                }

                entry.RemainingSeconds = 0f;
                _requests.RemoveAt(i);
                expired?.Add(entry.ToRequest());
            }
        }

        private Entry Find(string reason)
        {
            if (reason == null)
            {
                return null;
            }

            foreach (var entry in _requests)
            {
                if (string.Equals(entry.Reason, reason, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool Differs(float a, float b)
        {
            return Math.Abs(a - b) > _tolerance;
        }

        private static string Number(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private sealed class Entry
        {
            public Entry(string reason, float scale, float remainingSeconds)
            {
                Reason = reason;
                Scale = scale;
                RemainingSeconds = remainingSeconds;
            }

            public string Reason { get; }

            public float Scale { get; set; }

            public float RemainingSeconds { get; set; }

            public TimeScaleRequest ToRequest()
            {
                return new TimeScaleRequest(Reason, Scale, RemainingSeconds);
            }
        }
    }
}
