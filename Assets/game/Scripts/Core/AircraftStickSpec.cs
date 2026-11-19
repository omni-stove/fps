namespace Shooter.Core
{
    // 値を持たない型を先に置くのは、操縦桿の値を足す Issue が AircraftDefinition とモーターの間の受け渡しを作り直さずに済むようにするため。
    public sealed class AircraftStickSpec
    {
        public static AircraftStickSpec Default => new AircraftStickSpec();
    }
}
