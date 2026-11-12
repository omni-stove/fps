namespace Fps.Core
{
    /// <summary>
    /// 参加した順にチームを交互に割り当てる。最初は Alpha、次は Bravo、その次は Alpha…と続く。
    /// 抜けた人がいても順番は戻さない。
    /// </summary>
    public sealed class TeamAssigner
    {
        private Team _next = Team.Alpha;

        /// <summary>次に割り当てるチーム。<see cref="Assign"/> を呼ぶまで変わらない。</summary>
        public Team Peek => _next;

        /// <summary>次のチームを返し、順番を 1 つ進める。</summary>
        public Team Assign()
        {
            var team = _next;
            _next = team == Team.Alpha ? Team.Bravo : Team.Alpha;
            return team;
        }
    }
}
