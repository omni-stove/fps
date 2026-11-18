using System;
using System.Collections.Generic;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // オフラインの AbilityHost と同じく、ロードアウトの能力の並びの n 番目を Ability(n+1) の入力で使う。置く物の能力だけを
    // 置く要求 (NetV2DeployablesPart.Deploy) にし、それ以外の能力はサーバーが扱わないので押しても何もしない
    public sealed class NetV2DeployInputPart : INetV2ClientPart
    {
        private static readonly string[] _slotPaths = { InputActionPaths.Ability1, InputActionPaths.Ability2 };

        private readonly NetV2DeployablesPart _deployables;
        private readonly IReadOnlyList<DeployAbilityDefinition> _slots;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Func<int, bool> _slotPressed;
        private readonly Func<bool> _canAct;
        private InputAction[] _actions;
        private bool _abilitiesOn;
        private bool _disposed;

        // slots は能力の枠の順で、置く物でない枠は null。押したかを引く口は、テストや別の入力から差し替えられるよう受け取る
        public NetV2DeployInputPart(
            NetV2DeployablesPart deployables,
            IReadOnlyList<DeployAbilityDefinition> slots,
            Func<bool> canAct,
            Func<NetV2LocalPlayer> localPlayer = null,
            Func<int, bool> slotPressed = null)
        {
            _deployables = deployables ?? throw new ArgumentNullException(nameof(deployables));
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _slotPressed = slotPressed ?? ReadSlotPressed;
        }

        public static int SlotCount => _slotPaths.Length;

        // ロードアウトの能力の並びを、枠ごとの置く物の定義にする。枠の数を超えた能力は使う入力が無いので外す
        public static IReadOnlyList<DeployAbilityDefinition> SlotsOf(IReadOnlyList<AbilityDefinition> abilities)
        {
            var slots = new DeployAbilityDefinition[SlotCount];
            if (abilities == null)
            {
                return slots;
            }

            for (var i = 0; i < slots.Length && i < abilities.Count; i++)
            {
                slots[i] = abilities[i] as DeployAbilityDefinition;
            }

            return slots;
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        // サーバーも同じルールの値で断るので、届いたスナップショットの値で送るかを決める
        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _abilitiesOn = snapshot.Match.Rules.Abilities == Shooter.Core.Abilities.On;
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || !_abilitiesOn || GameUi.IsPauseOpen)
            {
                return;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                if (!_slotPressed(i))
                {
                    continue;
                }

                var definition = _slots[i];
                var local = _localPlayer();
                if (definition == null || local == null || !_canAct())
                {
                    continue;
                }

                _deployables.Deploy(definition.Id, local.transform, local.EyePosition, local.AimDirection);
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private bool ReadSlotPressed(int slot)
        {
            if (_actions == null)
            {
                _actions = new InputAction[_slotPaths.Length];
                var asset = InputSystem.actions;
                for (var i = 0; i < _slotPaths.Length && asset != null; i++)
                {
                    _actions[i] = asset.FindAction(_slotPaths[i]);
                }
            }

            return slot >= 0 && slot < _actions.Length && _actions[slot] != null && _actions[slot].WasPressedThisFrame();
        }
    }
}
