namespace Shooter.Protocol
{
    // 種類ごとの段階 (ChargerPhase・BomberPhase・SniperPhase・盾の上げ下げ) を、見せ方の違う 4 つにまとめて送る。
    // 段階そのものを送らないのは、種類ごとに数の違う段階を 1 バイトで読めるようにし、クライアントが種類の判断を持たずに済むようにするため
    public enum BotArchetypeCue : byte
    {
        // 何も見せない (Rifleman・構える前・盾を構えている間)
        None = 0,

        // 避けるための予兆 (突進の構え・自爆の合図・狙撃の照準)
        Warning = 1,

        // 攻撃している最中 (突進中・狙撃の発砲)
        Attacking = 2,

        // 隙 (突進の後のよろめき・盾を下ろしている間)
        Exposed = 3,
    }
}
