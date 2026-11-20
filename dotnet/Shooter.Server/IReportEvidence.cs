namespace Shooter.Server
{
    // 通報のファイルの書き方 (ServerReports) と証拠に何を残すかを分け、証拠を残さない起動では何も持たずに済むようにする。
    // 時刻を受け取らないのは、証拠は自分が受けた出来事の時刻で古いものを捨て、通報の間引きの時計とそろえる要が無いため。
    internal interface IReportEvidence
    {
        // 証拠のファイルの中身 (1 件 1 行で、各行を "\n" で終えた JSON Lines) を返す。
        // null は証拠を残さないという意味で、ファイルを作らせない。空の文字列は記録が無かったことを残す
        // reporterKey は通報した人の切断で変わらない識別 (ProductUserId)。1 人が全体の枠を使い切れないようにするために渡す
        string Describe(int targetId, string reporterKey);
    }
}
