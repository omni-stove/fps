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

        public static string Owner(int playerId)
        {
            return Text(playerId);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
