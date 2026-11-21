using System;

namespace Shooter.Core
{
    // 専用サーバーはアセット (GrappleAbilityDefinition) を読めないので、同じ値をデータ (grapple.json) から読んで持つ。
    // 掴める面のタグはサーバーの当たり (WorldCollider) に無いので持たず、レイヤーだけで見分ける。
    public sealed class GrappleType
    {
        // Physics.DefaultRaycastLayers と同じく、Ignore Raycast (2) の他の全部。
        public const int DefaultGrabbableLayers = ~(1 << 2);

        public GrappleType(string id, GrappleSpec spec, AbilitySpec ability, int grabbableLayers = DefaultGrabbableLayers)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be blank", nameof(id));
            }

            Id = id;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            GrabbableLayers = grabbableLayers;
        }

        public string Id { get; }

        public GrappleSpec Spec { get; }

        public AbilitySpec Ability { get; }

        public int GrabbableLayers { get; }

        public bool Grabs(int layer)
        {
            return layer >= 0 && layer < 32 && (GrabbableLayers & (1 << layer)) != 0;
        }
    }
}
