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
            ImGui.Text(playerState.ClassJob.Value.Abbreviation.ToString());

            ImGui.SameLine();
            ImGui.Text($" [Level {playerState.Level}]");

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

            ImGui.SameLine();

            if (ImGui.Button("リセット"))
            {
                plugin.Configuration.KillCounts.Clear();
                plugin.Configuration.Save();
            }

            ImGui.SameLine();
            var sameAreaOnly = plugin.Configuration.SameAreaOnly;
            if (ImGui.Checkbox("現在地のみ", ref sameAreaOnly))
            {
                plugin.Configuration.SameAreaOnly = sameAreaOnly;
                plugin.Configuration.Save();
            }

            ImGui.Separator();

            var currentTerritory = Plugin.ClientState.TerritoryType;

            var rows = plugin.Configuration.KillCounts
                .Where(kv => string.IsNullOrEmpty(filterText)
                    || kv.Key.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .Select(kv => new
                {
                    MobName = kv.Key,
                    Count = plugin.Configuration.SameAreaOnly
                        ? kv.Value.GetValueOrDefault(currentTerritory, 0)
                        : kv.Value.Values.Sum()
                })
                .Where(x => plugin.Configuration.SameAreaOnly || x.Count > 0)
                .OrderByDescending(x => x.Count);

            if (ImGui.BeginTable("kills", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("モブ名");
                ImGui.TableSetupColumn("撃破数", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();

                foreach (var row in rows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.MobName);
                    ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Count.ToString());
                }

                ImGui.EndTable();
            }
        }
    }
}
