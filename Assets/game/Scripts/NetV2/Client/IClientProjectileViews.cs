using System.Numerics;

namespace Fps.NetV2
{
    /// <summary>
    /// 飛ぶ弾の見た目を作る口。見た目の参照は武器の定義 (Unity のアセット) にしか無いので、Unity 抜きでビルドする
    /// <see cref="ClientProjectiles"/> は武器の Id だけを渡して作らせる。
    /// </summary>
    public interface IClientProjectileViews
    {
        IClientProjectileView Show(string weaponId, Vector3 position, Vector3 velocity);
    }
}
