using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 出現直後の無敵の同期 (<see cref="NetSpawnProtection"/>) と、他のプレイヤーから半透明に見せる判定と材質の切り替え。
    /// </summary>
    public sealed class NetSpawnProtectionTests
    {
        private const string _litShaderName = "Universal Render Pipeline/Lit";

        [TestCase(2f, 2)]
        [TestCase(1.2f, 2)]
        [TestCase(0.01f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        [TestCase(float.NaN, 0)]
        public void SecondsRoundUpSoAProtectedPlayerAlwaysHasARow(float remaining, int expected)
        {
            Assert.That(NetSpawnProtection.SecondsFrom(remaining), Is.EqualTo(expected));
        }

        [Test]
        public void RowsCompareByClientAndSeconds()
        {
            var row = new NetSpawnProtection(3, 2);

            Assert.That(row, Is.EqualTo(new NetSpawnProtection(3, 2)));
            Assert.That(row, Is.Not.EqualTo(new NetSpawnProtection(3, 1)));
            Assert.That(row, Is.Not.EqualTo(new NetSpawnProtection(4, 2)));
            Assert.That(row.GetHashCode(), Is.EqualTo(new NetSpawnProtection(3, 2).GetHashCode()));
            Assert.That(row.ToString(), Is.EqualTo("client=3 protected=2s"));
        }

        [TestCase(false, true, 2, true)]
        [TestCase(true, true, 2, false)]
        [TestCase(false, false, 2, false)]
        [TestCase(false, true, 0, false)]
        public void OnlyOtherLivingProtectedPlayersLookTranslucent(bool isOwner, bool isAlive, int seconds, bool expected)
        {
            Assert.That(NetPlayer.ShouldShowSpawnProtection(isOwner, isAlive, seconds), Is.EqualTo(expected));
        }

        [Test]
        public void SetTranslucentSwitchesALitMaterialToAlphaBlendingAndBack()
        {
            var shader = Shader.Find(_litShaderName);
            if (shader == null)
            {
                Assert.Ignore(_litShaderName + " is not available; the Universal Render Pipeline package is missing");
            }

            var material = new Material(shader);
            try
            {
                NetPlayer.SetTranslucent(material, true);

                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f));
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Transparent"));

                NetPlayer.SetTranslucent(material, false);

                Assert.That(material.renderQueue, Is.EqualTo(shader.renderQueue));
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.False);
                Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f));
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Opaque"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SetTranslucentRejectsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => NetPlayer.SetTranslucent(null, true));
        }
    }
}
