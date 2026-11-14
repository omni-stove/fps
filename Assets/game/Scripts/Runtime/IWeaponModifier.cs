using Fps.Core;

namespace Fps.Runtime
{
    // 散らばりと照準の広がりを別々に変えさせるのは、HUD の照準は撃った後の広がり (SpreadFraction) で、弾の円錐とは別の値だから。
    // 範囲に収めるのは WeaponController の側で行うので、ここでは足し引きだけでよい。
    public interface IWeaponModifier
    {
        float ModifyConeDegrees(WeaponSpec spec, float coneDegrees);

        float ModifySpreadFraction(float spreadFraction);
    }
}
