using UnityEngine;

/// <summary>One item in the HUD (icon + count). Restyle it in the HUD_ItemSlot prefab.</summary>
public class HUDItemSlot : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    [SerializeField] TMPro.TextMeshProUGUI label;
    [Tooltip("Tint for item icons. White = the sprite's own colors.")]
    [SerializeField] Color iconColor = Color.white;
    [Tooltip("Color of the placeholder square for items that have no icon yet.")]
    [SerializeField] Color placeholderIconColor = new Color(1f, 1f, 1f, 0.15f);

    public void Show(ItemData item, int count)
    {
        name = "Slot_" + item.name;
        if (icon != null)
        {
            icon.sprite = item.Icon;
            icon.color = item.Icon != null ? iconColor : placeholderIconColor;
        }

        // No icon yet: show the name. Stackable items show the count.
        string text = item.Icon == null ? item.DisplayName : "";
        if (item.Stackable) text = (text.Length > 0 ? text + " " : "") + "x" + count;
        if (label != null)
        {
            label.text = text;
            label.gameObject.SetActive(text.Length > 0);
        }
    }
}
