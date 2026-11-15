using System.Numerics;

namespace Fps.NetV2
{
    /// <summary><see cref="ClientProjectiles"/> が動かす、飛ぶ弾 1 本の見た目。</summary>
    public interface IClientProjectileView
    {
        void Move(Vector3 position, Vector3 velocity);

        /// <summary>刺さった所に残す。残しておく長さは見た目の側が決め、過ぎたら自分で消える。</summary>
        void Stick(Vector3 position);

        void Remove();
    }
}
