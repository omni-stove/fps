using System.Numerics;

namespace Shooter.NetV2
{
    // 見た目の参照は武器の定義 (Unity のアセット) にしか無いので、Unity 抜きでビルドする ClientProjectiles は武器の Id だけを渡して作らせる。
    public interface IClientProjectileViews
    {
        IClientProjectileView Show(string weaponId, Vector3 position, Vector3 velocity);
    }
}
