using System;
using System.Globalization;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットの持ち主の欄 (OwnerSnapshot.Money) から、オフラインの RoundEconomy と同じ形の財布の状態 (WalletStatus) を作る。
    // スナップショットには財布の上限も経済の有無も載らないので、上限は作るときに受け取り (クライアントの調整値)、
    // 経済があるかは爆弾設置の試合でお金が一度でも届いたかで決める (経済の無いサーバーはずっと 0 を送るため)。
    // 買える時間はサーバー (MatchRulesModule.Economy) と同じくラウンドの準備の間にする
    public sealed class ClientEconomy
    {
        private bool _seenMoney;

        public ClientEconomy(int maxMoney)
        {
            if (maxMoney < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMoney), maxMoney, "maxMoney must be at least 1");
            }

            MaxMoney = maxMoney;
        }

        public int MaxMoney { get; }

        // 経済の無い試合なら null
        public WalletStatus? Status { get; private set; }

        // Status が変わったら true
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.SearchAndDestroy)
            {
                return Reset();
            }

            // 持ち主の欄の無いスナップショットは所持金を知らせないので、前の状態を残す
            if (!snapshot.HasOwner)
            {
                return false;
            }

            var money = Math.Max(0, snapshot.Owner.Money);
            _seenMoney |= money > 0;
            if (!_seenMoney)
            {
                return false;
            }

            var canBuy = match.State == GameModeState.Playing && match.HasRound && match.Round == RoundPhase.Preparing;

            // サーバーの上限がこちらの調整値より大きいと上限を超えた額が届くので、そのときは届いた額を上限として出す
            var status = new WalletStatus(money, Math.Max(MaxMoney, money), canBuy);
            if (Status.HasValue && Status.Value == status)
            {
                return false;
            }

            Status = status;
            return true;
        }

        // Status を捨てたら true
        public bool Reset()
        {
            var had = Status.HasValue;
            Status = null;
            _seenMoney = false;
            return had;
        }

        // 送れない Id は null を返して理由を problem に入れる。BuyRequestMessage は空の Id を書けず、長すぎる Id はサーバーが読めずに捨てるため
        public static BuyRequestMessage BuyRequest(string itemId, out string problem)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                problem = "no item id";
                return null;
            }

            if (Encoding.UTF8.GetByteCount(itemId) > MatchProtocol.MaxContentIdBytes)
            {
                problem = "the item id is longer than " + MatchProtocol.MaxContentIdBytes.ToString(CultureInfo.InvariantCulture) + " bytes";
                return null;
            }

            problem = null;
            return new BuyRequestMessage { ItemId = itemId };
        }
    }
}
