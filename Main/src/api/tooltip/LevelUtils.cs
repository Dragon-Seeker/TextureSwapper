using System;
using System.Linq;

namespace io.wispforest.textureswapper;

public delegate bool LevelPredicate(RunManager manager, Level currentLevel);

public delegate Level LevelGetter(RunManager manager);

public enum Operation {
    ANY,
    NONE
}

public static class LevelUtils {
    public static readonly LevelGetter[] INVALID_LEVELS = [
            m => m.levelLobby, 
            m => m.levelLobbyMenu, 
            m => m.levelMainMenu, 
            m => m.levelSplashScreen, 
            m => m.levelTutorial
    ];

    public static readonly LevelPredicate GENERAL_VALID_PREDICATE = of(Operation.NONE, INVALID_LEVELS);
    
    public static LevelPredicate and(this LevelPredicate predicate1, LevelPredicate predicate2) => (manager, level) => predicate1(manager, level) && predicate2(manager, level);
    
    public static LevelPredicate or(this LevelPredicate predicate1, LevelPredicate predicate2) => (manager, level) => predicate1(manager, level) || predicate2(manager, level);

    public static bool isValid(this LevelPredicate predicate) {
        var manager = RunManager.instance;
        if (manager == null) return false;

        var currentLevel = manager.levelCurrent;
        if (currentLevel == null) return false;

        return predicate(manager, currentLevel);
    }

    public static bool isCurrentLevel(Level level1, Level level2) {
        return level1 != null && level2 != null && level1 == level2;
    }
    
    public static LevelPredicate of(Operation operation, params LevelGetter[] getters) {
        return operation switch {
                Operation.ANY => (manager, level) => getters.Select(getter => getter(manager)).Any(level1 => isCurrentLevel(level1, level)),
                Operation.NONE => (manager, level) => getters.Select(getter => getter(manager)).Any(level1 => !isCurrentLevel(level1, level)),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Invalid operation based when creating level predicate")
        };
    }
    
    public static LevelGetter ofName(string name) => of(s => s == name);

    public static LevelGetter of(Predicate<string> namePredicate) => manager => manager.levels.Find(level => namePredicate(level.name));
}