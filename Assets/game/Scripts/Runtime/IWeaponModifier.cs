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
    }
}
