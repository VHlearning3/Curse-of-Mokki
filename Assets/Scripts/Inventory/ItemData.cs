using UnityEngine;

/// <summary>One kind of item. Create > Curse of Mokki > Item, name it ITEM_BaseName.</summary>
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

    [Tooltip("Keep this item visible in the HUD the whole time the player carries it (e.g. keys). Off = shown only for a moment when it changes.")]
    [SerializeField] bool alwaysShowWhileCarried = false;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public bool Stackable => stackable;
    public int MaxCount => stackable ? Mathf.Max(1, maxCount) : 1;
    public bool ShowInHUD => showInHUD;
    public bool AlwaysShowWhileCarried => alwaysShowWhileCarried;
}
