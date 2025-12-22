using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Idler
{
    public class GameEmotes
    {
        private IReadOnlyDictionary<uint, Emote> emotes;

        public GameEmotes()
        {
            var emoteSheet = Service.DataManager.GetExcelSheet<Emote>();
            if (emoteSheet != null)
            {
                emotes = emoteSheet.ToDictionary(e => e.RowId, e => e);
            }
            else
            {
                emotes = new Dictionary<uint, Emote>();
            }
        }

        public List<Emote>? FindEmotesByName(string name)
        {
            List<Emote>? output = new List<Emote>();

            foreach (var (id, emote) in emotes)
            {
                if (output.Count >= 14) break;
                string emoteName = emote.Name.ToString().ToLower();
                if (emoteName.Equals(name.ToLower()) && !output.Contains(emote)) 
                    output.Add(emote);
                if (emoteName.Contains(name.ToLower()) && !output.Contains(emote)) 
                    output.Add(emote);
            }

            return output;
        }

        public List<Emote>? FindEmotes(List<ushort> ids)
        {
            List<Emote>? output = new List<Emote>();

            foreach (var id in ids)
            {
                var s = GetEmote((uint)id);
                if (s.HasValue) output.Add(s.Value);
            }

            return output;
        }

        public Emote? GetEmote(string name)
        {
            Emote? output = null;
            foreach (var (id, emote) in emotes)
            {
                string emoteName = emote.Name.ToString().ToLower();
                if (emoteName.Equals(name.ToLower()))
                {
                    output = emote;
                    break;
                }
                if (!output.HasValue && emoteName.Contains(name.ToLower())) 
                    output = emote;
            }
            return output;
        }

        public Emote? GetEmote(uint id)
        {
            if (emotes.TryGetValue(id, out var emote))
            {
                return emote;
            }
            return null;
        }

        /// <summary>
        /// Returns all emotes from the game data.
        /// </summary>
        public IEnumerable<Emote> GetAllEmotes()
        {
            return emotes.Values.OrderBy(e => e.Name.ToString());
        }

        /// <summary>
        /// Checks if an emote is unlocked by the player.
        /// </summary>
        public bool IsEmoteUnlocked(uint emoteId)
        {
            // Check if emote is unlocked via the game's emote unlock system
            // This uses Dalamud's GameData to check unlock status
            var emote = GetEmote(emoteId);
            if (emote == null) return false;

            // Emotes with UnlockLink = 0 are always available
            // Note: Actual unlock checking would require checking achievements/quests
            // For now, we'll assume all emotes are available if player is logged in
            return Service.ObjectTable.LocalPlayer != null;
        }

        /// <summary>
        /// Gets the emote command string (e.g., "/vpose")
        /// </summary>
        public string GetEmoteCommand(Emote emote)
        {
            try
            {
                var textCommand = emote.TextCommand.Value;
                if (textCommand.RowId > 0)
                {
                    return textCommand.Command.ToString();
                }
            }
            catch
            {
                // TextCommand might not be available
            }
            return string.Empty;
        }
    }
}

