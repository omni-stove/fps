using System.Numerics;

namespace Fps.NetV2
{
    /// <summary>他のプレイヤーの、ある tick の見た目の姿。</summary>
    public readonly struct RemotePose
    {
        public RemotePose(Vector3 position, float yaw, bool isCrouched)
        {
            Position = position;
            Yaw = yaw;
            IsCrouched = isCrouched;
        }

        /// <summary>本体 (ルートの Transform) の位置。サーバーの移動の <c>CharacterMotorState.Position</c> と同じもの。</summary>
        public Vector3 Position { get; }

        /// <summary>向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; }

        public bool IsCrouched { get; }
    }
}
