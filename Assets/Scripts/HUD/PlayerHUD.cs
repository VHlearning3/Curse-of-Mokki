using UnityEngine;

/// <summary>
/// Player HUD (GDD: items). After a change the items and a "+1 Pills" note show for a moment, then fade.
/// Items with Always Show While Carried (keys) stay visible. Restyle it in the HUD_Player prefab.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Parts (set in the prefab)")]
    [Tooltip("The part that fades out: the note and the item row.")]
    [SerializeField] CanvasGroup fadingGroup;
    [SerializeField] TMPro.TextMeshProUGUI changeNote;
    [Tooltip("Items that come and go (lantern, pills).")]
    [SerializeField] Transform itemRow;
    [Tooltip("Items with Always Show While Carried (keys). Never fades.")]
    [SerializeField] Transform alwaysVisibleRow;
    [Tooltip("The look of one item: the HUD_ItemSlot prefab.")]
    [SerializeField] HUDItemSlot slotPrefab;

    [Tooltip("Leave empty to use the first PlayerInventory in the scene.")]
    [SerializeField] PlayerInventory inventory;

    [Header("Showing")]
    [Tooltip("Seconds the HUD stays fully visible after a change.")]
    [SerializeField, Min(0f)] float showSeconds = 3f;
    [Tooltip("Seconds the fade-out takes.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 1f;
    [Tooltip("Show the \"+1 Pills\" note.")]
    [SerializeField] bool showChangeNote = true;

    float visibleTimer;

    void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory == null) Debug.LogWarning("[PlayerHUD] No PlayerInventory found.", this);
        ClearRow(itemRow); // example slots left in the prefab for editing
        ClearRow(alwaysVisibleRow);
        SetAlpha(0f);
    }

    void Start() => Rebuild(); // after PlayerInventory has added its start items

    void OnEnable()
    {
        if (inventory == null) return;
        inventory.OnItemAdded += HandleAdded;
        inventory.OnItemRemoved += HandleRemoved;
        inventory.OnChanged += Rebuild;
    }

    void OnDisable()
    {
        if (inventory == null) return;
        inventory.OnItemAdded -= HandleAdded;
        inventory.OnItemRemoved -= HandleRemoved;
        inventory.OnChanged -= Rebuild;
    }

    void Update() => Step(Time.deltaTime); // freezes while paused

    void Step(float dt)
    {
        if (visibleTimer > 0f)
        {
            visibleTimer -= dt;
            SetAlpha(1f);
        }
        else if (fadingGroup != null && fadingGroup.alpha > 0f)
        {
            SetAlpha(fadingGroup.alpha - dt / fadeSeconds);
        }
    }

    /// <summary>Shows the HUD for showSeconds.</summary>
    public void ShowNow()
    {
        visibleTimer = showSeconds;
        SetAlpha(1f);
    }

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

    void Rebuild()
    {
        ClearRow(itemRow);
        ClearRow(alwaysVisibleRow);
        if (inventory == null || slotPrefab == null) return;

        foreach (PlayerInventory.Slot slot in inventory.Slots)
        {
            if (slot.item == null || !slot.item.ShowInHUD) continue;
            Transform row = slot.item.AlwaysShowWhileCarried ? alwaysVisibleRow : itemRow;
            if (row != null) Instantiate(slotPrefab, row).Show(slot.item, slot.count);
        }
    }

    static void ClearRow(Transform row)
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            GameObject child = row.GetChild(i).gameObject;
            child.SetActive(false); // gone from the layout right away
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    void SetNote(string note)
    {
        if (changeNote == null) return;
        changeNote.gameObject.SetActive(showChangeNote);
        changeNote.text = note;
    }

    void SetAlpha(float a)
    {
        if (fadingGroup != null) fadingGroup.alpha = Mathf.Clamp01(a);
    }
}
