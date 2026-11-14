namespace Fps.Core
{
    public enum MeleeRefusal
    {
        None,

        // ルールの Melee が Off。
        Off,

        // 振っている最中か、次に振れるまでの待ち時間。
        Cooldown,

        // 回避・乗り越え・ほかの横取りの移動・ダウン・死亡など、体が別のことをしている。
        Busy,
    }
}
