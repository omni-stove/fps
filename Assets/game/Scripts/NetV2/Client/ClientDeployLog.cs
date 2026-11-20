using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ClientDeployLog
    {
        public static string Requested(string typeId, Vector3 position, float yaw, bool sent)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net request kind={1} pos=({2:0.00}, {3:0.00}, {4:0.00}) yaw={5:0.0} sent={6}",
                DeployLog.Tag,
                Name(typeId),
                position.X,
                position.Y,
                position.Z,
                yaw,
                sent);
        }

        public static string NotSent(string typeId, DeployVerdict verdict)
        {
            return $"{DeployLog.Tag} net request kind={Name(typeId)} not sent verdict={verdict}";
        }

        public static string UnknownType(string typeId)
        {
            return $"{DeployLog.Tag} net request kind={Name(typeId)} not sent: no deploy definition has this id";
        }

        public static string Shown(int deployableId, string typeId, bool isOwn)
        {
            return $"{DeployLog.Tag} net shown id={deployableId} kind={Name(typeId)} own={isOwn}";
        }

        // 差し替えの見た目が無くて箱で出したかを見分けられるようにする
        public static string Look(string typeId, string kind, bool fromPrefab)
        {
            return $"{DeployLog.Tag} net look kind={Name(typeId)} role={kind} prefab={fromPrefab}";
        }

        public static string Gone(int deployableId, string typeId)
        {
            return $"{DeployLog.Tag} net gone id={deployableId} kind={Name(typeId)}";
        }

        // 枠の順に、その枠で置く種類を並べる。置く物でない枠と「なし」の枠は -
        public static string Slots(IReadOnlyList<string> typeIds)
        {
            if (typeIds == null)
            {
                throw new ArgumentNullException(nameof(typeIds));
            }

            var names = new string[typeIds.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = Name(typeIds[i]);
            }

            return $"{DeployLog.Tag} net slots kinds={(names.Length > 0 ? string.Join(",", names) : "-")}";
        }

        public static string SlotsNotResolved(string reason)
        {
            return $"{DeployLog.Tag} net slots emptied: the accepted abilities cannot be looked up in the ability catalog: {(string.IsNullOrEmpty(reason) ? "-" : reason)}";
        }

        private static string Name(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
