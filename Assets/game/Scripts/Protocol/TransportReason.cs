using System;
using System.Globalization;
using System.Text;

namespace Fps.Protocol
{
    /// <summary>
    /// 切断の理由 (<see cref="TransportEvent.Reason"/>) を通信路に載せるバイト列との間で変える。どの <see cref="IMatchTransport"/> の実装も
    /// 同じ形で載せ、相手の実装が違っても読めるようにする。
    /// </summary>
    public static class TransportReason
    {
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return Array.Empty<byte>();
            }

            byte[] bytes;
            try
            {
                bytes = _utf8.GetBytes(reason);
            }
            catch (EncoderFallbackException e)
            {
                throw new ArgumentException("disconnect reason is not valid UTF-16", nameof(reason), e);
            }

            if (bytes.Length > TransportEvent.MaxReasonBytes)
            {
                throw new ArgumentException(
                    "disconnect reason is " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes in UTF-8; at most "
                    + TransportEvent.MaxReasonBytes.ToString(CultureInfo.InvariantCulture) + " can be sent",
                    nameof(reason));
            }

            return bytes;
        }

        /// <summary>
        /// 相手が自由に送れるので投げずに、長すぎる・UTF-8 として正しくないものは知らせが無かったもの (空文字) とする。
        /// </summary>
        public static string Decode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty || bytes.Length > TransportEvent.MaxReasonBytes)
            {
                return "";
            }

            try
            {
                return _utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return "";
            }
        }
    }
}
