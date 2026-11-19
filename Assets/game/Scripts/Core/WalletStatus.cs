using System;
using System.Globalization;

namespace Shooter.Core
{
    // 買える時間かを受け取るのは、買える時間 (ラウンドの初めの数秒・購入の場所の中など) を決めるのがモードとサーバーで、UI は表示を切り替えるだけのため。
    public readonly struct WalletStatus : IEquatable<WalletStatus>
    {
        public WalletStatus(int money, int max, bool canBuy)
        {
            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            if (money < 0 || money > max)
            {
                throw new ArgumentOutOfRangeException(nameof(money), money, "money must be between 0 and max");
            }

            Money = money;
            Max = max;
            CanBuy = canBuy;
        }

        public int Money { get; }

        public int Max { get; }

        public bool CanBuy { get; }

        public bool IsFull => Max > 0 && Money >= Max;

        public bool CanAfford(int price)
        {
            if (price < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), price, "price must not be negative");
            }

            return Money >= price;
        }

        public bool Equals(WalletStatus other)
        {
            return Money == other.Money && Max == other.Max && CanBuy == other.CanBuy;
        }

        public override bool Equals(object obj)
        {
            return obj is WalletStatus other && Equals(other);
        }

        public static bool operator ==(WalletStatus left, WalletStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WalletStatus left, WalletStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Money, Max, CanBuy);
        }

        public override string ToString()
        {
            return "money=" + Money.ToString(CultureInfo.InvariantCulture) + "/" + Max.ToString(CultureInfo.InvariantCulture)
                + " canBuy=" + (CanBuy ? "yes" : "no");
        }
    }
}
