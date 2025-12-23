using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Idler
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public string Emote { get; set; } = string.Empty; // Legacy support
        public uint EmoteId { get; set; } = 0; // New: Store emote ID
        public bool Unsheathed { get; set; } = true;
        public int IdleDelaySeconds { get; set; } = 0; // Delay before performing emote after becoming idle
        public bool HideLockedEmotes { get; set; } = false; // Hide locked emotes from the list

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
