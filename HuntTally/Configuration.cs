using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace HuntTally;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public Dictionary<string, Dictionary<uint, int>> KillCounts { get; set; } = [];

    public bool IsMainWindowOpen { get; set; } = false;
    public bool SameAreaOnly { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
