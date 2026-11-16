using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーが持っている物から、移動の速さに掛ける倍率を出す口。<see cref="PlayerController.CarryLoad"/> に渡す
    /// (オフラインは <see cref="WeaponController"/>)。
    /// </summary>
    public interface IPlayerCarryLoad
    {
        /// <summary>carry の決め方で、今持っている物から出した倍率 (<see cref="CarryLoadSpec.SpeedMultiplier"/>)。</summary>
        float CarrySpeedMultiplier(CarryLoadSpec carry);
    }
}
