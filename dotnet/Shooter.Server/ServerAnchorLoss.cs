namespace Shooter.Server
{
    // 人や乗り物に貼った物が、付いていく先を失った理由。失った物はその場に残る (オフラインの RemoteCharge も付いていく先が消えるとその場に残る)
    internal enum ServerAnchorLoss
    {
        // 貼られた人が倒された・出現し直した。サーバーには倒れた体が残らないので、倒れた所に残す
        Died,

        // 貼られた人が試合から抜けた
        Left,

        // 貼られた人の体や乗り物がもう無い (マップの乗り物の並びが替わったなど)
        Gone,
    }
}
