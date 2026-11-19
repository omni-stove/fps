namespace Shooter.Core
{
    public enum BailOutVerdict
    {
        BailOut,

        TooLow,

        // 下に地面が見つからない。降りても着く所が無いので、脱出せず普通に降ろす。
        NoGround,

        Off,
    }
}
