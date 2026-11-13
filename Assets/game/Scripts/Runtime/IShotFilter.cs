using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="WeaponController"/> が決めた射撃の始点と向きを、Raycast の前に書き換える (<see cref="WeaponController.ShotFilter"/>)。
    /// カバーから身を乗り出して撃つ、隠れたまま散らして撃つなど、アクションが射撃を変えるのに使う。
    /// </summary>
    public interface IShotFilter
    {
        /// <param name="origin">射撃の始点 (ワールド座標)。</param>
        /// <param name="direction">射撃の向き (ワールド座標)。書き換えるなら単位ベクトルにする。</param>
        void Filter(ref Vector3 origin, ref Vector3 direction);
    }
}
