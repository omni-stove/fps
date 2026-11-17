using NUnit.Framework;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    public sealed class NetV2StructuresTests
    {
        [Test]
        public void ClearStates_DropsStatesReceivedForThePreviousMap()
        {
            var structures = new NetV2Structures(null);
            var message = new StructureStateMessage { StructureId = 0 };
            message.SetWords(new ulong[] { 1UL });
            structures.Receive(message);
            Assert.AreEqual(1, structures.StateCount);

            structures.ClearStates();

            Assert.AreEqual(0, structures.StateCount);
        }
    }
}
