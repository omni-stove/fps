using System.Collections.Generic;

namespace Shooter.Editor
{
    // 言語の表はキットに無いので、表のキーの漏れは表を持つ game 側の Editor のアセンブリが点検を足す (ExtensionPoints が拾う)
    public interface IReplacementAuditCheck
    {
        void Check(ICollection<ReplacementAuditFinding> findings);
    }
}
