using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    // AppliedControlSettings に入れるとほかのテストと本物の設定に残るので、入れた値をここに覚えるだけにする。
    public sealed class RecordingSettingsSink : ISettingsSink
    {
        private readonly List<ControlSettings> _applied = new List<ControlSettings>();

        public RecordingSettingsSink(ControlSettings current)
        {
            Current = current ?? throw new ArgumentNullException(nameof(current));
        }

        public ControlSettings Current { get; private set; }

        public IReadOnlyList<ControlSettings> Applied => _applied;

        public void Apply(ControlSettings settings)
        {
            Current = settings ?? throw new ArgumentNullException(nameof(settings));
            _applied.Add(settings);
        }
    }
}
