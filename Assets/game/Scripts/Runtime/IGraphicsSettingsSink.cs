using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IGraphicsSettingsSink
    {
        GraphicsSettings Current { get; }

        void Apply(GraphicsSettings settings);
    }
}
