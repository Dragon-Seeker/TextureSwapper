namespace io.wispforest.textureswapper.api;

public class LevelEvents {
    public delegate void OnLevelChange(int completedLevels, string levelName);

    public static event OnLevelChange ON_CHANGE;

    private static int lastCompletedLevels = 0;
    
    public static void onLevelChange(int completedLevels, string levelName) {
        if (lastCompletedLevels == completedLevels) return;

        lastCompletedLevels = completedLevels;
        
        ON_CHANGE?.Invoke(completedLevels, levelName);
    }
}