using System.Numerics;

namespace Shooter.Server
{
    // 動こうとしているのに進めていないかを、決めた秒数ごとの移動の量で見る
    internal sealed partial class ServerBotAgent
    {
        private Vector3 _progressPosition;
        private float _progressTime;
        private bool _tracking;

        // はしごの上では上下に進むので、水平ではなく 3 次元の距離で測る
        public bool IsStuck(Vector3 position, bool moving)
        {
            if (!moving || !_tracking)
            {
                _tracking = moving;
                _progressPosition = position;
                _progressTime = Clock;
                return false;
            }

            if (Clock - _progressTime < _spec.StuckSeconds)
            {
                return false;
            }

            var moved = Vector3.Distance(position, _progressPosition);
            _progressPosition = position;
            _progressTime = Clock;
            return moved < _spec.StuckProgress;
        }
    }
}
