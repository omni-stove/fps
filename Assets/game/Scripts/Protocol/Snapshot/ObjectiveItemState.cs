namespace Shooter.Protocol
{
    // 物ごとに Core の段階 (FlagState・BombState) を分けて送らないのは、置き場所を描くのに要る違いがこれだけだから。
    // 爆弾が解除・爆発したかは MatchSnapshot.Bomb で送る。
    public enum ObjectiveItemState
    {
        Home,

        Carried,

        Dropped,

        Planted,
    }
}
