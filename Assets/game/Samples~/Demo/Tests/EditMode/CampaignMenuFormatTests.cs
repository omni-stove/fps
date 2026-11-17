using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CampaignMenuFormatTests
    {
        [Test]
        public void EveryStartItemHasALabel()
        {
            foreach (CampaignMenuItemKind kind in Enum.GetValues(typeof(CampaignMenuItemKind)))
            {
                if (kind == CampaignMenuItemKind.Mission || kind == CampaignMenuItemKind.Difficulty)
                {
                    continue;
                }

                Assert.That(CampaignMenuFormat.ItemLabel(kind), Is.Not.Empty, kind.ToString());
            }

            Assert.That(CampaignMenuFormat.DifficultyLabel(CampaignDifficulty.Easy), Is.EqualTo("かんたん"));
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMenuFormat.DifficultyLabel((CampaignDifficulty)99));
            Assert.That(CampaignMenuFormat.ConfirmRestartQuestion("Operation One"), Does.Contain("Operation One"));
        }
    }
}
