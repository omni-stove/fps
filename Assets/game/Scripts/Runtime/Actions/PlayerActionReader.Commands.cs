using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class PlayerActionReader
    {
        private static readonly PlayerCommands[] _commands =
        {
            PlayerCommands.Melee,
            PlayerCommands.Prone,
            PlayerCommands.Ping,
            PlayerCommands.Ability1,
            PlayerCommands.Ability2,
            PlayerCommands.SquadCommand,
            PlayerCommands.UseStreak,
            PlayerCommands.LeanLeft,
            PlayerCommands.LeanRight,
            PlayerCommands.Chat,
            PlayerCommands.TeamChat,
            PlayerCommands.PushToTalk,
        };

        private readonly InputAction[] _commandActions = new InputAction[_commands.Length];
        private readonly bool[] _enabledCommandActions = new bool[_commands.Length];
        private bool _commandsLive;
        private PlayerCommands _pressedCommands;
        private PlayerCommands _heldCommands;

        public PlayerCommands PressedCommands => _pressedCommands;

        public PlayerCommands HeldCommands => _heldCommands;

        public static string CommandActionPath(PlayerCommands command)
        {
            switch (command)
            {
                case PlayerCommands.Melee:
                    return InputActionPaths.Melee;
                case PlayerCommands.Prone:
                    return InputActionPaths.Prone;
                case PlayerCommands.Ping:
                    return InputActionPaths.Ping;
                case PlayerCommands.Ability1:
                    return InputActionPaths.Ability1;
                case PlayerCommands.Ability2:
                    return InputActionPaths.Ability2;
                case PlayerCommands.SquadCommand:
                    return InputActionPaths.SquadCommand;
                case PlayerCommands.UseStreak:
                    return InputActionPaths.UseStreak;
                case PlayerCommands.LeanLeft:
                    return InputActionPaths.LeanLeft;
                case PlayerCommands.LeanRight:
                    return InputActionPaths.LeanRight;
                case PlayerCommands.Chat:
                    return InputActionPaths.Chat;
                case PlayerCommands.TeamChat:
                    return InputActionPaths.TeamChat;
                case PlayerCommands.PushToTalk:
                    return InputActionPaths.PushToTalk;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, "command must be a single PlayerCommands button");
            }
        }

        public InputAction CommandAction(PlayerCommands command)
        {
            return _commandActions[CommandIndex(command)];
        }

        public void SetCommandAction(PlayerCommands command, InputAction action)
        {
            var index = CommandIndex(command);
            if (_commandsLive)
            {
                ReleaseCommandAction(index);
            }

            _commandActions[index] = action;
            if (_commandsLive)
            {
                AcquireCommandAction(index);
            }
        }

        public bool TryResolveCommandActions()
        {
            AppliedInputBindings.EnsureLoaded();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていないため、指示のボタンは読みません。", this);
                return false;
            }

            foreach (var command in _commands)
            {
                var path = CommandActionPath(command);
                var action = actions.FindAction(path);
                if (action == null)
                {
                    Debug.LogWarning($"{LogTag} Input Actions '{actions.name}' に '{path}' がないため、{command} は読みません。", this);
                }

                SetCommandAction(command, action);
            }

            return true;
        }

        public PlayerCommands ReadCommands()
        {
            var pressed = PlayerCommands.None;
            var held = PlayerCommands.None;
            for (var i = 0; i < _commands.Length; i++)
            {
                var action = _commandActions[i];
                if (action == null || !action.enabled)
                {
                    continue;
                }

                if (action.WasPressedThisFrame())
                {
                    pressed |= _commands[i];
                }

                if (action.IsPressed())
                {
                    held |= _commands[i];
                }
            }

            _pressedCommands = pressed;
            _heldCommands = held;
            return pressed;
        }

        private void OnEnable()
        {
            _commandsLive = true;
            for (var i = 0; i < _commandActions.Length; i++)
            {
                AcquireCommandAction(i);
            }
        }

        private void OnDisable()
        {
            _commandsLive = false;
            for (var i = 0; i < _commandActions.Length; i++)
            {
                ReleaseCommandAction(i);
            }

            _pressedCommands = PlayerCommands.None;
            _heldCommands = PlayerCommands.None;
        }

        // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたものだけ後で無効に戻す。
        private void AcquireCommandAction(int index)
        {
            var action = _commandActions[index];
            _enabledCommandActions[index] = action != null && !action.enabled;
            if (_enabledCommandActions[index])
            {
                action.Enable();
            }
        }

        private void ReleaseCommandAction(int index)
        {
            var action = _commandActions[index];
            if (_enabledCommandActions[index] && action != null)
            {
                action.Disable();
            }

            _enabledCommandActions[index] = false;
        }

        private static int CommandIndex(PlayerCommands command)
        {
            var index = Array.IndexOf(_commands, command);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(command), command, "command must be a single PlayerCommands button");
            }

            return index;
        }
    }
}
