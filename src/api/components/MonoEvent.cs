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
    }

    private void Reset() {
        onResetCallback?.Invoke(this.gameObject);
    }

    private void Start() {
        onStartCallback?.Invoke(this.gameObject);
    }

    private void Update() {
        onUpdateCallback?.Invoke(this.gameObject);
    }

    private void OnDestroy() {
        onDestoryCallback?.Invoke(this.gameObject);
    }
}