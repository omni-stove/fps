using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 時間の速さの要求 (理由・倍率・秒数) をまとめ、Time.timeScale と Time.fixedDeltaTime に入れる値を決める。
    /// Unity の時間には触らず、書く値は <see cref="Step"/> が返す (Runtime の TimeScaleArbiter が書く)。
    /// <para>
    /// 速さ: 受けている要求のうち一番遅いもの。fixedDeltaTime は、最初の要求を効かせる直前の値に同じ倍率を掛ける。
    /// 要求が無くなったら、最初の要求を効かせる直前の速さと fixedDeltaTime へ戻す。
    /// </para>
    /// <para>
    /// 秒数: 実時間で数える (遅くした時間で数えると、遅くするほど長引くため)。同じ理由でもう一度要求すると、倍率と残りの秒数を置き換える。
    /// </para>
    /// <para>
    /// ポーズ: ポーズ中は秒数を数えず、速さも書かない (ポーズが止めた速さを優先する)。ポーズを閉じた側が開く前の速さへ戻しても、
    /// 次の <see cref="Step"/> で今の要求の速さを書き直すので、ポーズ中に要求が増えたり消えたりしても解除後に正しい速さになる。
    /// </para>
    /// 受けてよいか (ルールとネット対戦) は <see cref="Check"/> で決め、ここでは要求を断らない。
    /// </summary>
    public sealed class TimeScaleArbitration
    {
        public const string LogTag = "[TimeScale]";

        /// <summary>要求が無いときの速さ。</summary>
        public const float NormalScale = 1f;

        /// <summary>Unity が受け付ける fixedDeltaTime の下限。とても遅い倍率でもこれより小さくしない。</summary>
        public const float MinFixedDeltaTime = 0.0001f;

        // 書いた値を読み直すと float の丸めで僅かにずれることがあるので、毎フレーム書き直さないための幅。
        private const float Tolerance = 1e-6f;

        private readonly List<Entry> _requests = new List<Entry>();
        private bool _applied;
        private float _baseTimeScale = NormalScale;
        private float _baseFixedDeltaTime;

        /// <summary>受けている要求のうち一番遅い倍率。要求が無ければ <see cref="NormalScale"/>。</summary>
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

        /// <summary>要求を 1 つ以上受けているか。</summary>
        public bool HasRequests => _requests.Count > 0;

        /// <summary>速さを書いていて、要求が無くなったら戻す値を覚えているか。</summary>
        public bool IsApplied => _applied;

        /// <summary>最後の <see cref="Step"/> がポーズ中だったか。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>要求が無くなったときに戻す速さ。<see cref="IsApplied"/> の間だけ意味がある。</summary>
        public float BaseTimeScale => _baseTimeScale;

        /// <summary>要求が無くなったときに戻す fixedDeltaTime。<see cref="IsApplied"/> の間だけ意味がある。</summary>
        public float BaseFixedDeltaTime => _baseFixedDeltaTime;

        /// <summary>受けている要求 (受けた順) の写し。</summary>
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

        /// <summary>
        /// 要求を受けてよいか。ネット対戦中はルールに関わらず <see cref="TimeScaleRefusal.NetworkSession"/>、
        /// ルールが On でなければ <see cref="TimeScaleRefusal.RuleOff"/>。
        /// </summary>
        public static TimeScaleRefusal Check(SlowMotion rule, bool networkSession)
        {
            if (networkSession)
            {
                return TimeScaleRefusal.NetworkSession;
            }

            return rule == SlowMotion.On ? TimeScaleRefusal.None : TimeScaleRefusal.RuleOff;
        }

        /// <summary>要求の値を確かめる。<see cref="Request"/> と同じ条件。</summary>
        /// <exception cref="ArgumentException">reason が null か空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">scale が 0 以下か 1 より大きいか有限でない。seconds が 0 以下か有限でない。</exception>
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

        /// <summary>
        /// 要求を受ける。同じ理由の要求があれば倍率と残りの秒数を置き換えて false、新しい理由なら true。
        /// 速さは次の <see cref="Step"/> で書く。
        /// </summary>
        /// <exception cref="ArgumentException">reason が null か空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">scale か seconds が範囲外 (<see cref="Validate"/>)。</exception>
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

        /// <summary>reason の要求を取り下げる。あれば true。速さは次の <see cref="Step"/> で書く。</summary>
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

        /// <summary>要求をすべて取り下げ、取り下げたもの (受けた順) を返す。速さは次の <see cref="Step"/> で戻す。</summary>
        public IReadOnlyList<TimeScaleRequest> CancelAll()
        {
            var cancelled = Requests;
            _requests.Clear();
            return cancelled;
        }

        /// <summary>
        /// 要求も、戻す値も忘れる。速さは戻さないので、Unity の時間が既に元に戻っているとき (Play の開始) にだけ使う。
        /// </summary>
        public void Reset()
        {
            _requests.Clear();
            _applied = false;
            _baseTimeScale = NormalScale;
            _baseFixedDeltaTime = 0f;
            IsPaused = false;
        }

        /// <summary>
        /// 1 フレーム進める。ポーズ中でなければ要求の秒数を realDeltaSeconds だけ減らし、期限が来たものを外して expired に足す。
        /// 書くべき速さと fixedDeltaTime が今の値と違えば true を返し、out にその値を入れる (違わなければ out は今の値のまま)。
        /// </summary>
        /// <param name="realDeltaSeconds">前のフレームからの実時間 (秒)。</param>
        /// <param name="paused">ポーズ中か。ポーズ中は何も書かない。</param>
        /// <param name="currentTimeScale">今の Time.timeScale。最初の要求を効かせるとき、戻す値として覚える。</param>
        /// <param name="currentFixedDeltaTime">今の Time.fixedDeltaTime。同上。</param>
        /// <param name="expired">期限が来た要求を足す先。要らなければ null。</param>
        /// <param name="timeScale">書く Time.timeScale。</param>
        /// <param name="fixedDeltaTime">書く Time.fixedDeltaTime。</param>
        /// <exception cref="ArgumentOutOfRangeException">realDeltaSeconds か currentTimeScale が負か有限でない。currentFixedDeltaTime が 0 以下か有限でない。</exception>
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

        /// <summary>例: <c>reason=debug scale=0.3 remaining=2.5</c>。</summary>
        public static string FormatRequest(TimeScaleRequest request)
        {
            return $"reason={request.Reason} scale={Number(request.Scale)} remaining={Number(request.RemainingSeconds)}";
        }

        /// <summary>例: <c>[TimeScale] accepted reason=debug scale=0.3 seconds=3 slowest=0.3</c>。</summary>
        public static string FormatAccepted(string reason, float scale, float seconds, float slowest)
        {
            return $"{LogTag} accepted reason={reason} scale={Number(scale)} seconds={Number(seconds)} slowest={Number(slowest)}";
        }

        /// <summary>例: <c>[TimeScale] refused reason=debug scale=0.3 seconds=3 because=slowMotionOff</c>。</summary>
        public static string FormatRefused(string reason, float scale, float seconds, TimeScaleRefusal refusal)
        {
            return $"{LogTag} refused reason={reason} scale={Number(scale)} seconds={Number(seconds)} because={RefusalName(refusal)}";
        }

        /// <summary>例: <c>[TimeScale] expired reason=debug</c>。</summary>
        public static string FormatExpired(TimeScaleRequest request)
        {
            return $"{LogTag} expired reason={request.Reason}";
        }

        /// <summary>例: <c>[TimeScale] cancelled reason=debug because=slowMotionOff</c>。</summary>
        public static string FormatCancelled(TimeScaleRequest request, string because)
        {
            return $"{LogTag} cancelled reason={request.Reason} because={because}";
        }

        /// <summary>例: <c>[TimeScale] set timeScale=0.3 fixedDeltaTime=0.006</c>。</summary>
        public static string FormatSet(float timeScale, float fixedDeltaTime)
        {
            return $"{LogTag} set timeScale={Number(timeScale)} fixedDeltaTime={Number(fixedDeltaTime)}";
        }

        /// <summary>例: <c>[TimeScale] restored timeScale=1 fixedDeltaTime=0.02</c>。</summary>
        public static string FormatRestored(float timeScale, float fixedDeltaTime)
        {
            return $"{LogTag} restored timeScale={Number(timeScale)} fixedDeltaTime={Number(fixedDeltaTime)}";
        }

        /// <summary>例: <c>[TimeScale] paused; holding 1 request(s)</c> / <c>[TimeScale] resumed; 1 request(s) remain</c>。</summary>
        public static string FormatPause(bool paused, int requestCount)
        {
            return paused
                ? $"{LogTag} paused; holding {requestCount} request(s)"
                : $"{LogTag} resumed; {requestCount} request(s) remain";
        }

        /// <summary>ログに出す断った理由の名前。</summary>
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
            return Math.Abs(a - b) > Tolerance;
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
