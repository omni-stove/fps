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

            if (vehicle.TryGetComponent<VehicleMotorLink>(out var link) && link.ControlScheme != null)
            {
                return link.ControlScheme;
            }

            return vehicle.TryGetComponent<IVehicleControlScheme>(out var scheme) ? scheme : CarControlScheme.Instance;
        }
    }
}
