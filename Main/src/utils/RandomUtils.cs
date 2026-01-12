using System;
using Random = UnityEngine.Random;

namespace io.wispforest.textureswapper.utils;

public class RandomUtils {
    public static T useHash<T>(Func<T> func, int? hash = null) {
        Random.State? prevState = null;

        if (hash is not null) {
            prevState = Random.state;
            Random.InitState((int)hash);
        }

        var t = func();
        
        if (prevState is not null) {
            Random.state = (Random.State) prevState;
        }

        return t;
    }
}