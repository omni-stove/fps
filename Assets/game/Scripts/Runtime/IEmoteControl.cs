using System.Collections.Generic;

namespace Shooter.Runtime
{
    // 名前でなく Id で渡すのは、表示名は言語ごとに変わり、画面がエモートのデータのキーから引くため。
    public interface IEmoteControl
    {
        IReadOnlyList<string> EmoteIds { get; }

        // 例外でなく false を返すのは、ルールで Off・倒れている・乗っているなど、今出せるかを画面からは前もって知れないため。
        bool Play(string emoteId);
    }
}
