using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using io.wispforest.textureswapper.utils;
using JetBrains.Annotations;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper;

public class TooltipUI : SemiUI {
    private TextMeshProUGUI Text;
    private SemiUI parentUi;
    
    public static TooltipUI? instance { get; private set; }

    public static void setupTooltipUI(SemiUI parent) {
        try {
            var hudObj = parent.transform.parent;

            if (hudObj == null) return;

            var textureTooltip = hudObj.getOrAddChild("TextureSwapperTooltip");
            
            var tooltipUI = textureTooltip.GetComponent<TooltipUI>();
            
            var panel = textureTooltip.getOrAddChild("Panel");
            
            var textObj = textureTooltip.getOrAddChild("Tooltip");
            
            if (tooltipUI == null) {
                tooltipUI = textureTooltip.AddComponent<TooltipUI>();
                
                tooltipUI.textRectTransform = textureTooltip.GetOrAddComponent<RectTransform>();
                tooltipUI.uiText = textObj.AddComponent<TextMeshProUGUI>();

                tooltipUI.showPosition = new Vector2(135, 105);
                tooltipUI.hidePosition = new Vector2(500, 105);

                tooltipUI.doNotDisable = [];
                
                var panelRect = panel.GetOrAddComponent<RectTransform>();
                
                var image = panel.GetOrAddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.5765f);
                
                // 1. Set Anchors to cover the entire parent (0,0 to 1,1)
                panelRect.anchorMin = Vector2.zero; // Bottom-Left
                panelRect.anchorMax = Vector2.one;  // Top-Right

                // 2. Reset the Pivot (optional, but standard for full-stretch panels)
                panelRect.pivot = new Vector2(0.5f, 0.5f);

                // 3. Zero out the offsets (this sets Left, Right, Top, and Bottom to 0)
                panelRect.offsetMin = Vector2.zero; // Distance from Bottom-Left
                panelRect.offsetMax = Vector2.zero; // Distance from Top-Right
    
                // Note: When anchors are stretched, sizeDelta controls the margins, not the width/height!
                // Setting it to zero effectively means "0 margin from edges".
                
                if (instance != null) Object.Destroy(instance);
            }
            
            tooltipUI.setParentUI(parent);

            instance = tooltipUI;
        } catch (Exception e) {
            Plugin.Logger.LogError("Unable to handle creating TooltipUI due to an error");
            Plugin.Logger.LogError(e);
        }
    }

    private void setParentUI(SemiUI parent) {
        parentUi = parent;
            
        var parentText = parent.uiText;

        if (parentText != null) {
            Text.font = parentText.font;
            Text.fontMaterial = parentText.fontMaterial;
            Text.fontMaterials = parentText.fontMaterials;
            Text.fontStyle = parentText.fontStyle;
            Text.fontSharedMaterial = parentText.fontSharedMaterial;
            Text.fontSharedMaterials = parentText.fontSharedMaterials;
            Text.material = parentText.material;
        }
    }

    private string dumpedText = "None";
    
    private RectTransform baseRect;
    private RectTransform textRect;
    private RectTransform panelRect;
    
    private float xAxis, yAxis;
    private float width, height;
    private float xPivot, yPivot;
    
    private float fontSize;
    
    protected override void Start() {
        instance = this;
        
        base.Start();
        
        Text = uiText;
        
        baseRect = textRectTransform as RectTransform; //uiText.gameObject.GetComponent<RectTransform>();
        textRect = Text.gameObject.GetComponent<RectTransform>();
        panelRect = baseRect.gameObject.getOrAddChild("Panel").GetComponent<RectTransform>();
        
        Text.text = "";
        
        //textRectTransform.anchoredPosition = Vector2.zero;
        //textRectTransform.anchoredPosition = new Vector2(200f, 0f);
        
        setupTransform(textRect, new Vector2(300, 300));
        setupTransform(baseRect, new Vector2(300, 300));
        
        Text.fontSize = 6;
        Text.enableAutoSizing = false;
        // Text.enableAutoSizing = true;
        // Text.fontSizeMin = 12;
        // Text.fontSizeMax = 5;
        
        Text.enableWordWrapping = false;
        Text.richText = false;

        xAxis = textRect.anchoredPosition.x;
        yAxis = textRect.anchoredPosition.y;
        
        width = textRect.sizeDelta.x;
        height = textRect.sizeDelta.y;
        
        xPivot = textRect.pivot.x;
        yPivot = textRect.pivot.y;

        fontSize = Text.fontSize;
    }

    private void setSize(Vector2 size) {
        size = new Vector2(Math.Max(size.x, 200), Math.Max(size.y, 0));
        textRect.sizeDelta = size;
        baseRect.sizeDelta = size;
    }

    private static void setupTransform(RectTransform rectTransform, Vector2 size) {
        //rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        //rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        //rectTransform.pivot = new Vector2(0.5f, 0.5f);

        rectTransform.anchoredPosition = new Vector2(0, 0);
        
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);

        rectTransform.sizeDelta = size;
    }

    private SortedDictionary<TooltipKey, string> messageParts = new SortedDictionary<TooltipKey, string>();
    
    private string? message;
    private bool hasMessageChanged = false;

    public void clearMessage(ICollection<TooltipKey> keys) {
        if (messageParts.isEmpty()) return;
        foreach (var key in keys) messageParts.Remove(key);
        hasMessageChanged = true;
    }
    
    public void clearMessage() {
        if (messageParts.isEmpty()) return;
        messageParts.Clear();
        hasMessageChanged = true;
    }

    public void removeMessage(TooltipKey key) => setMessage(key, null);
    
    public void setMessage(TooltipKey key, string? message) {
        if (!messageParts.ContainsKey(key)) {
            if (message == null) return;
        } else {
            var currentMessage = messageParts[key];
            
            if (currentMessage == message) {
                if (currentMessage == null) messageParts.Remove(key);
                
                return;
            }
        }

        if (message == null) {
            messageParts.Remove(key);
        } else {
            messageParts[key] = message!;
        }
        
        hasMessageChanged = true;
    }

    private enum ChangingState {
        HIDDEN,
        SHOWING,
        SHOWN
    }
    
    private ChangingState state = ChangingState.SHOWN;

    public ICollection<TooltipKey> getInvalidKeys() {
        if (messageParts.isEmpty()) return [];
        
        var currentKeys = messageParts.Keys;
        
        var manager = RunManager.instance;
        if (manager == null) return currentKeys;

        var currentLevel = manager.levelCurrent;
        if (currentLevel == null) return currentKeys;

        var invalidKeys = new List<TooltipKey>();
        
        foreach (var key in currentKeys) {
            if (!key.predicate(manager, currentLevel)) {
                invalidKeys.Add(key);
            }
        }

        return invalidKeys;
    }
    
    protected override void Update() {
        if (messageParts.isEmpty()) {
            if (state != ChangingState.HIDDEN) {
                Hide();
                state = ChangingState.HIDDEN;
            }
        } else if (state == ChangingState.HIDDEN) {
            Show();
            state = ChangingState.SHOWING;
        } else if (!isHidden) {
            state = ChangingState.SHOWN;
        }

        try {
            base.Update();
        } catch (Exception _) { }

        var invalidKeys = getInvalidKeys();
        
        if (invalidKeys.isNotEmpty()) {
            clearMessage(invalidKeys);
        } else if (hasMessageChanged) {
            string msg;

            if (messageParts.isNotEmpty()) {
                msg = string.Join("\n", messageParts.Select((pair) => {
                    pair.Deconstruct(out var key, out var value);

                    return string.Join("\n", key.name, value!);
                }));
            } else {
                msg = "";
            }
            
            Text.text = msg;
            hasMessageChanged = false;
        }
        
        setSize(new Vector2(Text.preferredWidth, Text.preferredHeight));
    }
    
    // void OnGUI() {
    //     //The Labels show what the Sliders represent
    //     GUI.Label(new Rect(0, 18, 150, 80), "Anchor Pos X : ");
    //     GUI.Label(new Rect(300, 18, 150, 80), "Anchor Pos Y : ");
    //     xAxis = GUI.HorizontalSlider(new Rect(150, 20, 100, 20), xAxis, -500.0f, 500.0f);
    //     yAxis = GUI.HorizontalSlider(new Rect(450, 20, 100, 20), yAxis, -500.0f, 500.0f);
    //     
    //     GUI.Label(new Rect(0, 38, 150, 80), "Width : ");
    //     GUI.Label(new Rect(300, 38, 150, 80), "Height : ");
    //     width = GUI.HorizontalSlider(new Rect(150, 40, 100, 20), width, 0, 500.0f);
    //     height = GUI.HorizontalSlider(new Rect(450, 40, 100, 20), height, 0, 500.0f);
    //     
    //     GUI.Label(new Rect(0, 58, 150, 80), "Pivot Pos X : ");
    //     GUI.Label(new Rect(300, 58, 150, 80), "Pivot Pos Y : ");
    //     xPivot = GUI.HorizontalSlider(new Rect(150, 60, 100, 20), xPivot, -1.0f, 1.0f);
    //     yPivot = GUI.HorizontalSlider(new Rect(450, 60, 100, 20), yPivot, -1.0f, 1.0f);
    //     
    //     GUI.Label(new Rect(300, 78, 150, 80), "Font Size : ");
    //     fontSize = GUI.HorizontalSlider(new Rect(450, 80, 100, 20), fontSize, 1, 46);
    //
    //     //Detect a change in the GUI Slider
    //     // if (GUI.changed) {
    //     //     //Change the RectTransform's anchored positions depending on the Slider values
    //     //     rectTransform.anchoredPosition = new Vector2(xAxis, yAxis);
    //     //     rectTransform.sizeDelta = new Vector2(width, height);
    //     //     rectTransform.pivot = new Vector2(xPivot, yPivot);
    //     //     Text.fontSize = fontSize;
    //     // }
    // }

    public static readonly TooltipKey BASIC = new (0, "Basic");
    public static readonly TooltipKey DEBUG = new (100, "Debug Info", true, (_, _) => true);
}

public class TooltipKey(int index, string name, bool showName = false, LevelPredicate? predicate = null) : IComparable<TooltipKey> {
    public int index { get; } = index;
    public string name { get; } = name;
    public readonly bool showName = showName;
    public readonly LevelPredicate predicate = predicate ?? LevelUtils.GENERAL_VALID_PREDICATE;
    
    public int CompareTo(TooltipKey? other) {
        if (ReferenceEquals(this, other)) return 0;
        return other is not null ? index.CompareTo(other.index) : 1;
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is TooltipKey other && index == other.index && name == other.name;
    }

    public override int GetHashCode() {
        unchecked {
            return (index * 397) ^ name.GetHashCode();
        }
    }
}