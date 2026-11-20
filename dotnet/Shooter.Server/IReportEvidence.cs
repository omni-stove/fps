namespace Shooter.Server
{
    // 通報のファイルの書き方 (ServerReports) と証拠に何を残すかを分け、証拠を残さない起動では何も持たずに済むようにする。
    // 時刻を受け取らないのは、証拠は自分が受けた出来事の時刻で古いものを捨て、通報の間引きの時計とそろえる要が無いため。
    internal interface IReportEvidence
    {
        string Describe(int targetId);
    }
}
