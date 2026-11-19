using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 品の Id は送った Id のまま持つ。一覧に無い Id への答え (UnknownItem) でも、受けた側が待っている品と照らせるようにするため。
    public readonly struct PurchaseAnswer
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _itemId;

        public string ItemId => _itemId ?? string.Empty;

        public PurchaseVerdict Verdict { get; }

        public bool Bought => Verdict == PurchaseVerdict.Bought;

        public PurchaseAnswer(string itemId, PurchaseVerdict verdict)
        {
            if (!Enum.IsDefined(typeof(PurchaseVerdict), verdict))
            {
                throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "verdict is not a valid PurchaseVerdict");
            }

            _itemId = itemId ?? string.Empty;
            Verdict = verdict;
        }
    }
}
