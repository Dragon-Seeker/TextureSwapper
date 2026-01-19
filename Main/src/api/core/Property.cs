namespace io.wispforest.textureswapper.api.core;

public delegate T Getter<out T>();

public delegate T Setter<T>(T t);

public interface Property {
    public static Property<T> of<T>(Getter<T> getter, Setter<T> setter, Getter<T>? defaultValueGetter = null) {
        return defaultValueGetter == null
            ? new PropertyDelegateImpl<T>(setter, getter)
            : new DefaultedPropertyDelegateImpl<T>(setter, getter, defaultValueGetter);
    }
    
    public static Property<T> of<T>(Getter<T>? defaultValueGetter = null) {
        return defaultValueGetter == null ? new PropertyImpl<T>() : new DefaultedPropertyImpl<T>(defaultValueGetter);
    }
}

public interface Property<T> : Property {
    T get();
    T set(T t);

    public T Invoke() => get();
    
    public T Invoke(T t) => set(t);
}

public interface DefaultedProperty<T> : Property<T> {
    T defaultValue();
}

internal class PropertyDelegateImpl<T>(Setter<T> setter, Getter<T> getter) : Property<T> {
    public T get() => getter();
    public T set(T t) => setter(t);
}

internal sealed class DefaultedPropertyDelegateImpl<T>(Setter<T> setter, Getter<T> getter, Getter<T> defaultValueGetter) 
    : PropertyDelegateImpl<T>(setter, getter), DefaultedProperty<T> {
    
    public T defaultValue() => defaultValueGetter();
}

internal class PropertyImpl<T>() : Property<T> {
    private T? value;
    public T get() => value;
    public T set(T t) => value = t;
}

internal sealed class DefaultedPropertyImpl<T>(Getter<T> defaultValueGetter) : PropertyImpl<T>(), DefaultedProperty<T> {
    public T defaultValue() => defaultValueGetter();
}