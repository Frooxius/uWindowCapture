﻿using UnityEngine;
using System.Collections.Generic;

namespace uWindowCapture
{

public enum WindowTextureType
{
    Window = 0,
    Desktop = 1,
    Child = 2,
}

public enum WindowTextureCaptureTiming
{
    EveryFrame = 0,
    OnlyWhenVisible = 1,
    Manual = 2,
}

public enum WindowTextureScaleControlType
{
    BaseScale = 0,
    FixedWidth = 1,
    FixedHeight = 2,
    Manual = 3,
}

public class UwcWindowTexture : MonoBehaviour
{
    bool shouldUpdateWindow_ = true;

    [SerializeField]
    WindowTextureType type_ = WindowTextureType.Window;
    public WindowTextureType type
    {
        get
        {
            return type_;
        }
        set
        {
            shouldUpdateWindow_ = true;
            type_ = value;
        }
    }

    [SerializeField]
    bool altTabWindow_ = false;
    public bool altTabWindow
    {
        get 
        { 
            return altTabWindow_; 
        }
        set
        {
            shouldUpdateWindow_ = true;
            altTabWindow_ = value;
        }
    }

    [SerializeField]
    bool createChildWindows_ = true;
    public bool createChildWindows
    {
        get 
        { 
            return createChildWindows_;
        }
        set
        {
            createChildWindows_ = value;

            var manager = GetComponent<UwcWindowTextureChildrenManager>();
            if (createChildWindows_) {
                if (!manager) {
                    gameObject.AddComponent<UwcWindowTextureChildrenManager>();
                }
            } else {
                if (manager) {
                    Destroy(manager);
                }
            }
        }
    }

    public GameObject childWindowPrefab;
    public float childWindowZDistance = 0.02f;

    [SerializeField]
    string partialWindowTitle_;
    public string partialWindowTitle 
    {
        get 
        {
            return partialWindowTitle_;
        }
        set 
        {
            shouldUpdateWindow_ = true;
            partialWindowTitle_ = value;
        }
    }

    [SerializeField]
    int desktopIndex_ = 0;
    public int desktopIndex
    {
        get
        {
            return desktopIndex_;
        }
        set
        {
            shouldUpdateWindow_ = true;
            desktopIndex_ = Mathf.Clamp(value, 0, UwcManager.desktopCount - 1);
        }
    }

    public CaptureMode captureMode = CaptureMode.PrintWindow;
    public CapturePriority capturePriority = CapturePriority.Auto;
    public WindowTextureCaptureTiming captureRequestTiming = WindowTextureCaptureTiming.OnlyWhenVisible;
    public int captureFrameRate = 30;
    public bool drawCursor = true;

    public WindowTextureScaleControlType scaleControlType = WindowTextureScaleControlType.BaseScale;
    public float scalePer1000Pixel = 1f;

    static HashSet<UwcWindowTexture> list_ = new HashSet<UwcWindowTexture>();
    public static HashSet<UwcWindowTexture> list
    {
        get { return list_; }
    }

    UwcWindow window_;
    public UwcWindow window 
    { 
        get 
        {
            return window_;
        }
        set 
        {
            if (window_ != null) {
                window_.onCaptured.RemoveListener(OnCaptured);
            }

            onWindowChanged_.Invoke(value, window_);
            window_ = value;

            if (window_ != null) {
                shouldUpdateWindow_ = false;
                captureMode = window_.captureMode;
                window_.onCaptured.AddListener(OnCaptured);
                window_.RequestCapture(CapturePriority.High);
            }
        }
    }

    public UwcWindowTextureManager manager { get; set; }
    public UwcWindowTexture parent { get; set; }

    UwcWindowChangeEvent onWindowChanged_ = new UwcWindowChangeEvent();
    public UwcWindowChangeEvent onWindowChanged
    {
        get { return onWindowChanged_; }
    }

    float basePixel
    {
        get { return 1000f / scalePer1000Pixel; }
    }

    public bool isValid
    {
        get
        {
            return window != null && window.isValid;
        }
    }

    Material material_;
    Renderer renderer_;
    MeshFilter meshFilter_;
    Collider collider_;
    float captureTimer_ = 0f;
    bool hasBeenCaptured_ = false;

    void Awake()
    {
        renderer_ = GetComponent<Renderer>();
        material_ = renderer_.material; // clone
        meshFilter_ = GetComponent<MeshFilter>();
        collider_ = GetComponent<Collider>();

        list_.Add(this);
    }

    void OnDestroy()
    {
        list_.Remove(this);
    }

    void Update()
    {
        UpdateTargetWindow();

        if (!isValid) {
            material_.mainTexture = null;
            return;
        }

        UpdateTexture();
        UpdateRenderer();
        UpdateScale();

        if (captureRequestTiming == WindowTextureCaptureTiming.EveryFrame) {
            RequestCapture();
        }

        captureTimer_ += Time.deltaTime;

        UpdateBasicComponents();
    }

    void OnWillRenderObject()
    {
        if (captureRequestTiming == WindowTextureCaptureTiming.OnlyWhenVisible) {
            RequestCapture();
        }
    }

    void UpdateTexture()
    {
        if (!isValid) return;

        window.cursorDraw = drawCursor;

        if (material_.mainTexture != window.texture) {
            material_.mainTexture = window.texture;
        }
    }

    void UpdateRenderer()
    {
        if (hasBeenCaptured_) {
            renderer_.enabled = !window.isIconic && window.isVisible;
        }
    }

    void UpdateScale()
    {
        if (!isValid || window.isChild) return;

        var scale = transform.localScale;

        switch (scaleControlType) {
            case WindowTextureScaleControlType.BaseScale: {
                var extents = meshFilter_.sharedMesh.bounds.extents;
                var meshWidth = extents.x * 2f;
                var meshHeight = extents.y * 2f;
                var baseHeight = meshHeight * basePixel;
                var baseWidth = meshWidth * basePixel;
                scale.x = window.width / baseWidth;
                scale.y = window.height / baseHeight;
                break;
            }
            case WindowTextureScaleControlType.FixedWidth: {
                scale.y = transform.localScale.x * window.height / window.width;
                break;
            }
            case WindowTextureScaleControlType.FixedHeight: {
                scale.x = transform.localScale.y * window.width / window.height;
                break;
            }
            case WindowTextureScaleControlType.Manual: {
                break;
            }
        }

        transform.localScale = scale;
    }

    void UpdateTargetWindow()
    {
        if (!shouldUpdateWindow_) return;

        switch (type)
        {
            case WindowTextureType.Window:
                if (altTabWindow) {
                    window = UwcManager.Find(window => window.isAltTabWindow && window.title.IndexOf(partialWindowTitle) != -1);
                } else {
                    window = UwcManager.Find(partialWindowTitle);
                }
                break;
            case WindowTextureType.Desktop:
                window = UwcManager.FindDesktop(desktopIndex);
                break;
            case WindowTextureType.Child:
                break;
        }
    }

    void UpdateBasicComponents()
    {
        if (renderer_) renderer_.enabled = isValid;
        if (collider_) collider_.enabled = isValid;
    }

    void OnCaptured()
    {
        hasBeenCaptured_ = true;
    }

    public void RequestCapture()
    {
        if (!isValid) return;

        window.captureMode = captureMode;

        float T = 1f / captureFrameRate;
        if (captureTimer_ < T) return;

        while (captureTimer_  > T) {
            captureTimer_ -= T;
        }

        var priority = capturePriority;
        if (priority == CapturePriority.Auto) {
            priority = CapturePriority.Low;
            if (window == UwcManager.cursorWindow) {
                priority = CapturePriority.High;
            } else if (window.zOrder < UwcSetting.MiddlePriorityMaxZ) {
                priority = CapturePriority.Middle;
            }
        }

        window.RequestCapture(priority);
    }
}

}