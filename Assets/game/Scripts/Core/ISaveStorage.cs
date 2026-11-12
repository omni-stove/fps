namespace Fps.Core
{
    /// <summary>
    /// セーブデータの文字列を名前 (スロット) ごとに置く場所。Core はファイルに触れないので、実際の読み書きは Runtime 側で実装する。
    /// 中身は <see cref="CampaignSave.ToJson"/> の結果で、解釈は <see cref="CampaignSave.FromJson"/> が行う。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>slot に置いた文字列を読む。無ければ false を返し、text は null。</summary>
        bool TryRead(string slot, out string text);

        /// <summary>slot に text を置く。既にあれば置き換える。途中で失敗しても前の内容が壊れないように書くこと。</summary>
        void Write(string slot, string text);

        /// <summary>slot を消す。無ければ何もしない。</summary>
        void Delete(string slot);
    }
}
