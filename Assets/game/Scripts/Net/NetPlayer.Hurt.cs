using System;
using Fps.Runtime;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、被弾を演出 (<see cref="CharacterAudio"/> の被弾の声) に知らせる部分 (<see cref="ICharacterHurtSource"/>)。
    /// 体力は全クライアントに同期されているので、他人のプレイヤーの被弾は体力が減ったことで知る。
    /// 自分のプレイヤーの被弾は <see cref="NetCombatFeed"/> が <see cref="CombatEvents.DamageTaken"/> で届けるので、ここでは出さない。
    /// </summary>
    public sealed partial class NetPlayer : ICharacterHurtSource
    {
        /// <summary>他人のプレイヤーが生きている間に体力を減らされたとき、減った量を渡して呼ばれる。自分のプレイヤーとサーバーだけのインスタンスでは呼ばない。</summary>
        public event Action<int> Hurt;

        // NetworkBehaviour.IsLocalPlayer はプレイヤーオブジェクトとして生成したときしか立たないので、持ち主かどうかで答える
        bool ICharacterHurtSource.IsLocalPlayer => IsSpawned && IsOwner;

        private void ReportHurt(int previous, int current)
        {
            if (previous <= 0 || current >= previous || IsOwner || !IsClient)
            {
                return;
            }

            Hurt?.Invoke(previous - current);
        }
    }
}
