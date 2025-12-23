using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

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
        /// Returns all emotes from the game data, filtered to exclude invalid/empty emotes.
        /// </summary>
        public IEnumerable<Emote> GetAllEmotes()
        {
            return emotes.Values
                .Where(e => 
                {
                    try
                    {
                        // Filter out emotes with empty names or invalid data
                        var name = e.Name.ToString();
                        return !string.IsNullOrWhiteSpace(name) && e.RowId > 0;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .OrderBy(e => e.Name.ToString());
        }

        /// <summary>
        /// Checks if an emote is unlocked by the player using FFXIVClientStructs UIState.
        /// </summary>
        public unsafe bool IsEmoteUnlocked(uint emoteId)
        {
            try
            {
                var emote = GetEmote(emoteId);
                if (emote == null || !emote.HasValue) return false;

                // Emotes with UnlockLink = 0 are always available (default emotes)
                if (emote.Value.UnlockLink == 0)
                {
                    return true;
                }

                // Use UIState to check if the emote is actually unlocked
                var uiState = UIState.Instance();
                if (uiState == null) return false;

                // Cast to ushort as IsEmoteUnlocked expects ushort
                return uiState->IsEmoteUnlocked((ushort)emoteId);
            }
            catch
            {
                // If we can't check, return false to be safe
                return false;
            }
        }

        /// <summary>
        /// Gets the emote command string (e.g., "/vpose")
        /// </summary>
        public string GetEmoteCommand(Emote emote)
        {
            try
            {
                var textCommandRef = emote.TextCommand;
                if (textCommandRef.Value.RowId > 0)
                {
                    var textCommand = textCommandRef.Value;
                    return textCommand.Command.ToString();
                }
            }
            catch (Exception ex)
            {
                // TextCommand might not be available or accessible
                try
                {
                    Service.Log?.Debug($"Could not get emote command for emote {emote.RowId}: {ex.Message}");
                }
                catch
                {
                    // Log might not be available during construction
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the emote icon ID
        /// </summary>
        public uint GetEmoteIcon(Emote emote)
        {
            try
            {
                return emote.Icon;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the emote name
        /// </summary>
        public string GetEmoteName(Emote emote)
        {
            try
            {
                return emote.Name.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

