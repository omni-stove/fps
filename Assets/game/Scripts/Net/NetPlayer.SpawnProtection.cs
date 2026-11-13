using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、出現直後の無敵の見た目。
    /// 無敵の残りは試合の <see cref="NetTeamDeathmatch.SpawnProtections"/> で届くので、LateUpdate で自分の持ち主の行を引き、
    /// 他のプレイヤーの画面では無敵の間だけ体を半透明 (<see cref="SpawnProtectionAlpha"/>) にする。自分の画面の自分の体は変えない。
    /// 判定 (ダメージを受けない・撃つと解ける) はサーバーの <see cref="Fps.Core.TeamDeathmatchSession"/> が行う。
    /// </summary>
    public sealed partial class NetPlayer
    {
        /// <summary>他のプレイヤーから見た、無敵の最中の体の不透明度。</summary>
        public const float SpawnProtectionAlpha = 0.35f;

        // URP の Lit (リグの部品が使う既定の材質) の、不透明と半透明を切り替えるプロパティ
        private static readonly int _surfaceId = Shader.PropertyToID("_Surface");
        private static readonly int _blendId = Shader.PropertyToID("_Blend");
        private static readonly int _srcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int _dstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int _srcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int _dstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int _zWriteId = Shader.PropertyToID("_ZWrite");
        private const string _transparentKeyword = "_SURFACE_TYPE_TRANSPARENT";

        private bool _showsSpawnProtection;

        // 複製した材質を今どちらの描画の設定にしているか。ApplyVisuals が切り替わったときだけ書き直す
        private bool _materialsTranslucent;

        /// <summary>出現直後の無敵が切れるまでの秒数 (切り上げ)。無敵でない・試合がまだ届いていなければ 0。</summary>
        public int SpawnProtectionSeconds
        {
            get
            {
                var match = NetTeamDeathmatch.Current;
                return match != null && IsSpawned ? match.GetSpawnProtectionSeconds(OwnerClientId) : 0;
            }
        }

        /// <summary>出現直後の無敵の最中か。</summary>
        public bool IsSpawnProtected => SpawnProtectionSeconds > 0;

        /// <summary>このインスタンスの体を、無敵の最中として半透明にしているか。</summary>
        internal bool ShowsSpawnProtection => _showsSpawnProtection;

        /// <summary>
        /// 体を半透明にするか。他のプレイヤーで、生きていて、無敵の残りがあるときだけ true。
        /// 自分の体は一人称では描かず、三人称でも自分が無敵かは HUD の案内で分かるので変えない。
        /// </summary>
        internal static bool ShouldShowSpawnProtection(bool isOwner, bool isAlive, int seconds)
        {
            return !isOwner && isAlive && seconds > 0;
        }

        /// <summary>
        /// material を URP の Lit の半透明 (アルファブレンド) か、不透明に戻す。色のアルファは呼んだ側が決める。
        /// 他のシェーダーの材質では無いプロパティへの書き込みは無視されるので、描画が変わらないだけで壊れない。
        /// </summary>
        internal static void SetTranslucent(Material material, bool translucent)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            if (translucent)
            {
                material.SetFloat(_surfaceId, 1f);
                material.SetFloat(_blendId, 0f);
                material.SetFloat(_srcBlendId, (float)BlendMode.SrcAlpha);
                material.SetFloat(_dstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(_srcBlendAlphaId, (float)BlendMode.One);
                material.SetFloat(_dstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(_zWriteId, 0f);
                material.EnableKeyword(_transparentKeyword);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            material.SetFloat(_surfaceId, 0f);
            material.SetFloat(_srcBlendId, (float)BlendMode.One);
            material.SetFloat(_dstBlendId, (float)BlendMode.Zero);
            material.SetFloat(_srcBlendAlphaId, (float)BlendMode.One);
            material.SetFloat(_dstBlendAlphaId, (float)BlendMode.Zero);
            material.SetFloat(_zWriteId, 1f);
            material.DisableKeyword(_transparentKeyword);
            material.SetOverrideTag("RenderType", "Opaque");

            // Geometry を決め打ちせず、複製元と同じくシェーダーの既定の順 (-1) に戻す
            material.renderQueue = -1;
        }

        // LateUpdate で呼ぶ。切り替わったときだけ材質を書き直す
        private void RefreshSpawnProtection()
        {
            var show = ShouldShowSpawnProtection(IsOwner, IsAlive, SpawnProtectionSeconds);
            if (show == _showsSpawnProtection)
            {
                return;
            }

            _showsSpawnProtection = show;
            ApplyVisuals();
            Debug.Log("[Net] spawn protection look owner=" + OwnerClientId.ToString(CultureInfo.InvariantCulture)
                + " translucent=" + (show ? "true" : "false"), this);
        }

        private void ResetSpawnProtectionLook()
        {
            if (!_showsSpawnProtection)
            {
                return;
            }

            _showsSpawnProtection = false;
            ApplyVisuals();
        }
    }
}
