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

        // Guardian のリーダー。持ち主がリーダーで、立っている間は本人が運び (Carried)、倒れたら倒れた場所に落ちる (Dropped)
        Leader,
    }
}
