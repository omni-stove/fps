using System;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、回線の表示 (Fps.Net.UI の NetHud の F3) がパケットロスを測るための確かめの便。
    /// 持ち主が番号を <see cref="SendNetStatsProbe"/> で信頼性なしにサーバーへ送り、サーバーは同じ番号とサーバーの tick を
    /// 信頼性なしに持ち主へ返す。返事は <see cref="NetStatsProbeEchoed"/> で配る。行きと帰りのどちらで欠けても返事は来ない。
    /// </summary>
    /// <remarks>
    /// サーバーが返す数は <see cref="MaxNetStatsProbesPerSecond"/> を上限にし、持ち主が送り過ぎても返事は増やさない。
    /// </remarks>
    public sealed partial class NetPlayer
    {
        /// <summary>サーバーが 1 人の持ち主に 1 秒あたり返す確かめの便の上限。HUD が送る数 (1 秒に 20) より余裕を持たせる。</summary>
        public const float MaxNetStatsProbesPerSecond = 30f;

        // サーバーで、返してよい残りの数。毎秒 MaxNetStatsProbesPerSecond ずつ回復する
        private float _netStatsProbeBudget = MaxNetStatsProbesPerSecond;
        private double _netStatsProbeBudgetAt = double.NaN;

        /// <summary>
        /// 持ち主で、確かめの便の返事が届いたとき。引数は送った番号と、サーバーが返したときのサーバーの tick。
        /// 届いた順は送った順と限らない。
        /// </summary>
        internal event Action<ushort, int> NetStatsProbeEchoed;

        /// <summary>持ち主から sequence の確かめの便をサーバーへ送る。生成前・持ち主でなければ送らずに false。</summary>
        internal bool SendNetStatsProbe(ushort sequence)
        {
            if (!IsSpawned || !IsOwner)
            {
                return false;
            }

            NetStatsProbeRpc(sequence);
            return true;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void NetStatsProbeRpc(ushort sequence, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored a net stats probe from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            // 送り過ぎは毎回ログに出すと 1 秒に何十行にもなるので、黙って落とす (信頼性なしの便なので欠けとして数えられるだけ)
            if (!TakeNetStatsProbeBudget(Time.unscaledTimeAsDouble))
            {
                return;
            }

            NetStatsEchoRpc(sequence, NetworkManager.ServerTime.Tick);
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable)]
        private void NetStatsEchoRpc(ushort sequence, int serverTick, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            NetStatsProbeEchoed?.Invoke(sequence, serverTick);
        }

        /// <summary>サーバーで、時刻 now (秒) に返事を 1 つ返してよいか。よければ残りを 1 つ減らす。</summary>
        internal bool TakeNetStatsProbeBudget(double now)
        {
            if (!double.IsNaN(_netStatsProbeBudgetAt) && now > _netStatsProbeBudgetAt)
            {
                _netStatsProbeBudget = Mathf.Min(
                    MaxNetStatsProbesPerSecond,
                    _netStatsProbeBudget + (float)((now - _netStatsProbeBudgetAt) * MaxNetStatsProbesPerSecond));
            }

            _netStatsProbeBudgetAt = now;
            if (_netStatsProbeBudget < 1f)
            {
                return false;
            }

            _netStatsProbeBudget -= 1f;
            return true;
        }
    }
}
