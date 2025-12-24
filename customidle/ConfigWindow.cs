using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Lumina.Excel.Sheets;
using ECommons.ImGuiMethods;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Runtime.CompilerServices;
using CkCommons.Gui;
using OtterGui;

namespace Idler
{
    public class ConfigWindow : Window, IDisposable
    {
        private Configuration Configuration;
        private Plugin Plugin;
        private GameEmotes GameEmotes;
        private List<Emote> AllEmotes = new();
        private List<Emote> FilteredEmotes = new();
        private string[] EmoteNames = Array.Empty<string>();
        private int SelectedEmoteIndex = -1;
        private string FilterText = string.Empty;
        private string ComboSearchText = string.Empty;
        private float IconSize;
        
        // Animation and styling
        private float borderAnimationTime = 0f;
        private float scrollPosition = 0f;
        private float lastScrollPosition = 0f;

        public ConfigWindow(Plugin plugin) : base(
            "Idler Configuration",
            ImGuiWindowFlags.None)
        {
            this.Size = new Vector2(500, 400);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            
            // Compact size constraints - everything should fit without scrolling
            this.SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new Vector2(450, 350),
                MaximumSize = new Vector2(700, 500),
            };
            this.Configuration = plugin.Configuration;
            this.Plugin = plugin;
            this.GameEmotes = new GameEmotes();
            this.IconSize = ImGui.GetTextLineHeight();
            
            try
            {
                // Load all emotes and filter out invalid ones
                this.AllEmotes = GameEmotes.GetAllEmotes()
                    .Where(e => 
                    {
                        try
                        {
                            // Additional filtering: ensure emote has a valid command
                            var command = GameEmotes.GetEmoteCommand(e);
                            var name = GameEmotes.GetEmoteName(e);
                            return !string.IsNullOrWhiteSpace(name) && e.RowId > 0;
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToList();
                
                // Sort and filter emotes
                UpdateEmoteList();
                
                // Find current emote index if EmoteId is set
                if (this.Configuration.EmoteId > 0)
                {
                    var currentEmote = GameEmotes.GetEmote(this.Configuration.EmoteId);
                    if (currentEmote.HasValue && FilteredEmotes != null)
                    {
                        SelectedEmoteIndex = FilteredEmotes.FindIndex(e => e.RowId == currentEmote.Value.RowId);
                    }
                }
            }
            catch (Exception ex)
            {
                // If emote loading fails, initialize with empty lists
                Service.Log.Error($"Failed to load emotes: {ex.Message}");
                this.AllEmotes = new List<Emote>();
                this.FilteredEmotes = new List<Emote>();
                this.EmoteNames = Array.Empty<string>();
            }
        }

        private void UpdateEmoteList()
        {
            try
            {
                // Start with all emotes
                var emotesToShow = AllEmotes.AsEnumerable();

                // Apply search filter if present
                if (!string.IsNullOrEmpty(ComboSearchText))
                {
                    emotesToShow = emotesToShow.Where(e => 
                    {
                        try
                        {
                            var name = GameEmotes.GetEmoteName(e).ToLower();
                            var command = GameEmotes.GetEmoteCommand(e).ToLower();
                            var searchLower = ComboSearchText.ToLower();
                            
                            if (name.Contains(searchLower) || command.Contains(searchLower))
                                return true;
                            
                            // Also check if search is a number matching the emote ID
                            if (ushort.TryParse(ComboSearchText, out var searchId) && searchId == e.RowId)
                                return true;
                            
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }

                // Filter out locked emotes if the option is enabled
                if (Configuration.HideLockedEmotes)
                {
                    emotesToShow = emotesToShow.Where(e => 
                    {
                        try
                        {
                            return GameEmotes.IsEmoteUnlocked(e.RowId);
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }

                // Sort: unlocked first (alphabetical), then locked (alphabetical)
                FilteredEmotes = emotesToShow
                    .OrderBy(e => 
                    {
                        try
                        {
                            var unlocked = GameEmotes.IsEmoteUnlocked(e.RowId);
                            return unlocked ? 0 : 1; // 0 = unlocked (first), 1 = locked (second)
                        }
                        catch
                        {
                            return 1; // Treat errors as locked
                        }
                    })
                    .ThenBy(e => 
                    {
                        try
                        {
                            return GameEmotes.GetEmoteName(e);
                        }
                        catch
                        {
                            return "zzz"; // Put errors at the end
                        }
                    })
                    .ToList();
                
                // Update emote names array for display
                EmoteNames = FilteredEmotes.Select(e => 
                {
                    try
                    {
                        var command = GameEmotes.GetEmoteCommand(e);
                        var unlocked = GameEmotes.IsEmoteUnlocked(e.RowId);
                        var status = unlocked ? "[✓]" : "[✗]";
                        var emoteName = GameEmotes.GetEmoteName(e);
                        return $"{status} {emoteName} ({command})";
                    }
                    catch
                    {
                        return "[?] Unknown Emote";
                    }
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
            catch (Exception ex)
            {
                Service.Log.Error($"Error updating emote list: {ex.Message}");
                EmoteNames = Array.Empty<string>();
                FilteredEmotes = new List<Emote>();
            }
        }


        private bool DrawIconTextFrame(uint iconId, string text, bool hoverColor = false)
        {
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(IconSize) + ImGui.GetStyle().FramePadding * 2;
            var frameSize = new Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
            var isHovered = ImGui.IsMouseHoveringRect(pos, pos + frameSize);

            // Use cute theme colors for hover
            var bgColor = isHovered && hoverColor 
                ? CuteTheme.FrameBgHovered 
                : CuteTheme.FrameBg;
            
            using (ImRaii.PushColor(ImGuiCol.FrameBg, bgColor))
            {
                if (ImGui.BeginChildFrame(ImGui.GetID($"iconTextFrame_{iconId}_{text}"), frameSize))
                {
                    var drawlist = ImGui.GetWindowDrawList();
                    if (ThreadLoadImageHandler.TryGetIconTextureWrap(iconId, false, out var icon))
                    {
                        drawlist.AddImage(icon.Handle, pos, pos + new Vector2(size.Y));
                    }
                    var textSize = ImGui.CalcTextSize(text);
                    var textColor = isHovered ? CuteTheme.AccentLavender : CuteTheme.Text;
                    drawlist.AddText(pos + new Vector2(size.Y + ImGui.GetStyle().FramePadding.X, size.Y / 2f - textSize.Y / 2f), ImGui.GetColorU32(textColor), text);
                    
                    // Add cute border on hover
                    if (isHovered && hoverColor)
                    {
                        var borderColor = CuteTheme.AccentPink;
                        borderColor.W = 0.5f;
                        drawlist.AddRect(pos, pos + frameSize, ImGui.GetColorU32(borderColor), 4f, ImDrawFlags.None, 2f);
                    }
                }
                ImGui.EndChildFrame();
            }
            return ImGui.IsItemClicked();
        }

        private void DrawComboEmote(string id)
        {
            var previewEmote = Configuration.EmoteId > 0 ? GameEmotes.GetEmote(Configuration.EmoteId) : null;
            var previewName = previewEmote.HasValue ? GameEmotes.GetEmoteName(previewEmote.Value) : "Select emote...";
            var previewIcon = previewEmote.HasValue ? GameEmotes.GetEmoteIcon(previewEmote.Value) : 0u;

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
            {
                if (ThreadLoadImageHandler.TryGetIconTextureWrap(previewIcon, false, out var iconPicture))
                {
                    ImGui.Image(iconPicture.Handle, new Vector2(IconSize));
                }
                else
                {
                    ImGui.Dummy(new Vector2(IconSize));
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                if (ImGui.BeginCombo($"##{id}", previewName, ImGuiComboFlags.HeightLargest))
                {
                    if (ImGui.IsWindowAppearing())
                    {
                        ComboSearchText = string.Empty;
                        ImGui.SetKeyboardFocusHere();
                    }

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputTextWithHint($"##{id}SearchInput", "Search...", ref ComboSearchText, 256))
                    {
                        // Update the filtered list when search text changes
                        UpdateEmoteList();
                    }

                    if (ImGui.BeginChild($"##{id}SearchScroll", new Vector2(ImGui.GetContentRegionAvail().X, 300 * ImGuiHelpers.GlobalScale)))
                    {
                        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, ImGui.GetColorU32(ImGuiCol.ButtonHovered)))
                        {
                            // Use the filtered and sorted list
                            foreach (var emote in FilteredEmotes)
                            {
                                try
                                {
                                    var emoteName = GameEmotes.GetEmoteName(emote);
                                    var emoteCommand = GameEmotes.GetEmoteCommand(emote);
                                    
                                    // Skip if name or command is empty
                                    if (string.IsNullOrWhiteSpace(emoteName))
                                        continue;
                                    
                                    var displayText = string.IsNullOrEmpty(emoteCommand) ? emoteName : $"{emoteName} ({emoteCommand})";
                                    var iconId = GameEmotes.GetEmoteIcon(emote);
                                    var unlocked = GameEmotes.IsEmoteUnlocked(emote.RowId);
                                    var status = unlocked ? "[✓]" : "[✗]";
                                    var fullText = $"{status} {displayText}";

                                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                    if (DrawIconTextFrame(iconId, fullText, true))
                                    {
                                        Configuration.EmoteId = emote.RowId;
                                        Configuration.Emote = emoteCommand; // Keep for compatibility
                                        Configuration.Save();
                                        ImGui.CloseCurrentPopup();
                                    }
                                }
                                catch
                                {
                                    // Skip emotes that cause errors
                                }
                            }
                        }
                        ImGui.EndChild();
                    }
                    ImGui.EndCombo();
                }
            }
        }

        // Cute dark mode color theme
        private static class CuteTheme
        {
            // Dark background colors
            public static readonly Vector4 WindowBg = new Vector4(0.12f, 0.12f, 0.15f, 0.95f); // Dark purple-gray
            public static readonly Vector4 ChildBg = new Vector4(0.10f, 0.10f, 0.13f, 0.90f);
            public static readonly Vector4 FrameBg = new Vector4(0.15f, 0.15f, 0.18f, 0.80f);
            public static readonly Vector4 FrameBgHovered = new Vector4(0.20f, 0.20f, 0.25f, 0.90f);
            public static readonly Vector4 FrameBgActive = new Vector4(0.25f, 0.20f, 0.30f, 1.0f);
            
            // Cute pastel accent colors
            public static readonly Vector4 AccentPink = new Vector4(1.0f, 0.6f, 0.8f, 1.0f);      // #FF99CC
            public static readonly Vector4 AccentLavender = new Vector4(0.8f, 0.7f, 1.0f, 1.0f);  // #CCB3FF
            public static readonly Vector4 AccentMint = new Vector4(0.6f, 0.95f, 0.85f, 1.0f);    // #99F2D9
            public static readonly Vector4 AccentPeach = new Vector4(1.0f, 0.85f, 0.7f, 1.0f);    // #FFD9B3
            public static readonly Vector4 AccentSky = new Vector4(0.7f, 0.9f, 1.0f, 1.0f);       // #B3E6FF
            
            // Text colors
            public static readonly Vector4 Text = new Vector4(0.95f, 0.95f, 0.98f, 1.0f);
            public static readonly Vector4 TextMuted = new Vector4(0.7f, 0.7f, 0.75f, 1.0f);
            
            // Border colors (animated)
            public static Vector4 GetBorderColor(float time, float scrollDelta)
            {
                // Create a gradient that shifts based on time and scroll
                var baseHue = (time * 0.3f + scrollDelta * 5f) % 1f;
                var r = 0.5f + 0.5f * (float)Math.Sin(baseHue * Math.PI * 2 + 0);
                var g = 0.5f + 0.5f * (float)Math.Sin(baseHue * Math.PI * 2 + 2.09f);
                var b = 0.5f + 0.5f * (float)Math.Sin(baseHue * Math.PI * 2 + 4.18f);
                return new Vector4(r, g, b, 0.8f);
            }
            
            // Button colors
            public static readonly Vector4 Button = new Vector4(0.25f, 0.20f, 0.30f, 0.80f);
            public static readonly Vector4 ButtonHovered = new Vector4(0.35f, 0.28f, 0.42f, 1.0f);
            public static readonly Vector4 ButtonActive = new Vector4(0.45f, 0.35f, 0.52f, 1.0f);
        }
        
        public override void PreDraw()
        {
            base.PreDraw();
            
            try
            {
                // Only push title bar colors here (needed before window creation)
                // These will be popped in PostDraw to prevent affecting other windows
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(ImGui.GetStyle().WindowPadding.X, 0));
                ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.331f, 0.081f, 0.169f, 0.803f));
                ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.579f, 0.170f, 0.359f, 0.828f));
                
                // Update animation time safely
                try
                {
                    var io = ImGui.GetIO();
                    if (io.DeltaTime > 0)
                    {
                        borderAnimationTime += io.DeltaTime;
                    }
                }
                catch
                {
                    // If DeltaTime access fails, use a small increment
                    borderAnimationTime += 0.016f; // ~60fps
                }
            }
            catch (Exception ex)
            {
                Service.Log?.Error($"Error in PreDraw: {ex.Message}");
            }
        }
        
        public override void PostDraw()
        {
            try
            {
                // Pop title bar colors and style var (pushed in PreDraw)
                // This ensures they don't leak to other windows
                ImGui.PopStyleColor(2); // TitleBg and TitleBgActive
                ImGui.PopStyleVar(); // WindowPadding
            }
            catch (Exception ex)
            {
                Service.Log?.Error($"Error in PostDraw: {ex.Message}\n{ex.StackTrace}");
            }
            
            base.PostDraw();
        }
        
        // Helper methods using CkGui
        private void CenterText(string text, Vector4? color = null)
        {
            var textSize = ImGui.CalcTextSize(text);
            var contentWidth = CkGui.GetWindowContentRegionWidth();
            var offset = (contentWidth - textSize.X) / 2;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            
            if (color.HasValue)
            {
                ImGui.TextColored(color.Value, text);
            }
            else
            {
                ImGui.TextUnformatted(text);
            }
        }
        
        private void DrawAnimatedBorder()
        {
            try
            {
                // Safety check - ensure we're in a valid window context
                if (!ImGui.IsWindowHovered() && !ImGui.IsWindowFocused())
                {
                    // Still allow drawing, just be more careful
                }
                
                var drawList = ImGui.GetWindowDrawList();
                // Check if drawList is valid - IsNull might not exist, so use try-catch instead
                
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                
                // Validate window size
                if (windowSize.X <= 0 || windowSize.Y <= 0) return;
                
                var borderThickness = 3f * ImGuiHelpers.GlobalScale;
                
                // Calculate scroll delta for border animation
                try
                {
                    scrollPosition = ImGui.GetScrollY();
                    var scrollDelta = Math.Abs(scrollPosition - lastScrollPosition);
                    lastScrollPosition = scrollPosition;
                    var borderColor = CuteTheme.GetBorderColor(borderAnimationTime, scrollDelta);
                    
                    // Draw border with rounded corners
                    var rounding = 8f * ImGuiHelpers.GlobalScale;
            
                    // Top border
                    drawList.AddLine(
                        new Vector2(windowPos.X + rounding, windowPos.Y),
                        new Vector2(windowPos.X + windowSize.X - rounding, windowPos.Y),
                        ImGui.GetColorU32(borderColor),
                        borderThickness
                    );
                    
                    // Bottom border
                    drawList.AddLine(
                        new Vector2(windowPos.X + rounding, windowPos.Y + windowSize.Y),
                        new Vector2(windowPos.X + windowSize.X - rounding, windowPos.Y + windowSize.Y),
                        ImGui.GetColorU32(borderColor),
                        borderThickness
                    );
                    
                    // Left border
                    drawList.AddLine(
                        new Vector2(windowPos.X, windowPos.Y + rounding),
                        new Vector2(windowPos.X, windowPos.Y + windowSize.Y - rounding),
                        ImGui.GetColorU32(borderColor),
                        borderThickness
                    );
                    
                    // Right border
                    drawList.AddLine(
                        new Vector2(windowPos.X + windowSize.X, windowPos.Y + rounding),
                        new Vector2(windowPos.X + windowSize.X, windowPos.Y + windowSize.Y - rounding),
                        ImGui.GetColorU32(borderColor),
                        borderThickness
                    );
                    
                    // Rounded corners with cute accent colors
                    var cornerRadius = rounding;
                    var cornerThickness = borderThickness * 1.5f;
                    
                    // Top-left corner (Pink)
                    DrawRoundedCorner(drawList, 
                        new Vector2(windowPos.X + cornerRadius, windowPos.Y + cornerRadius),
                        cornerRadius, 180f, 270f, CuteTheme.AccentPink, cornerThickness);
                    
                    // Top-right corner (Lavender)
                    DrawRoundedCorner(drawList,
                        new Vector2(windowPos.X + windowSize.X - cornerRadius, windowPos.Y + cornerRadius),
                        cornerRadius, 270f, 360f, CuteTheme.AccentLavender, cornerThickness);
                    
                    // Bottom-left corner (Mint)
                    DrawRoundedCorner(drawList,
                        new Vector2(windowPos.X + cornerRadius, windowPos.Y + windowSize.Y - cornerRadius),
                        cornerRadius, 90f, 180f, CuteTheme.AccentMint, cornerThickness);
                    
                    // Bottom-right corner (Peach)
                    DrawRoundedCorner(drawList,
                        new Vector2(windowPos.X + windowSize.X - cornerRadius, windowPos.Y + windowSize.Y - cornerRadius),
                        cornerRadius, 0f, 90f, CuteTheme.AccentPeach, cornerThickness);
            
                    // Add sparkle effect on scroll (simplified, no content size needed)
                    if (scrollDelta > 0.1f && windowSize.Y > 0)
                    {
                        var sparkleColor = CuteTheme.AccentSky;
                        sparkleColor.W = 0.6f;
                        var sparklePos = new Vector2(
                            windowPos.X + windowSize.X * 0.5f,
                            windowPos.Y + windowSize.Y * 0.5f
                        );
                        DrawSparkle(drawList, sparklePos, sparkleColor);
                    }
                }
                catch
                {
                    // If scroll access fails, skip sparkle
                }
            }
            catch (Exception ex)
            {
                Service.Log?.Error($"Error drawing border: {ex.Message}");
            }
        }
        
        private void DrawRoundedCorner(ImDrawListPtr drawList, Vector2 center, float radius, float startAngle, float endAngle, Vector4 color, float thickness)
        {
            var segments = 8;
            var angleStep = (endAngle - startAngle) / segments;
            var prevPoint = center + new Vector2(
                radius * (float)Math.Cos(startAngle * Math.PI / 180f),
                radius * (float)Math.Sin(startAngle * Math.PI / 180f)
            );
            
            for (int i = 1; i <= segments; i++)
            {
                var angle = startAngle + angleStep * i;
                var point = center + new Vector2(
                    radius * (float)Math.Cos(angle * Math.PI / 180f),
                    radius * (float)Math.Sin(angle * Math.PI / 180f)
                );
                drawList.AddLine(prevPoint, point, ImGui.GetColorU32(color), thickness);
                prevPoint = point;
            }
        }
        
        private void DrawSparkle(ImDrawListPtr drawList, Vector2 center, Vector4 color)
        {
            var size = 4f * ImGuiHelpers.GlobalScale;
            var points = new[]
            {
                center + new Vector2(0, -size),
                center + new Vector2(size * 0.5f, -size * 0.5f),
                center + new Vector2(size, 0),
                center + new Vector2(size * 0.5f, size * 0.5f),
                center + new Vector2(0, size),
                center + new Vector2(-size * 0.5f, size * 0.5f),
                center + new Vector2(-size, 0),
                center + new Vector2(-size * 0.5f, -size * 0.5f)
            };
            
            for (int i = 0; i < points.Length; i++)
            {
                var next = (i + 1) % points.Length;
                drawList.AddLine(points[i], points[next], ImGui.GetColorU32(color), 2f);
            }
        }
        
        private void DrawCuteSeparator()
        {
            // Cute separator with gradient
            var separatorStart = ImGui.GetCursorScreenPos();
            var separatorWidth = ImGui.GetContentRegionAvail().X;
            var drawList = ImGui.GetWindowDrawList();
            var gradientStart = CuteTheme.AccentPink;
            var gradientEnd = CuteTheme.AccentLavender;
            for (int i = 0; i < separatorWidth; i++)
            {
                var t = i / separatorWidth;
                var color = new Vector4(
                    gradientStart.X * (1 - t) + gradientEnd.X * t,
                    gradientStart.Y * (1 - t) + gradientEnd.Y * t,
                    gradientStart.Z * (1 - t) + gradientEnd.Z * t,
                    0.5f
                );
                drawList.AddLine(
                    separatorStart + new Vector2(i, 0),
                    separatorStart + new Vector2(i, 1),
                    ImGui.GetColorU32(color),
                    1f
                );
            }
            ImGui.Dummy(new Vector2(0, 2));
        }
        
        public override void Draw()
        {
            try
            {
                // Apply theme colors only to this window using ImRaii scopes
                // This ensures they don't leak to other windows
                using (ImRaii.PushColor(ImGuiCol.WindowBg, CuteTheme.WindowBg))
                using (ImRaii.PushColor(ImGuiCol.ChildBg, CuteTheme.ChildBg))
                using (ImRaii.PushColor(ImGuiCol.FrameBg, CuteTheme.FrameBg))
                using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, CuteTheme.FrameBgHovered))
                using (ImRaii.PushColor(ImGuiCol.FrameBgActive, CuteTheme.FrameBgActive))
                using (ImRaii.PushColor(ImGuiCol.Text, CuteTheme.Text))
                using (ImRaii.PushColor(ImGuiCol.Button, CuteTheme.Button))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, CuteTheme.ButtonHovered))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, CuteTheme.ButtonActive))
                using (ImRaii.PushColor(ImGuiCol.Header, CuteTheme.FrameBgHovered))
                using (ImRaii.PushColor(ImGuiCol.HeaderHovered, CuteTheme.AccentLavender))
                using (ImRaii.PushColor(ImGuiCol.HeaderActive, CuteTheme.AccentPink))
                using (ImRaii.PushColor(ImGuiCol.CheckMark, CuteTheme.AccentMint))
                using (ImRaii.PushColor(ImGuiCol.SliderGrab, CuteTheme.AccentPink))
                using (ImRaii.PushColor(ImGuiCol.SliderGrabActive, CuteTheme.AccentLavender))
                using (ImRaii.PushColor(ImGuiCol.ScrollbarBg, CuteTheme.FrameBg))
                using (ImRaii.PushColor(ImGuiCol.ScrollbarGrab, CuteTheme.AccentLavender))
                using (ImRaii.PushColor(ImGuiCol.ScrollbarGrabHovered, CuteTheme.AccentPink))
                using (ImRaii.PushColor(ImGuiCol.ScrollbarGrabActive, CuteTheme.AccentMint))
                {
                    var windowContentWidth = CkGui.GetWindowContentRegionWidth();
                    var windowContentHeight = ImGui.GetContentRegionAvail().Y;
                    
                    // Calculate responsive heights based on available space
                    var frameHeight = ImGui.GetFrameHeight();
                    var itemSpacing = ImGui.GetStyle().ItemSpacing.Y;
                    var windowPadding = ImGui.GetStyle().WindowPadding.Y;
                    
                    // Minimum heights for each section
                    var minEmoteHeight = frameHeight * 3f;
                    var minDelayHeight = frameHeight * 2f;
                    var minOptionsHeight = frameHeight * 4.5f + itemSpacing * 3f;
                    var minLegendHeight = frameHeight * 1.5f;
                    
                    // Total minimum height needed
                    var totalMinHeight = minEmoteHeight + minDelayHeight + minOptionsHeight + minLegendHeight + itemSpacing * 3f;
                    
                    // Calculate available height for distribution
                    var availableHeight = Math.Max(windowContentHeight, totalMinHeight);
                    var extraHeight = Math.Max(0, availableHeight - totalMinHeight);
                    
                    // Distribute extra height proportionally (emote section gets more space)
                    var emoteHeight = minEmoteHeight + extraHeight * 0.3f;
                    var delayHeight = minDelayHeight + extraHeight * 0.1f;
                    var optionsHeight = minOptionsHeight + extraHeight * 0.4f;
                    var legendHeight = minLegendHeight + extraHeight * 0.2f;
                    
                    // Compact layout - all settings in one main box with responsive sizing
                    using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 4f))
                    using (ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink))
                    using (var mainBox = ImRaii.Child("MainSettingsBox", new Vector2(windowContentWidth, 0), true, ImGuiWindowFlags.None))
                    {
                        if (mainBox)
                        {
                            var innerWidth = ImGui.GetContentRegionAvail().X;
                            
                            // Emote selection section with colored border (responsive, vertically centered)
                            using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 3f))
                            using (ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedPink))
                            using (var emoteBox = ImRaii.Child("EmoteSection", new Vector2(innerWidth, emoteHeight), true, ImGuiWindowFlags.None))
                            {
                                if (emoteBox)
                                {
                                    try
                                    {
                                        var emoteBoxHeight = ImGui.GetContentRegionAvail().Y;
                                        var emoteContentHeight = frameHeight * 2f; // Label + combo
                                        if (emoteBoxHeight > emoteContentHeight)
                                        {
                                            var emoteOffsetY = (emoteBoxHeight - emoteContentHeight) / 2f;
                                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + emoteOffsetY);
                                        }
                                    }
                                    catch
                                    {
                                        // If centering fails, just draw normally
                                    }
                                    
                                    CkGui.ColorText("Select Emote:", ImGuiColors.ParsedPink);
                                    DrawComboEmote("EmoteSelect");
                                }
                            }

                            ImGui.Spacing();

                            // Delay input section with colored border (responsive, inline, vertically centered)
                            using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 3f))
                            using (ImRaii.PushColor(ImGuiCol.Border, ImGuiColors.ParsedGold))
                            using (var delayBox = ImRaii.Child("DelaySection", new Vector2(innerWidth, delayHeight), true, ImGuiWindowFlags.None))
                            {
                                if (delayBox)
                                {
                                    try
                                    {
                                        var delayBoxHeight = ImGui.GetContentRegionAvail().Y;
                                        var delayContentHeight = frameHeight;
                                        if (delayBoxHeight > delayContentHeight)
                                        {
                                            var delayOffsetY = (delayBoxHeight - delayContentHeight) / 2f;
                                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + delayOffsetY);
                                        }
                                    }
                                    catch
                                    {
                                        // If centering fails, just draw normally
                                    }
                                    
                                    CkGui.ColorText("Idle Delay (seconds):", ImGuiColors.ParsedGold);
                                    ImGui.SameLine();
                                    ImGui.SetNextItemWidth(80);
                                    int delay = this.Configuration.IdleDelaySeconds;
                                    if (ImGui.InputInt("##Delay", ref delay, 1, 5))
                                    {
                                        if (delay < 0) delay = 0;
                                        this.Configuration.IdleDelaySeconds = delay;
                                        this.Configuration.Save();
                                    }
                                    ImGui.SameLine();
                                    CkGui.ColorText("(Time before emote)", ImGuiColors.DalamudGrey);
                                    CkGui.AttachToolTip("The number of seconds to wait after becoming idle before the emote is performed");
                                }
                            }

                            ImGui.Spacing();

                            // Options section with colored border (responsive, enough height for all checkboxes, no scrollbar)
                            using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 3f))
                            using (ImRaii.PushColor(ImGuiCol.Border, CuteTheme.AccentLavender))
                            using (var optionsBox = ImRaii.Child("OptionsSection", new Vector2(innerWidth, optionsHeight), true, ImGuiWindowFlags.None))
                            {
                                if (optionsBox)
                                {
                                    try
                                    {
                                        var optionsBoxHeight = ImGui.GetContentRegionAvail().Y;
                                        var optionsContentHeight = frameHeight * 2f + itemSpacing * 2f; // Label + 2 checkboxes with spacing
                                        if (optionsBoxHeight > optionsContentHeight)
                                        {
                                            var optionsOffsetY = (optionsBoxHeight - optionsContentHeight) / 2f;
                                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + optionsOffsetY);
                                        }
                                    }
                                    catch
                                    {
                                        // If centering fails, just draw normally
                                    }
                                    
                                    CkGui.ColorText("Options:", CuteTheme.AccentLavender);
                                    
                                    // Unsheathed checkbox
                                    var unsheathedConfig = this.Configuration.Unsheathed;
                                    if (ImGui.Checkbox("Also perform emote while unsheathed?", ref unsheathedConfig))
                                    {
                                        this.Configuration.Unsheathed = unsheathedConfig;
                                        this.Configuration.Save();
                                    }
                                    CkGui.AttachToolTip("If checked, the plugin will also perform emotes when your weapon is unsheathed.");

                                    // Hide locked emotes toggle
                                    var hideLockedConfig = this.Configuration.HideLockedEmotes;
                                    if (ImGui.Checkbox("Hide locked emotes", ref hideLockedConfig))
                                    {
                                        this.Configuration.HideLockedEmotes = hideLockedConfig;
                                        this.Configuration.Save();
                                        UpdateEmoteList(); // Refresh the list when toggle changes
                                    }
                                    CkGui.AttachToolTip("If checked, locked emotes will not appear in the selection list.");
                                }
                            }

                            ImGui.Spacing();

                            // Legend section with colored border (responsive, centered)
                            using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 3f))
                            using (ImRaii.PushColor(ImGuiCol.Border, CuteTheme.AccentMint))
                            using (var legendBox = ImRaii.Child("LegendSection", new Vector2(innerWidth, legendHeight), true, ImGuiWindowFlags.None))
                            {
                                if (legendBox)
                                {
                                    try
                                    {
                                        var legendBoxHeight = ImGui.GetContentRegionAvail().Y;
                                        var legendContentHeight = frameHeight;
                                        if (legendBoxHeight > legendContentHeight)
                                        {
                                            var legendOffsetY = (legendBoxHeight - legendContentHeight) / 2f;
                                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + legendOffsetY);
                                        }
                                        
                                        // Center the legend text horizontally
                                        var unlockedText = "[✓] = Unlocked";
                                        var lockedText = "[✗] = Locked";
                                        var unlockedSize = ImGui.CalcTextSize(unlockedText);
                                        var lockedSize = ImGui.CalcTextSize(lockedText);
                                        var totalWidth = unlockedSize.X + lockedSize.X + ImGui.GetStyle().ItemSpacing.X;
                                        var legendWidth = ImGui.GetContentRegionAvail().X;
                                        var startX = (legendWidth - totalWidth) / 2;
                                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + startX);
                                    }
                                    catch
                                    {
                                        // If centering fails, just draw normally
                                    }
                                    
                                    CkGui.ColorText("[✓] = Unlocked", CuteTheme.AccentMint);
                                    ImGui.SameLine();
                                    CkGui.ColorText("[✗] = Locked", CuteTheme.AccentPink);
                                }
                            }
                        }
                    }
                }
                
                // Draw animated border at the end when window is fully rendered
                DrawAnimatedBorder();
            }
            catch (Exception ex)
            {
                Service.Log?.Error($"Error in Draw: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Dispose() { }
    }
}
