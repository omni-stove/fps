using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IPartyControl
    {
        event Action<PartyStatus> StatusChanged;

        PartyStatus Status { get; }

        void Create();

        void Join(string inviteCode);

        void Leave();
    }
}
