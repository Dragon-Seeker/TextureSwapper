using BepInEx;
using BepInEx.Configuration;

namespace io.wispforest.textureswapper.api.config;

public class DummyConfigFile : ConfigFile {
    public DummyConfigFile(string name, BepInPlugin ownerMetadata) : base(Utility.CombinePaths(Paths.ConfigPath, $"{ownerMetadata.GUID}_{name}.cfg"), false, ownerMetadata) {
        this.SaveOnConfigSet = false;
    }
}