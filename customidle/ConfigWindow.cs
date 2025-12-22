using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Lumina.Excel.Sheets;

namespace Idler
{
    public class ConfigWindow : Window, IDisposable
    {
        private Configuration Configuration;
        private Plugin Plugin;
        private GameEmotes GameEmotes;
        private List<Emote> AllEmotes;
        private List<Emote> FilteredEmotes;
        private string[] EmoteNames = Array.Empty<string>();
        private int SelectedEmoteIndex = -1;
        private string FilterText = string.Empty;

        public ConfigWindow(Plugin plugin) : base(
            "Idler",
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.Size = new Vector2(400, 400);
            this.SizeCondition = ImGuiCond.Always;
            this.Configuration = plugin.Configuration;
            this.Plugin = plugin;
            this.GameEmotes = new GameEmotes();
            this.AllEmotes = GameEmotes.GetAllEmotes().ToList();
            this.FilteredEmotes = new List<Emote>(AllEmotes);
            UpdateEmoteNames();
            
            // Find current emote index if EmoteId is set
            if (this.Configuration.EmoteId > 0)
            {
                var currentEmote = GameEmotes.GetEmote(this.Configuration.EmoteId);
                if (currentEmote.HasValue)
                {
                    SelectedEmoteIndex = FilteredEmotes.FindIndex(e => e.RowId == currentEmote.Value.RowId);
                }
            }
        }

        private void UpdateEmoteNames()
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                FilteredEmotes = new List<Emote>(AllEmotes);
            }
            else
            {
                FilteredEmotes = AllEmotes.Where(e => 
                {
                    var name = e.Name.ToString().ToLower();
                    var command = GameEmotes.GetEmoteCommand(e).ToLower();
                    return name.Contains(FilterText.ToLower()) || command.Contains(FilterText.ToLower());
                }).ToList();
            }
            
            EmoteNames = FilteredEmotes.Select(e => 
            {
                var command = GameEmotes.GetEmoteCommand(e);
                var unlocked = IsEmoteUnlocked(e);
                var status = unlocked ? "[✓]" : "[✗]";
                return $"{status} {e.Name} ({command})";
            }).ToArray();
            
            // Update selected index to match filtered list
            if (this.Configuration.EmoteId > 0)
            {
                var currentEmote = GameEmotes.GetEmote(this.Configuration.EmoteId);
                if (currentEmote.HasValue)
                {
                    SelectedEmoteIndex = FilteredEmotes.FindIndex(e => e.RowId == currentEmote.Value.RowId);
                }
                else
                {
                    SelectedEmoteIndex = -1;
                }
            }
        }

        private bool IsEmoteUnlocked(Emote emote)
        {
            // Simplified check - for now, assume all emotes are available if player is logged in
            // Actual unlock checking would require checking achievements/quests via UnlockLink
            if (Service.ObjectTable.LocalPlayer == null) return false;
            
            // For now, assume unlocked if player is logged in
            // You can enhance this by checking actual unlock conditions based on emote.UnlockLink
            return true;
        }

        public override void Draw()
        {
            ImGui.Text("Select Emote:");
            ImGui.Spacing();

            // Filter input
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##Filter", "Search emotes...", ref FilterText, 256))
            {
                UpdateEmoteNames();
            }

            ImGui.Spacing();

            // Emote selection combo
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##EmoteSelect", ref SelectedEmoteIndex, EmoteNames, EmoteNames.Length))
            {
                if (SelectedEmoteIndex >= 0 && SelectedEmoteIndex < FilteredEmotes.Count)
                {
                    var selectedEmote = FilteredEmotes[SelectedEmoteIndex];
                    this.Configuration.EmoteId = selectedEmote.RowId;
                    this.Configuration.Emote = GameEmotes.GetEmoteCommand(selectedEmote); // Keep for compatibility
                    this.Configuration.Save();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Delay input
            ImGui.Text("Idle Delay (seconds):");
            ImGui.SetNextItemWidth(100);
            int delay = this.Configuration.IdleDelaySeconds;
            if (ImGui.InputInt("##Delay", ref delay, 1, 5))
            {
                if (delay < 0) delay = 0;
                this.Configuration.IdleDelaySeconds = delay;
                this.Configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextWrapped("Time to wait before performing emote after becoming idle");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Unsheathed checkbox
            var unsheathedConfig = this.Configuration.Unsheathed;
            if (ImGui.Checkbox("Also perform emote while unsheathed?", ref unsheathedConfig))
            {
                this.Configuration.Unsheathed = unsheathedConfig;
                this.Configuration.Save();
            }

            // Legend
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "[✓] = Unlocked");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "[✗] = Locked");
        }

        public void Dispose() { }
    }
}
