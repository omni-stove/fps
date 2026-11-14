using System;

namespace Fps.Protocol
{
    /// <summary>受け取ったデータがメッセージとして読めない (足りない・値が壊れている)。送り手を信じずに切る目安にする。</summary>
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
