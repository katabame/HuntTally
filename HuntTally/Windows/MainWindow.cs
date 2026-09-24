using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace HuntTally.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string filterText = string.Empty;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin) : base("HuntTally##MainWindow")
    {
        this.plugin = plugin;
        Size = new Vector2(320, 400);
        SizeCondition = ImGuiCond.FirstUseEver;

        IsOpen = plugin.Configuration.IsMainWindowOpen;
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        plugin.Configuration.IsMainWindowOpen = true;
        plugin.Configuration.Save();
        base.OnOpen();
    }

    public override void OnClose()
    {
        plugin.Configuration.IsMainWindowOpen = false;
        plugin.Configuration.Save();
        base.OnClose();
    }

    public override void Draw()
    {
        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
        using var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true);
        // Check if this child is drawing
        if (child.Success)
        {
            // Example for other services that Dalamud provides.
            // PlayerState provides a wrapper filled with information about the player character.

            var playerState = Plugin.PlayerState;
            if (!playerState.IsLoaded)
            {
                ImGui.Text("プレイヤーがログインしていません");
                return;
            }

            if (!playerState.ClassJob.IsValid)
            {
                ImGui.Text("現在のジョブは有効ではありません");
                return;
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text($"現在のジョブ:");

            // Scaling hardcoded pixel values is important, as otherwise users with HUD scales above or below 100%
            // won't be able to see everything.
            ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);

            // Get the icon id from a known offset + the class jobs id
            var jobIconId = 62100 + playerState.ClassJob.RowId;
            var iconTexture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(jobIconId)).GetWrapOrEmpty();
            ImGui.Image(iconTexture.Handle, new Vector2(28, 28) * ImGuiHelpers.GlobalScale);

            ImGui.SameLine();

            // If you want to see the Macro representation of this SeString use `.ToMacroString()`
            // More info about SeStrings: https://dalamud.dev/plugin-development/sestring/
            ImGui.Text(playerState.ClassJob.Value.Name.ToString());

            ImGui.SameLine();
            ImGui.Text($"[レベル {playerState.Level}]");

            // Example for querying Lumina, getting the name of our current area.
            var territoryId = Plugin.ClientState.TerritoryType;
            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
            {
                ImGui.Text($"現在地:");
                ImGui.SameLine(120 * ImGuiHelpers.GlobalScale);
                ImGui.Text(territoryRow.PlaceName.Value.Name.ToString());
            }
            else
            {
                ImGui.Text("無効な場所です");
            }

            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##filter", "モブ名で絞り込み", ref filterText, 64);


            var currentTerritory = Plugin.ClientState.TerritoryType;
            var sameAreaOnly = plugin.Configuration.SameAreaOnly;

            var visibleMobNames = plugin.Configuration.KillCounts
                .Where(kv => string.IsNullOrEmpty(filterText) || kv.Key.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .Where(kv => !sameAreaOnly || kv.Value.GetValueOrDefault(currentTerritory, 0) > 0)
                .Select(kv => kv.Key)
                .ToList();

            ImGui.SameLine();

            if (ImGui.Checkbox("現在地のみ", ref sameAreaOnly))
            {
                plugin.Configuration.SameAreaOnly = sameAreaOnly;
                plugin.Configuration.Save();
            }

            //ImGui.SameLine();

            if (ImGui.Button("表示中の全てのモブをリセット"))
            {
                foreach (var mobName in visibleMobNames)
                {
                    ResetMobKills(mobName, sameAreaOnly, currentTerritory);
                }

                plugin.Configuration.Save();
            }

            ImGui.Separator();

            var rows = visibleMobNames
                .Select(name => new
                {
                    MobName = name,
                    Count = sameAreaOnly
                        ? plugin.Configuration.KillCounts.GetValueOrDefault(name)?.GetValueOrDefault(currentTerritory, 0) ?? 0
                        : plugin.Configuration.KillCounts.GetValueOrDefault(name)?.Values.Sum() ?? 0,
                })
                .OrderByDescending(x => x.Count);

            if (ImGui.BeginTable("kills", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("モブ名");
                ImGui.TableSetupColumn("撃破数", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();

                foreach (var row in rows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.MobName);

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        if (plugin.Configuration.KillCounts.TryGetValue(row.MobName, out var areaCounts))
                        {
                            foreach (var kv in areaCounts.OrderByDescending(x => x.Value))
                            {
                                ImGui.TextUnformatted($"{plugin.GetTerritoryName(kv.Key)}: {kv.Value}");
                            }
                        }
                        ImGui.EndTooltip();
                    }

                    if (ImGui.BeginPopupContextItem($"ctx_{row.MobName}"))
                    {
                        var label = sameAreaOnly ? $"「{row.MobName}」の現在地での討伐数をリセット" : $"「{row.MobName}」の全ての討伐数をリセット";
                        if (ImGui.MenuItem(label))
                        {
                            ResetMobKills(row.MobName, sameAreaOnly, currentTerritory);
                            plugin.Configuration.Save();
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.Count.ToString());
                }

                ImGui.EndTable();
            }
        }
    }

    private void ResetMobKills(string mobName, bool sameAreaOnly, uint currentTerritory)
    {
        if (sameAreaOnly)
        {
            if (plugin.Configuration.KillCounts.TryGetValue(mobName, out var territories))
            {
                territories.Remove(currentTerritory);
                if (territories.Count == 0)
                {
                    plugin.Configuration.KillCounts.Remove(mobName);
                }
            }
        }
        else
        {
            plugin.Configuration.KillCounts.Remove(mobName);
        }
    }

}
