using System;

namespace Shooter.Protocol
{
    public sealed class MessageReadException : Exception
    {
        public MessageReadException()
        {
        }

        public MessageReadException(string message)
            : base(message)
        {
        }

        public MessageReadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
