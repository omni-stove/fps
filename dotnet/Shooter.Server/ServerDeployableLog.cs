using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 置いた・消えた・撃たれた・撃ったはオフラインと同じ行 (DeployLog・TurretLog・DecoyLog) を出す。ここはサーバーにしか無い行だけ。
    internal static class ServerDeployableLog
    {
        public static string Catalog(int types, int usable, string path)
        {
            return DeployLog.Tag + " net types=" + Text(types) + " usable=" + Text(usable) + " from=" + (path ?? "default");
        }

        public static string Unusable(string typeId, string reason)
        {
            return DeployLog.Tag + " net kind=" + typeId + " unusable: " + reason;
        }

        // 知らない種類の Id はクライアントが送った文字なので、ログの行を崩さないよう載せない。
        public static string Refused(int playerId, string knownTypeId, ServerDeployRefusal reason)
        {
            return DeployLog.Prefix(Owner(playerId), knownTypeId) + " net refused reason=" + reason;
        }

        // 戻らない回数 (rechargeSeconds が 0) を使い切ると待ちは無限なので、数の代わりに never と書く
        public static string NotReady(int playerId, string typeId, ServerDeployRefusal reason, int charges, int maxCharges, float waitSeconds)
        {
            var wait = float.IsFinite(waitSeconds) ? waitSeconds.ToString("0.00", CultureInfo.InvariantCulture) : "never";
            return Refused(playerId, typeId, reason) + " charges=" + Text(charges) + "/" + Text(maxCharges) + " wait=" + wait;
        }

        public static string Owner(int playerId)
        {
            return Text(playerId);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
