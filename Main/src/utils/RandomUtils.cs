using System;
using Random = UnityEngine.Random;

namespace io.wispforest.textureswapper.utils;

public class RandomUtils {
    public static T preserveState<T>(Func<T> func, int? hash = null) {
        Random.State? prevState = Random.state;

        if (hash is not null) {
            Random.InitState((int)hash);
        }

        var t = func();
        
        Random.state = (Random.State) prevState;
        
        return t;
    }
}