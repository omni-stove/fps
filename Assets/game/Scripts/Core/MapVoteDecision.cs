namespace Shooter.Core
{
    public readonly struct MapVoteDecision
    {
        // default ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _mapId;

        public MapVoteDecision(string mapId, MapVoteDecisionReason reason, int votes)
        {
            _mapId = mapId;
            Reason = reason;
            Votes = votes;
        }

        public string MapId => _mapId ?? string.Empty;

        // 候補が無いときに今のマップ Id を入れないのは、MapVoteStatus が候補に無い決定を受け付けないため。
        public bool KeepsCurrentMap => MapId.Length == 0;

        public MapVoteDecisionReason Reason { get; }

        public int Votes { get; }
    }
}
