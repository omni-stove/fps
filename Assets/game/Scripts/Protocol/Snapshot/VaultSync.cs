using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// プレイヤーの乗り越えの状態 (Netcode の <c>NetVaultState</c> の中身)。経路は始めた位置からの相対の 3 点。
    /// </summary>
    public struct VaultSync
    {
        public bool IsVaulting { get; set; }

        public Vector3 Rise { get; set; }

        public Vector3 Cross { get; set; }

        public Vector3 Land { get; set; }

        /// <summary>乗り越え始めてからの秒数。</summary>
        public float Elapsed { get; set; }

        public static VaultSync None => default;

        public VaultSync(bool isVaulting, Vector3 rise, Vector3 cross, Vector3 land, float elapsed)
        {
            IsVaulting = isVaulting;
            Rise = rise;
            Cross = cross;
            Land = land;
            Elapsed = elapsed;
        }

        public bool SameAs(in VaultSync other)
        {
            return IsVaulting == other.IsVaulting
                && Same.Bits(Rise, other.Rise)
                && Same.Bits(Cross, other.Cross)
                && Same.Bits(Land, other.Land)
                && Same.Bits(Elapsed, other.Elapsed);
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteBool(IsVaulting);
            writer.WriteVector3(Rise);
            writer.WriteVector3(Cross);
            writer.WriteVector3(Land);
            writer.WriteSingle(Elapsed);
        }

        internal static VaultSync Read(MessageReader reader)
        {
            return new VaultSync(
                reader.ReadBool(),
                reader.ReadFiniteVector3("vault rise"),
                reader.ReadFiniteVector3("vault cross"),
                reader.ReadFiniteVector3("vault land"),
                reader.ReadFiniteSingle("vault time"));
        }
    }
}
