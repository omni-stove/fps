namespace Fps.Net
{
    /// <summary><see cref="NetworkPlayerMotor"/> がこのプロセスで担う役割。</summary>
    public enum NetworkPlayerRole
    {
        /// <summary>まだ生成されていない、または破棄された。</summary>
        None,

        /// <summary>ホスト自身のプレイヤー。サーバー上で入力をそのまま確定させる。</summary>
        HostOwner,

        /// <summary>サーバー上の他クライアントのプレイヤー。受け取った入力で動かし、確定状態を返す。</summary>
        ServerSimulated,

        /// <summary>オーナーのクライアント。予測して動かし、確定状態と照合する。</summary>
        Predicted,

        /// <summary>他人のクライアント上のプレイヤー。受け取った状態を遅らせて補間表示する。</summary>
        Interpolated,
    }
}
