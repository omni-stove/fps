using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class CosmeticLog
    {
        public const string LogTag = "[Cosmetic]";

        public static string Configured(CosmeticCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} configured skins={1} camos={2}",
                LogTag, catalog.OfKind(CosmeticKind.Skin).Count, catalog.OfKind(CosmeticKind.Camo).Count);
        }

        public static string Equipped(CosmeticKind kind, string cosmeticId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} equipped kind={1} id={2}", LogTag, kind, Id(cosmeticId));
        }

        public static string Refused(CosmeticKind kind, string cosmeticId, string reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} refused kind={1} id={2} reason={3}", LogTag, kind, Id(cosmeticId), string.IsNullOrEmpty(reason) ? "-" : reason);
        }

        public static string NoVisual(string cosmeticId, string visualId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} no visual id={1} visual={2}; the default look is kept", LogTag, Id(cosmeticId), Id(visualId));
        }

        public static string RemoteWorn(int playerId, string cosmeticId, string bodyName)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} remote player={1} id={2} body={3}", LogTag, playerId, Id(cosmeticId), string.IsNullOrEmpty(bodyName) ? "default" : bodyName);
        }

        private static string Id(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
