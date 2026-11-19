using System;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class ClientVoteKick
    {
        public event Action<ClientVoteKickState> StateReceived;

        public bool Handle(IMessage message)
        {
            if (!(message is VoteKickStateMessage state))
            {
                return false;
            }

            Receive(state);
            return true;
        }

        public void Receive(VoteKickStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            StateReceived?.Invoke(ClientVoteKickState.From(message));
        }
    }
}
