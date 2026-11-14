using System;
using System.Numerics;

namespace Fps.Core
{
    // 知覚の差し込みと振る舞いを 1 つにまとめるのは、判断の前にためたゲージと最後に見た場所を、判断の後の置き換えで同じ tick のうちに使うため。
    public sealed class StealthAwareness : IPerceptionContributor, IEnemyBehavior
    {
        private Stealth _rule;
        private Vector3 _forward = Vector3.UnitZ;
        private AwarenessPosture _posture;
        private float _light = 1f;
        private float _speed;
        private AwarenessStage _offStage;
        private AwarenessStage _published;

        public StealthAwareness(AwarenessSpec spec)
        {
            Meter = new AwarenessMeter(spec ?? throw new ArgumentNullException(nameof(spec)));
        }

        public event Action<AwarenessStage, AwarenessStage> StageChanged;

        public AwarenessMeter Meter { get; }

        public AwarenessSpec Spec => Meter.Spec;

        public Stealth Rule
        {
            get => _rule;
            set
            {
                if (!Enum.IsDefined(typeof(Stealth), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown stealth rule");
                }

                _rule = value;
            }
        }

        // Off のときはゲージを使わず、今までどおり判断が戦闘に入ったかで交戦を決める。ログと頭上の印は On と同じ口で出せるように。
        public AwarenessStage Stage => _rule == Stealth.On ? Meter.Stage : _offStage;

        public float Level => _rule == Stealth.On ? Meter.Level : (_offStage == AwarenessStage.Engaged ? AwarenessMeter.Full : 0f);

        public Vector3? LastSeen { get; private set; }

        // 仲間から伝わった交戦をさらに伝えると、伝える範囲が鎖のようにつながって離れた敵まで一斉に寄ってくる。
        public bool RaisedByAlarm { get; private set; }

        // 視線と距離は知覚から求めるので、ここでは知覚に無い材料だけを受け取る。
        public void Observe(Vector3 forward, AwarenessPosture posture, float lightFactor, float targetSpeed)
        {
            if (!Enum.IsDefined(typeof(AwarenessPosture), posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture), posture, "unknown posture");
            }

            if (!(lightFactor >= 0f) || lightFactor > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lightFactor), lightFactor, "lightFactor must be between 0 and 1");
            }

            if (!(targetSpeed >= 0f) || float.IsInfinity(targetSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(targetSpeed), targetSpeed, "targetSpeed must not be negative and must be finite");
            }

            // 真上や真下を向いた正面は水平の向きを持たないので、前の向きを使い続ける。
            var flat = new Vector3(forward.X, 0f, forward.Z);
            if (IsFinite(flat) && flat.LengthSquared() > 1e-8f)
            {
                _forward = Vector3.Normalize(flat);
            }

            _posture = posture;
            _light = lightFactor;
            _speed = targetSpeed;
        }

        public bool Alarm(Vector3 position)
        {
            if (_rule != Stealth.On || Stage == AwarenessStage.Engaged)
            {
                return false;
            }

            LastSeen = position;
            RaisedByAlarm = true;
            Meter.Raise(AwarenessMeter.Full);
            Publish();
            return true;
        }

        public void Reset()
        {
            Meter.Reset();
            LastSeen = null;
            RaisedByAlarm = false;
            _offStage = AwarenessStage.Unaware;
            Publish();
        }

        public AwarenessSighting SightingOf(EnemyPerception perception)
        {
            if (!perception.Target.HasValue)
            {
                return AwarenessSighting.None;
            }

            var self = perception.Self;
            var target = perception.Target.Value;
            var distance = Vector3.Distance(self, target);
            if (!IsFinite(distance))
            {
                return AwarenessSighting.None;
            }

            var toTarget = new Vector3(target.X - self.X, 0f, target.Z - self.Z);
            var angle = 0f;
            if (toTarget.LengthSquared() > 1e-8f)
            {
                var cos = Vector3.Dot(_forward, Vector3.Normalize(toTarget));
                angle = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * 180.0 / Math.PI);
            }

            return new AwarenessSighting(perception.HasLineOfSight, distance, angle, _posture, _light, _speed);
        }

        public EnemyPerception Contribute(EnemyBrain brain, float deltaSeconds, EnemyPerception perception)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            if (_rule != Stealth.On)
            {
                return perception;
            }

            Meter.Tick(deltaSeconds, brain.Spec.SightRange, SightingOf(perception));
            if (Meter.IsSeeing)
            {
                LastSeen = perception.Target;
                RaisedByAlarm = false;
            }

            if (perception.WasHit)
            {
                var source = perception.HitSource ?? perception.Target;
                if (source.HasValue)
                {
                    LastSeen = source;
                }

                if (Stage != AwarenessStage.Engaged)
                {
                    RaisedByAlarm = false;
                }

                Meter.Raise(AwarenessMeter.Full);
            }

            Publish();

            // 交戦の手前では量が 1 未満 (満ちた時点で交戦になる) なので、EnemyBrain は見えていても追わない。
            var awareness = Stage == AwarenessStage.Engaged ? AwarenessMeter.Full : Meter.Level;
            return new EnemyPerception(
                perception.Self,
                perception.Target,
                perception.HasLineOfSight,
                perception.HeardNoise,
                perception.NoisePosition,
                perception.WasHit,
                perception.HitSource,
                perception.HealthFraction,
                awareness,
                perception.IsSuppressed);
        }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            decision = planned;
            var fighting = IsFighting(brain.State);
            if (_rule != Stealth.On)
            {
                _offStage = fighting ? AwarenessStage.Engaged : AwarenessStage.Unaware;
                Publish();
                return false;
            }

            // 体力が減って隠れに行くなど、ゲージを通らずに戦闘へ入ることもあるので、段階を判断にそろえる。
            if (fighting)
            {
                if (Stage != AwarenessStage.Engaged)
                {
                    RaisedByAlarm = false;
                    Meter.Raise(AwarenessMeter.Full);
                    Publish();
                }

                return false;
            }

            if (!LastSeen.HasValue)
            {
                return false;
            }

            var spot = LastSeen.Value;
            switch (Stage)
            {
                case AwarenessStage.Noticed:
                    decision = new EnemyDecision(null, spot, false);
                    return true;
                case AwarenessStage.Alerted:
                    decision = Vector3.Distance(perception.Self, spot) > EnemyBrain.ArrivalDistance
                        ? new EnemyDecision(spot, spot, false)
                        : new EnemyDecision(null, null, false);
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFighting(EnemyState state)
        {
            return state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.TakeCover;
        }

        private void Publish()
        {
            var stage = Stage;
            if (stage == _published)
            {
                return;
            }

            var previous = _published;
            _published = stage;
            StageChanged?.Invoke(previous, stage);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }
    }
}
