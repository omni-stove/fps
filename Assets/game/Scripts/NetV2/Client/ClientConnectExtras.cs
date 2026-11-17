using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // ConnectionExtras は欄に合わない値を例外で拒むので、手元の値が 1 つ合わないだけで試合に入れなくならないよう、その欄だけ落としてつなぐ。
    public static class ClientConnectExtras
    {
        public static ConnectionExtras Compose(string partyId, string joinCode, int? skillRating, string productUserId, Action<string> log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return new ConnectionExtras(
                Field(ConnectLog.PartyIdField, partyId, ConnectionExtras.MaxPartyIdLength, log),
                Field(ConnectLog.JoinCodeField, joinCode, ConnectionExtras.MaxJoinCodeLength, log),
                skillRating,
                Field(ConnectLog.ProductUserIdField, productUserId, ConnectionExtras.MaxProductUserIdLength, log));
        }

        private static string Field(string name, string value, int maxLength, Action<string> log)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (ConnectionExtras.IsValidField(value, maxLength))
            {
                return value;
            }

            log(ConnectLog.FieldDropped(name, value.Length, maxLength));
            return null;
        }
    }
}
