using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using CkCommons;

namespace Idler
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "Idler";
        private const string CommandName = "/idler";
        private IDalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Idler");
        private ConfigWindow ConfigWindow { get; init; }
        private GameEmotes GameEmotes { get; init; }
        private bool IsMoving() => AgentMap.Instance()->IsPlayerMoving;
        private bool InCombat => Service.Condition[ConditionFlag.InCombat];
        private bool IsJumping => Service.Condition[ConditionFlag.Jumping];
        private bool IsBetweenAreas => Service.Condition[ConditionFlag.BetweenAreas] && Service.Condition[ConditionFlag.BetweenAreas51];
        
        // Delay tracking
        private DateTime? IdleStartTime { get; set; } = null;
        private bool HasPerformedEmoteThisIdle { get; set; } = false;

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.PluginInterface.Create<Service>(this);
            
            // Initialize CkCommons first (this creates CkCommons.Svc)
            CkCommonsHost.Init(pluginInterface, this, CkLogFilter.None);
            
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            ECommonsMain.Init(pluginInterface, this, Module.All);
            this.GameEmotes = new GameEmotes();
            Service.Framework.Update += onFrameworkUpdate;

            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the config window"
            });
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            this.PluginInterface.UiBuilder.Draw += DrawUI;
        }

        public unsafe void onFrameworkUpdate(object framework)
        {
            // Check if we should perform emote
            if (Configuration.EmoteId == 0) return; // No emote selected

            var emote = GameEmotes.GetEmote(Configuration.EmoteId);
            if (!emote.HasValue) return;

            // Check weapon state
            bool weaponUnsheathed = IsWeaponUnsheathed();
            if (weaponUnsheathed && !Configuration.Unsheathed)
                {
                // Reset idle state if weapon is unsheathed and we don't want to emote while unsheathed
                if (IsMoving())
                {
                    IdleStartTime = null;
                    HasPerformedEmoteThisIdle = false;
                }
                return;
            }

            // Check if we can perform emote
            if (!CanPerformEmote())
            {
                // Reset idle state if we can't perform emote
                IdleStartTime = null;
                HasPerformedEmoteThisIdle = false;
                return;
            }

            // Check if we're idle (not moving)
            if (IsMoving())
            {
                // Reset idle state when moving
                IdleStartTime = null;
                HasPerformedEmoteThisIdle = false;
                return;
            }

            // We're idle - start or update idle timer
            if (IdleStartTime == null)
                {
                IdleStartTime = DateTime.Now;
                HasPerformedEmoteThisIdle = false;
                }

            // Check if delay has passed
            if (!HasPerformedEmoteThisIdle && IdleStartTime.HasValue)
            {
                var idleDuration = (DateTime.Now - IdleStartTime.Value).TotalSeconds;
                if (idleDuration >= Configuration.IdleDelaySeconds)
                {
                    // Perform emote
                    var emoteCommand = GameEmotes.GetEmoteCommand(emote.Value);
                    if (!string.IsNullOrEmpty(emoteCommand))
                    {
                        Chat.SendMessage(emoteCommand);
                        HasPerformedEmoteThisIdle = true;
                    }
                }
            }
        }

        private bool CanPerformEmote()
        {
            return !IsMoving() && !InCombat && !IsJumping && !IsBetweenAreas;
        }

        private bool IsWeaponUnsheathed()
        {
            return UIState.Instance()->WeaponState.IsUnsheathed;
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
            ECommonsMain.Dispose();
            CkCommonsHost.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            ConfigWindow.IsOpen = true;
        }

        public void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            this.ConfigWindow.IsOpen = true;
        }
    }
}
