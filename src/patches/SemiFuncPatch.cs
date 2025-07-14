using HarmonyLib;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(SemiFunc))]
public class SemiFuncPatch {
    [HarmonyPatch(nameof(SemiFunc.MenuActionSingleplayerGame))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void singlePlayerStart() {
        Plugin.logIfDebugging(() => "Starting SinglePlayer!");
        
        Plugin.Instance.loadQueries();
    }
    
    [HarmonyPatch(nameof(SemiFunc.MenuActionHostGame))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void multiplayerStart() {
        Plugin.logIfDebugging(() => "Starting MultiPlayer!");

        Plugin.Instance.loadQueries();
    }
    
    [HarmonyPatch(nameof(SemiFunc.OnSceneSwitch))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void onSceneSwitch(bool _gameOver, bool _leaveGame) {
        var levelName = RunManager.instance.levelCurrent.name;
        
        Plugin.logIfDebugging(() => $"Switching scene! Level: {levelName}");
        
        Plugin.Instance.loadQueries(levelName);
    }
}