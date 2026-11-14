namespace Fps.Runtime
{
    /// <summary>
    /// 防具を持てるもの。<see cref="Damageable"/> と同じ GameObject に付いたコンポーネントがこれを実装していれば、
    /// <see cref="Pickup"/> の防具を拾える。無ければ防具は拾えない (<see cref="Fps.Core.PickupRejection.NoArmor"/>)。
    /// </summary>
    public interface IArmorHolder
    {
        /// <summary>今の防具。0 以上 <see cref="MaxArmor"/> 以下。</summary>
        int Armor { get; }

        /// <summary>防具の上限。1 以上。</summary>
        int MaxArmor { get; }

        /// <summary>防具を amount 足す。上限を超える分は捨てる。</summary>
        void AddArmor(int amount);
    }
}
