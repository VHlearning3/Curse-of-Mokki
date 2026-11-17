using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Player HUD (GDD: items, e.g. lantern; madness has no UI element - see MadnessVignette).
/// Shows the carried items (icon + count) and a short note ("+1 Pills") for a moment whenever
/// the inventory changes, then fades out. Builds its own Screen Space Overlay canvas at runtime.
/// Put on the player next to PlayerInventory (or assign the inventory).
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Tooltip("Leave empty to use PlayerInventory on this object, or the first one in the scene.")]
    [SerializeField] PlayerInventory inventory;

    [Header("Showing")]
    [Tooltip("Seconds the HUD stays fully visible after a change.")]
    [SerializeField, Min(0f)] float showSeconds = 3f;
    [Tooltip("Seconds the fade-out takes.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 1f;
    [Tooltip("Show the \"+1 Pills\" note above the items.")]
    [SerializeField] bool showChangeNote = true;

    [Header("Layout (1920x1080 reference)")]
    [SerializeField] Vector2 margin = new Vector2(40f, 40f);
    [SerializeField, Min(8f)] float iconSize = 64f;
    [SerializeField, Min(0f)] float spacing = 16f;
    [SerializeField, Min(8f)] float fontSize = 26f;
    [SerializeField] Color textColor = new Color(0.9f, 0.88f, 0.82f);
    [Tooltip("Color used for items without an icon (placeholder square).")]
    [SerializeField] Color placeholderIconColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] int sortingOrder = 50;

    [Header("Debug (live)")]
    [SerializeField, Range(0f, 1f)] float currentAlpha;

    CanvasGroup group;
    RectTransform itemRow;
    TextMeshProUGUI noteText;
    readonly List<GameObject> slotObjects = new List<GameObject>();
    float visibleTimer;

    void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory == null) Debug.LogWarning("[PlayerHUD] No PlayerInventory found.", this);
        Build();
        Rebuild();
        SetAlpha(0f);
    }

    void OnEnable()
    {
        if (inventory == null) return;
        inventory.OnItemAdded += HandleAdded;
        inventory.OnItemRemoved += HandleRemoved;
        inventory.OnChanged += HandleChanged;
    }

    void OnDisable()
    {
        if (inventory == null) return;
        inventory.OnItemAdded -= HandleAdded;
        inventory.OnItemRemoved -= HandleRemoved;
        inventory.OnChanged -= HandleChanged;
    }

    void Update()
    {
        Step(Time.deltaTime); // freezes while paused
    }

    void Step(float dt)
    {
        if (group == null) return;
        if (visibleTimer > 0f)
        {
            visibleTimer -= dt;
            SetAlpha(1f);
        }
        else if (currentAlpha > 0f)
        {
            SetAlpha(currentAlpha - dt / fadeSeconds);
        }
    }

    /// <summary>Shows the HUD for showSeconds (e.g. a "check items" key later).</summary>
    public void ShowNow()
    {
        visibleTimer = showSeconds;
        SetAlpha(1f);
    }

    // --- Inventory events ---------------------------------------------------

    void HandleAdded(ItemData item, int amount)
    {
        if (!item.ShowInHUD) return;
        SetNote(item.Stackable ? $"+{amount} {item.DisplayName}" : item.DisplayName);
        ShowNow();
    }

    void HandleRemoved(ItemData item, int amount)
    {
        if (!item.ShowInHUD) return;
        SetNote(item.Stackable ? $"-{amount} {item.DisplayName}" : $"{item.DisplayName} used");
        ShowNow();
    }

    void HandleChanged() => Rebuild();

    // --- UI building ----------------------------------------------------------

    void Build()
    {
        var canvasObject = new GameObject("PlayerHUD (runtime)", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Bottom-left root: note above, item row below
        var root = new GameObject("Root", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvasObject.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
        root.anchoredPosition = margin;
        var vlg = root.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerLeft;
        vlg.spacing = 6f;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
        var fitter = root.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        noteText = CreateText("ChangeNote", root, fontSize, TextAlignmentOptions.BottomLeft);

        itemRow = new GameObject("ItemRow", typeof(RectTransform)).GetComponent<RectTransform>();
        itemRow.SetParent(root, false);
        var hlg = itemRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.LowerLeft;
        hlg.spacing = spacing;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
    }

    void Rebuild()
    {
        if (itemRow == null) return;
        for (int i = 0; i < slotObjects.Count; i++)
        {
            if (Application.isPlaying) Destroy(slotObjects[i]);
            else DestroyImmediate(slotObjects[i]);
        }
        slotObjects.Clear();
        if (inventory == null) return;

        foreach (PlayerInventory.Slot slot in inventory.Slots)
        {
            if (slot.item == null || !slot.item.ShowInHUD) continue;
            slotObjects.Add(CreateSlot(slot.item, slot.count));
        }
    }

    GameObject CreateSlot(ItemData item, int count)
    {
        var slotRect = new GameObject("Slot_" + item.name, typeof(RectTransform)).GetComponent<RectTransform>();
        slotRect.SetParent(itemRow, false);
        var vlg = slotRect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.spacing = 2f;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

        var iconRect = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRect.SetParent(slotRect, false);
        var image = iconRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (item.Icon != null) image.sprite = item.Icon;
        else image.color = placeholderIconColor;
        var le = iconRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredWidth = le.preferredHeight = iconSize;

        // Without an icon the name is shown; stackable items show the count
        string label = item.Icon == null ? item.DisplayName : "";
        if (item.Stackable) label = (label.Length > 0 ? label + " " : "") + "x" + count;
        if (label.Length > 0) CreateText("Label", slotRect, fontSize * 0.8f, TextAlignmentOptions.Center).text = label;

        return slotRect.gameObject;
    }

    TextMeshProUGUI CreateText(string objectName, Transform parent, float size, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = textColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.text = "";
        return text;
    }

    void SetNote(string note)
    {
        if (noteText == null) return;
        noteText.gameObject.SetActive(showChangeNote);
        noteText.text = note;
    }

    void SetAlpha(float a)
    {
        currentAlpha = Mathf.Clamp01(a);
        if (group != null) group.alpha = currentAlpha;
    }
}
