using UnityEngine;

/// <summary>
/// One kind of item the player can carry (lantern, pills, a key...).
/// Create with: right-click in Project > Create > Curse of Mokki > Item. Name assets ITEM_BaseName (e.g. ITEM_Pills).
/// </summary>
[CreateAssetMenu(fileName = "ITEM_NewItem", menuName = "Curse of Mokki/Item")]
public class ItemData : ScriptableObject
{
    [Tooltip("Name shown in the HUD. Empty = the asset name.")]
    [SerializeField] string displayName = "";

    [Tooltip("UI_* sprite shown in the HUD. Empty = the name is shown as text instead.")]
    [SerializeField] Sprite icon;

    [Tooltip("Can the player carry more than one (e.g. pills)?")]
    [SerializeField] bool stackable;

    [Tooltip("Most the player can carry when stackable.")]
    [SerializeField, Min(1)] int maxCount = 1;

    [Tooltip("Show this item in the HUD.")]
    [SerializeField] bool showInHUD = true;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public bool Stackable => stackable;
    public int MaxCount => stackable ? Mathf.Max(1, maxCount) : 1;
    public bool ShowInHUD => showInHUD;
}
