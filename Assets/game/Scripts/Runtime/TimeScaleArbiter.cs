using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TimeScaleArbiter : MonoBehaviour
    {
        public const string LogTag = TimeScaleArbitration.LogTag;

        public const string ObjectName = "TimeScaleArbiter";

        public const string RuleOwnerGoneReason = "ruleOwnerGone";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly TimeScaleArbitration _arbitration = new TimeScaleArbitration();
        private static readonly List<TimeScaleRequest> _expiredScratch = new List<TimeScaleRequest>();
        private static TimeScaleArbiter _instance;
        private static SlowMotion _rule = SlowMotion.Off;
        private static UnityEngine.Object _ruleOwner;
        private static Func<bool> _networkSessionSource;
        private static bool _wasPaused;

        public static SlowMotion Rule => _ruleOwner != null ? _rule : SlowMotion.Off;

        // Runtime はネットのアセンブリを参照できないので、ネット対戦中かはネットのアセンブリが起動時に登録する。
        public static Func<bool> NetworkSessionSource
        {
            get => _networkSessionSource;
            set => _networkSessionSource = value;
        }

        public static bool IsNetworkSession
        {
            get
            {
                if (NetStartup.IsDedicatedServerBuild)
                {
                    return true;
                }

                var source = _networkSessionSource;
                if (source == null)
                {
                    return false;
                }

                try
                {
                    return source();
                }
                catch (Exception e)
                {
                    // 分からないときに全員の時間を変えてしまわないよう、ネット対戦中として断る側に倒す。
                    Debug.LogError($"{LogTag} NetworkSessionSource threw; treating this as a network session: {e.Message}");
                    Debug.LogException(e);
                    return true;
                }
            }
        }

        public static float Scale => _arbitration.Scale;

        public static IReadOnlyList<TimeScaleRequest> Requests => _arbitration.Requests;

        public static TimeScaleArbiter Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ResetState();
            _networkSessionSource = null;
        }

        public static void ApplyRules(SlowMotion rule, UnityEngine.Object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (rule != SlowMotion.Off && rule != SlowMotion.On)
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "unknown slow motion rule");
            }

            var changed = Rule != rule;
            _rule = rule;
            _ruleOwner = owner;
            if (changed)
            {
                Debug.Log($"{LogTag} rule slowMotion={rule} owner='{owner.name}'", owner);
            }

            if (rule != SlowMotion.On)
            {
                CancelAll(TimeScaleArbitration.RefusalName(TimeScaleRefusal.RuleOff));
            }
        }

        public static TimeScaleRefusal Request(string reason, float scale, float seconds)
        {
            TimeScaleArbitration.Validate(reason, scale, seconds);

            var refusal = TimeScaleArbitration.Check(Rule, IsNetworkSession);
            if (refusal != TimeScaleRefusal.None)
            {
                Debug.LogWarning(TimeScaleArbitration.FormatRefused(reason, scale, seconds, refusal));
                return refusal;
            }

            _arbitration.Request(reason, scale, seconds);
            Debug.Log(TimeScaleArbitration.FormatAccepted(reason, scale, seconds, _arbitration.Scale));
            EnsureInstance();
            return TimeScaleRefusal.None;
        }

        public static bool Cancel(string reason)
        {
            var requests = _arbitration.Requests;
            if (!_arbitration.Cancel(reason))
            {
                return false;
            }

            foreach (var request in requests)
            {
                if (request.Reason == reason)
                {
                    Debug.Log(TimeScaleArbitration.FormatCancelled(request, "cancelled"));
                }
            }

            return true;
        }

        public static void CancelAll(string because)
        {
            foreach (var request in _arbitration.CancelAll())
            {
                Debug.Log(TimeScaleArbitration.FormatCancelled(request, because));
            }
        }

        internal static void Tick(float realDeltaSeconds, bool paused)
        {
            if (_arbitration.HasRequests)
            {
                if (IsNetworkSession)
                {
                    CancelAll(TimeScaleArbitration.RefusalName(TimeScaleRefusal.NetworkSession));
                }
                else if (Rule != SlowMotion.On)
                {
                    CancelAll(_ruleOwner == null ? RuleOwnerGoneReason : TimeScaleArbitration.RefusalName(TimeScaleRefusal.RuleOff));
                }
            }

            if (paused != _wasPaused)
            {
                _wasPaused = paused;
                if (_arbitration.HasRequests || _arbitration.IsApplied)
                {
                    Debug.Log(TimeScaleArbitration.FormatPause(paused, _arbitration.Requests.Count));
                }
            }

            _expiredScratch.Clear();
            var wasApplied = _arbitration.IsApplied;
            var write = _arbitration.Step(
                Math.Max(0f, realDeltaSeconds), paused, Time.timeScale, Time.fixedDeltaTime, _expiredScratch,
                out var timeScale, out var fixedDeltaTime);

            foreach (var request in _expiredScratch)
            {
                Debug.Log(TimeScaleArbitration.FormatExpired(request));
            }

            _expiredScratch.Clear();
            if (write)
            {
                Time.timeScale = timeScale;
                Time.fixedDeltaTime = fixedDeltaTime;
            }

            if (wasApplied && !_arbitration.IsApplied)
            {
                Debug.Log(TimeScaleArbitration.FormatRestored(Time.timeScale, Time.fixedDeltaTime));
            }
            else if (write)
            {
                Debug.Log(TimeScaleArbitration.FormatSet(timeScale, fixedDeltaTime));
            }
        }

        internal static void ResetState()
        {
            _arbitration.Reset();
            _expiredScratch.Clear();
            _instance = null;
            _rule = SlowMotion.Off;
            _ruleOwner = null;
            _wasPaused = false;
        }

        private static void EnsureInstance()
        {
            if (_instance != null || !Application.isPlaying)
            {
                return;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<TimeScaleArbiter>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{LogTag} only one TimeScaleArbiter stays resident; destroying the duplicate '{name}'", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime, GameUi.IsPauseOpen);
        }
    }
}
