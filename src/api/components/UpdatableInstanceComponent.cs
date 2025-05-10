using System;
using System.Collections.Generic;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components;

public class UpdatableInstanceComponent : MonoBehaviour {

    private UpdatableInstance _instance = UpdatableInstance.EMPTY;

    public UpdatableInstanceComponent createAndSetInstance(float maxTime, IList<float> deltaBetweenFrames, Func<int, bool> onNextFrame) {
        _instance = new UpdatableInstance(maxTime, deltaBetweenFrames, onNextFrame);

        return this;
    }

    public void Reset() {
        _instance = UpdatableInstance.EMPTY;
    }

    public void Update() {
        if (_instance == UpdatableInstance.EMPTY) return;
        
        _instance.Update();
    }
}

public class UpdatableInstance {
    public static readonly UpdatableInstance EMPTY = new (1, new List<float>(), i => false);
    
    private readonly float _maxTime = 1;
    private readonly IList<float> _deltaBetweenFrames;

    private readonly Func<int, bool> _onNextFrame;

    public UpdatableInstance(float maxTime, IList<float> deltaBetweenFrames, Func<int, bool> onNextFrame) {
        this._maxTime = maxTime;
        this._deltaBetweenFrames = deltaBetweenFrames;
        this._onNextFrame = onNextFrame;
    }
    
    private float _updateSpeed = 1.0f; // Speed of the update
    private float _currentTime = 0.0f;

    private float _lastActionTime = 0;
    private int _currentIndex = 0;
    
    public void Update() {
        // Increment the value based on time.deltaTime
        _currentTime += _updateSpeed * Time.deltaTime;

        //Plugin.Logger.LogWarning($"Current: {_currentValue}, Last: {_lastActionTime}");
        
        if (_currentIndex == 0) {
            _onNextFrame(_currentIndex);
            
            _currentIndex++;
            _lastActionTime = _currentTime;
            
            return;
        }

        var reset = _currentIndex >= _deltaBetweenFrames.Count;

        if (!reset) {
            var delta = _deltaBetweenFrames[_currentIndex];

            if (_currentTime - _lastActionTime >= delta) {
                _currentIndex++;
                _lastActionTime = _currentTime;
                
                reset = _onNextFrame(_currentIndex) || _currentIndex >= _deltaBetweenFrames.Count;
            }
        }

        if (reset) {
            _currentIndex = 0;
            _lastActionTime = 0;
            _currentTime = 0;
        }
    }
}