using System;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、ダウン中に動けなくする処理と、オーナーの tick ごとの通知。
    /// </summary>
    /// <remarks>
    /// ダウンはしゃがみと同じく tick の入力 (<see cref="MotorInput.Downed"/>) に乗せ、予測とサーバーで同じ
    /// <see cref="NetMotorSimulation"/> を通す。ただしクライアントからは送らない。サーバーは自分の知る状態を、
    /// 予測は同期された状態 (<see cref="NetPlayer.IsDowned"/>) を入れる。届くまでの間に食い違った tick は、照合で巻き戻される。
    /// </remarks>
    public sealed partial class NetworkPlayerMotor
    {
        private bool _downed;

        /// <summary>
        /// オーナー (予測するクライアントとホスト) で、tick の入力を処理した直後に呼ばれる。引数は tick。
        /// 入力 (<see cref="SubmitInputRpc"/>) と同じ頻度で送りたい要求 (蘇生) に使う。
        /// </summary>
        internal event Action<int> OwnerTicked;

        /// <summary>ダウン中として動かしているか。</summary>
        public bool IsDowned => _downed;

        /// <summary>
        /// ダウン中かどうかを設定する。次の tick の入力から効く。<see cref="NetPlayer"/> が同期されたダウンの状態から呼ぶ
        /// (サーバーではサーバーが書いた値、クライアントでは届いた値)。
        /// </summary>
        internal void SetDowned(bool downed)
        {
            _downed = downed;
        }
    }
}
