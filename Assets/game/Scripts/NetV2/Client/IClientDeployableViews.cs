using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 見た目の参照は置く物の定義 (Unity のアセット) にしか無いので、Unity 抜きでビルドする ClientDeployables は種類の Id だけを渡して作らせる。
    public interface IClientDeployableViews
    {
        IClientDeployableView Show(DeployableKind kind, string typeId, Team team, bool isOwn, Vector3 position, float yaw);
    }
}
