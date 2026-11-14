using Fps.Core;

namespace Fps.Runtime
{
    // FireBlocker (Func) と違い複数を並べて登録できるので、近接・伏せ・制圧などがそれぞれ 1 つずつ足しても互いに上書きしない。
    public interface IWeaponModifier
    {
        bool BlocksFire { get; }

        // true の間に始まったリロードは、WeaponController が次の Tick で取り消す。新しく始めることもできない。
        bool BlocksReload { get; }

        // 照準の広がり (CurrentConeDegrees) に掛ける倍率。0 以上の有限値。変えないなら 1。
        float SpreadMultiplier { get; }

        // 倍率とは別に足し引きも受けるのは、腰撃ちでも ADS でも同じ度数だけ広げたいもの (制圧) があるから。
        // 散らばりと照準の広がりを別々に変えさせるのは、HUD の照準は撃った後の広がり (SpreadFraction) で、弾の円錐とは別の値だから。
        // 範囲に収めるのは WeaponController の側で行うので、ここでは足し引きだけでよい。
        float ModifyConeDegrees(WeaponSpec spec, float coneDegrees);

        float ModifySpreadFraction(float spreadFraction);
    }
}
