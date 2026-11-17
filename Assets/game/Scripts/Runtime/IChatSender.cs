using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IChatSender
    {
        void Send(ChatChannel channel, string text);
    }
}
