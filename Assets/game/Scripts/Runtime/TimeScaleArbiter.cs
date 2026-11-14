using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 時間の速さ (スローモーション) の口。<see cref="Request"/> で理由・倍率・秒数を受け、一番遅いものを Time.timeScale にし、
    /// Time.fixedDeltaTime も同じ倍率にする。期限が来たら元へ戻す。どの要求をまとめてどの値を書くかは Core の <see cref="TimeScaleArbitration"/> が決める。
    /// <para>
    /// 受けるのはルールの <see cref="SlowMotion"/> が On のオフラインだけ。ルールは <see cref="SlowMotionActionInstaller"/> が
    /// プレイヤーを持ち主にして <see cref="ApplyRules"/> で入れ、持ち主が消える (シーンを離れる) と Off に戻る。
    /// ネット対戦中か (<see cref="NetworkSessionSource"/>) はネットのアセンブリが登録する。断ったときはその理由をログに出す。
    /// </para>
    /// <para>
    /// ポーズ中 (<see cref="GameUi.IsPauseOpen"/>) は秒数を数えず速さにも触らず、閉じた後に今の要求の速さを書き直す。
    /// どの場面で要求するかは使う側に任せる (組み込みではデバッグパネルのボタンだけが要求する)。
    /// </para>
    /// 要求を受けると、Play 中は秒数を数える GameObject をシーンをまたいで 1 つ常駐させる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimeScaleArbiter : MonoBehaviour
    {
        public const string LogTag = TimeScaleArbitration.LogTag;

        /// <summary>常駐させる GameObject の名前。</summary>
        public const string ObjectName = "TimeScaleArbiter";

        /// <summary>ルールの持ち主が消えて要求を取り下げたときの理由。</summary>
        public const string RuleOwnerGoneReason = "ruleOwnerGone";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly TimeScaleArbitration _arbitration = new TimeScaleArbitration();
        private static readonly List<TimeScaleRequest> _expiredScratch = new List<TimeScaleRequest>();
        private static TimeScaleArbiter _instance;
        private static SlowMotion _rule = SlowMotion.Off;
        private static UnityEngine.Object _ruleOwner;
        private static Func<bool> _networkSessionSource;
        private static bool _wasPaused;

        /// <summary>
        /// 今のルール。<see cref="ApplyRules"/> で入れた持ち主が消えていれば Off。
        /// </summary>
        public static SlowMotion Rule => _ruleOwner != null ? _rule : SlowMotion.Off;

        /// <summary>
        /// ネット対戦中かを返す処理。ネットのアセンブリが起動時に登録する。null なら専用サーバーのビルドだけをネット対戦とみなす。
        /// </summary>
        public static Func<bool> NetworkSessionSource
        {
            get => _networkSessionSource;
            set => _networkSessionSource = value;
        }

        /// <summary>ネット対戦中か。専用サーバーのビルドか、<see cref="NetworkSessionSource"/> が true を返す。</summary>
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

        /// <summary>受けている要求のうち一番遅い倍率。要求が無ければ 1。ポーズ中も要求の倍率を返す。</summary>
        public static float Scale => _arbitration.Scale;

        /// <summary>受けている要求 (受けた順) の写し。</summary>
        public static IReadOnlyList<TimeScaleRequest> Requests => _arbitration.Requests;

        /// <summary>常駐している秒数の数え役。まだ要求を受けていなければ null。</summary>
        public static TimeScaleArbiter Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ResetState();
            _networkSessionSource = null;
        }

        /// <summary>
        /// ルールを入れる。owner (プレイヤーなど) が消えると Off に戻る。Off にしたときに受けている要求があれば取り下げてログに出す
        /// (速さは次のフレームで戻す)。
        /// </summary>
        /// <exception cref="ArgumentNullException">owner が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">rule が知らない値。</exception>
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

        /// <summary>
        /// 時間の速さを scale にする要求を、実時間で seconds 秒だけ出す。同じ reason で出し直すと倍率と秒数を置き換える。
        /// ルールが Off かネット対戦中なら断ってログに出し、その理由を返す。受けたら <see cref="TimeScaleRefusal.None"/> で、
        /// 速さは次のフレームから変わる (ポーズ中なら閉じた後から)。
        /// </summary>
        /// <exception cref="ArgumentException">reason が null か空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">scale が 0 以下か 1 より大きい、seconds が 0 以下か有限でない。</exception>
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

        /// <summary>reason の要求を取り下げる。あれば true で、速さは次のフレームで書き直す。</summary>
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

        /// <summary>要求をすべて取り下げ、because を添えてログに出す。速さは次のフレームで戻す。</summary>
        public static void CancelAll(string because)
        {
            foreach (var request in _arbitration.CancelAll())
            {
                Debug.Log(TimeScaleArbitration.FormatCancelled(request, because));
            }
        }

        /// <summary>
        /// 1 フレーム分進め、書くべき値を Time.timeScale と Time.fixedDeltaTime に書く。Play 中は常駐の Update が呼ぶ。
        /// ルールの持ち主が消えたか、ネット対戦が始まっていれば、先に要求をすべて取り下げる。
        /// </summary>
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

        /// <summary>要求・ルール・覚えた値を Play の開始時の状態に戻す。Unity の時間には触らない。</summary>
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
