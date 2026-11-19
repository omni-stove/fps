using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "MovingPlatformSettings", menuName = "Shooter/Moving Platform Settings")]
    public sealed class MovingPlatformSettings : ScriptableObject
    {
        [SerializeField, Tooltip("PingPong は端で折り返して往復、Loop は最後の点から最初の点へ戻って回り続け、OnCall は呼ばれた点へ行くエレベーター")]
        private PlatformRouteKind _kind = MovingPlatformSpec.DefaultKind;

        [SerializeField, Min(0f), Tooltip("進む速さ (m/s)。0 なら動かない (回るだけの床に使う)")]
        private float _speed = MovingPlatformSpec.DefaultSpeed;

        [SerializeField, Min(0f), Tooltip("点に着いてから次へ向かうまでの秒数")]
        private float _waitSeconds = MovingPlatformSpec.DefaultWaitSeconds;

        [SerializeField, Tooltip("Y 軸まわりに回る速さ (度/秒)。正で上から見て時計回り。0 なら回らない")]
        private float _spinDegreesPerSecond = MovingPlatformSpec.DefaultSpinDegreesPerSecond;

        [SerializeField, Tooltip("OnCall で止まっているときにプレイヤーが乗ると、次の点へ向かう")]
        private bool _departWhenBoarded = MovingPlatformSpec.DefaultDepartWhenBoarded;

        public PlatformRouteKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        public float WaitSeconds
        {
            get => _waitSeconds;
            set => _waitSeconds = value;
        }

        public float SpinDegreesPerSecond
        {
            get => _spinDegreesPerSecond;
            set => _spinDegreesPerSecond = value;
        }

        public bool DepartWhenBoarded
        {
            get => _departWhenBoarded;
            set => _departWhenBoarded = value;
        }

        // 値が不正なら MovingPlatformSpec が ArgumentOutOfRangeException を投げる
        public MovingPlatformSpec ToSpec()
        {
            return new MovingPlatformSpec(_kind, _speed, _waitSeconds, _spinDegreesPerSecond, _departWhenBoarded);
        }

        public static MovingPlatformSpec Resolve(MovingPlatformSettings settings)
        {
            return settings != null ? settings.ToSpec() : MovingPlatformSpec.Default;
        }
    }
}
