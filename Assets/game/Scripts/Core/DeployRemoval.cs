namespace Fps.Core
{
    public enum DeployRemoval
    {
        Destroyed,
        Replaced,

        // シーンごと消えたときなど、Remove を通らずに消えた物も持ち主の一覧から外せるよう、上の 2 つと分ける。
        Discarded,
    }
}
