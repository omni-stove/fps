using System.Numerics;

namespace Shooter.Core
{
    public interface IWaterVolumes
    {
        float? SurfaceAt(Vector3 position);
    }

    public sealed class NoWaterVolumes : IWaterVolumes
    {
        public static readonly NoWaterVolumes Instance = new NoWaterVolumes();

        private NoWaterVolumes()
        {
        }

        public float? SurfaceAt(Vector3 position)
        {
            return null;
        }
    }
}
