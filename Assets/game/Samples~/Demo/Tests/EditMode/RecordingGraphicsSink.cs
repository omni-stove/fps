using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    // AppliedGraphicsSettings はエディターの QualitySettings と fps を書き換えるので、テストでは入れた値を覚えるだけにする。
    internal sealed class RecordingGraphicsSink : IGraphicsSettingsSink
    {
        public RecordingGraphicsSink(GraphicsSettings current)
        {
            Current = current;
        }

        public GraphicsSettings Current { get; private set; }

        public List<GraphicsSettings> Applied { get; } = new List<GraphicsSettings>();

        public void Apply(GraphicsSettings settings)
        {
            Current = settings;
            Applied.Add(settings);
        }
    }
}
