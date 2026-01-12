using System;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components;

public class MonoEvent : MonoBehaviour {
    public event Action<GameObject>? onAwakeCallback;
    public event Action<GameObject>? onResetCallback;
    public event Action<GameObject>? onStartCallback;
    public event Action<GameObject>? onUpdateCallback;
    public event Action<GameObject>? onDestoryCallback;
    
    private void Awake() {
        onAwakeCallback?.Invoke(this.gameObject);
        onAwake();
    }

    private void Reset() {
        onResetCallback?.Invoke(this.gameObject);
        onReset();
    }

    private void Start() {
        onStartCallback?.Invoke(this.gameObject);
        onStart();
    }

    private void Update() {
        onUpdateCallback?.Invoke(this.gameObject);
        onUpdate();
    }

    private void OnDestroy() {
        onDestoryCallback?.Invoke(this.gameObject);
    }
    
    protected virtual void onAwake() { }

    protected virtual void onReset() { }

    protected virtual void onStart() { }

    protected virtual void onUpdate() { }
}