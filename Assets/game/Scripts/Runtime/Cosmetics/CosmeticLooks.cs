using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 見た目の Id を絵に引く。Id がカタログに無い・種類が違う・絵が無いときは null を返し、使う側は既定の見た目のままにする。
    public static class CosmeticLooks
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        private static CosmeticVisualCatalog _visuals;
        private static bool _visualsResolved;

        public static CosmeticVisualCatalog Visuals
        {
            get
            {
                if (!_visualsResolved)
                {
                    _visuals = CosmeticVisualCatalog.Load();
                    _visualsResolved = true;
                }

                return _visuals;
            }
        }

        // 相手の見た目を引くときは、自分の解けているかに関わらずカタログだけを見る。記録係が無い画面 (専用サーバーの確認など) でも引けるよう、無ければ JSON から読む。
        public static CosmeticCatalog Catalog => GameSignals.Cosmetics.Value?.Catalog ?? CosmeticLocker.LoadCatalog();

        public static string WornId(CosmeticKind kind)
        {
            return GameSignals.Cosmetics.Value?.EquippedId(kind);
        }

        public static Material WornPaint()
        {
            var locker = GameSignals.Cosmetics.Value;
            return locker == null ? null : PaintOf(locker.Catalog, Visuals, locker.EquippedId(CosmeticKind.Camo));
        }

        public static GameObject BodyOf(string cosmeticId)
        {
            return BodyOf(Catalog, Visuals, cosmeticId);
        }

        public static GameObject BodyOf(CosmeticCatalog catalog, CosmeticVisualCatalog visuals, string cosmeticId)
        {
            var item = Find(catalog, cosmeticId, CosmeticKind.Skin);
            if (item == null)
            {
                return null;
            }

            var body = visuals != null ? visuals.FindBody(item.VisualId) : null;
            WarnIfMissing(body == null, item);
            return body;
        }

        public static Material PaintOf(CosmeticCatalog catalog, CosmeticVisualCatalog visuals, string cosmeticId)
        {
            var item = Find(catalog, cosmeticId, CosmeticKind.Camo);
            if (item == null)
            {
                return null;
            }

            var paint = visuals != null ? visuals.FindPaint(item.VisualId) : null;
            WarnIfMissing(paint == null, item);
            return paint;
        }

        private static CosmeticItem Find(CosmeticCatalog catalog, string cosmeticId, CosmeticKind kind)
        {
            if (catalog == null || string.IsNullOrEmpty(cosmeticId))
            {
                return null;
            }

            var item = catalog.Find(cosmeticId);
            return item != null && item.Kind == kind ? item : null;
        }

        private static void WarnIfMissing(bool missing, CosmeticItem item)
        {
            if (missing && _warned.Add(item.Id))
            {
                Debug.LogWarning(CosmeticLog.NoVisual(item.Id, item.VisualId));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlay()
        {
            _warned.Clear();
            _visuals = null;
            _visualsResolved = false;
        }
    }
}
