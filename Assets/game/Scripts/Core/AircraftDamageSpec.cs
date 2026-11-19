namespace Shooter.Core
{
    // 値を持たない型を先に置くのは、壊れ方の値を足す Issue が AircraftDefinition とモーターの間の受け渡しを作り直さずに済むようにするため。
    public sealed class AircraftDamageSpec
    {
        public static AircraftDamageSpec Default => new AircraftDamageSpec();
    }
}
