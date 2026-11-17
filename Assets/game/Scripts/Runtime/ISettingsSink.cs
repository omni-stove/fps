using Shooter.Core;

namespace Shooter.Runtime
{
    public interface ISettingsSink
    {
        ControlSettings Current { get; }

        void Apply(ControlSettings settings);
    }
}
