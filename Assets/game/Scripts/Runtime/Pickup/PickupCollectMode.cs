namespace Shooter.Runtime
{
    public enum PickupCollectMode
    {
        Touch = 0,

        Interact = 1,

        // 落とした武器はこの拾い方にする。触れて拾うと、落とした人がその場で拾い直してしまうため。
        Look = 2,
    }
}
