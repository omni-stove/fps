namespace Shooter.Core
{
    public enum DeployRemoval
    {
        Destroyed,
        Replaced,

        // シーンごと消えたときなど、Remove を通らずに消えた物も持ち主の一覧から外せるよう、上の 2 つと分ける。
        Discarded,

        // 壊された (Destroyed) のと分けて、撃ち尽くしたのか時間で消えたのかをログで見分けられるようにする。
        Expended,
        Expired,

        // 地雷のように自分で爆発して消えたのを、壊された (Destroyed) のと分けてログで見分けられるようにする。
        Detonated,

        // ドローンを持ち主が戻したのと届く距離を越えて落ちたのを、壊された (Destroyed) のや電池切れ (Expired) と分けてログで見分けられるようにする。
        Recalled,
        OutOfRange,
    }
}
