using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IReplayLibrary
    {
        event Action Changed;

        IReadOnlyList<ReplayEntry> Entries { get; }

        bool Play(string replayId);

        bool Delete(string replayId);
    }
}
