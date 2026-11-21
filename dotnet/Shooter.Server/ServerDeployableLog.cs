using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

        // 置いた物の Id はクライアントが送った数なので、種類は置いた物が見つかったときだけ載せる
        public static string DroneControlRefused(int playerId, string knownTypeId, int deployableId, ServerDroneControlRefusal reason)
        {
            return DeployLog.Prefix(Owner(playerId), knownTypeId) + " net drone control refused serial=" + Text(deployableId) + " reason=" + reason;
        }

        public static string Abilities(IReadOnlyList<string> candidates, IReadOnlyList<string> defaults, bool fromRules)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(defaults);

            return DeployLog.Tag + " net abilities=" + Join(candidates) + " default=" + Join(defaults)
                + " from=" + (fromRules ? "rules" : "catalog");
        }

        // Id はサーバーのルールのファイルに書いたものなので、そのまま載せてよい
        public static string AbilityNotInCatalog(string typeId, string rulesName)
        {
            return DeployLog.Tag + " net kind=" + typeId + " is in loadout.abilities of rules " + rulesName
                + " but not in the deployable tuning; it is not offered";
        }

        public static string AbilitiesReset(int playerId)
        {
            return DeployLog.Prefix(Owner(playerId), null) + " net abilities reset to default: the rules no longer offer the chosen ones";
        }

        public static string Equipped(int playerId, IReadOnlyList<string> abilityIds)
        {
            ArgumentNullException.ThrowIfNull(abilityIds);

            return DeployLog.Prefix(Owner(playerId), null) + " net equipped abilities=" + Join(abilityIds);
        }

        // 戻らない回数 (rechargeSeconds が 0) を使い切ると待ちは無限なので、数の代わりに never と書く
        public static string NotReady(int playerId, string typeId, ServerDeployRefusal reason, int charges, int maxCharges, float waitSeconds)
        {
            var wait = float.IsFinite(waitSeconds) ? waitSeconds.ToString("0.00", CultureInfo.InvariantCulture) : "never";
            return Refused(playerId, typeId, reason) + " charges=" + Text(charges) + "/" + Text(maxCharges) + " wait=" + wait;
        }

        // サーバーの当たりには名前が無いので、貼った先はマップのコライダーの番号で書く
        public static string Surface(int colliderId)
        {
            return "collider:" + Text(colliderId);
        }

        public static string Body(int playerId)
        {
            return "player:" + Text(playerId);
        }

        // 乗り物はマップの乗り物の名前 (VehicleLog.NetName) で書き、乗り物のログの行と突き合わせられるようにする
        public static string Vehicle(string netName)
        {
            return "vehicle:" + netName;
        }

        public static string Released(int ownerId, string typeId, int serial, string surface, ServerAnchorLoss loss)
        {
            return RemoteChargeLog.Prefix(Owner(ownerId), typeId) + " serial=" + Text(serial) + " net released from=" + surface + " reason=" + loss;
        }

        public static string Owner(int playerId)
        {
            return Text(playerId);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 「なし」の枠は空の Id なので、行の区切りが崩れないよう - で書く
        private static string Join(IReadOnlyList<string> ids)
        {
            if (ids.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(string.IsNullOrEmpty(ids[i]) ? "-" : ids[i]);
            }

            return builder.ToString();
        }
    }
}
