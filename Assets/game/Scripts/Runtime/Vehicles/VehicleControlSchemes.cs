using UnityEngine;

namespace Shooter.Runtime
{
    public static class VehicleControlSchemes
    {
        // 毎回引き直すのは、ゲームが当て方の部品を後から付け替えても、結び直さずに次の入力から効くようにするため。
        public static IVehicleControlScheme For(GameObject vehicle)
        {
            if (vehicle == null)
            {
                return CarControlScheme.Instance;
            }

            if (vehicle.TryGetComponent<VehicleMotorLink>(out var link))
            {
                var linked = link.ControlScheme;
                // interface への null 判定は破棄済みの MonoBehaviour を見逃すので、Unity の null で見る。
                if (linked is Object unityScheme ? unityScheme != null : linked != null)
                {
                    return linked;
                }
            }

            return vehicle.TryGetComponent<IVehicleControlScheme>(out var scheme) ? scheme : CarControlScheme.Instance;
        }
    }
}
