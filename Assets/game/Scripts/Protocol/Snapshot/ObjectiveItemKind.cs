namespace Shooter.Protocol
{
    // 値の並びをそのまま 1 バイトで送るので、足すときは後ろに足す。
    public enum ObjectiveItemKind
    {
        Flag,

        Bomb,

        Tag,

        Cart,

        LootBox,

        SupplyCrate,

        DeathBox,

        ExtractionPoint,
    }
}
