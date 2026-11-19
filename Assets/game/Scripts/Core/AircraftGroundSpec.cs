namespace Shooter.Core
{
    // 値を持たない型を先に置くのは、地上の走りの値を足す Issue が AircraftDefinition とモーターの間の受け渡しを作り直さずに済むようにするため。
    public sealed class AircraftGroundSpec
    {
        public static AircraftGroundSpec Default => new AircraftGroundSpec();
    }
}
