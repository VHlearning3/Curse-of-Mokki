using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TEST ONLY. Keys to add and remove items so the inventory and HUD can be tried without pickups.
/// Default setup in Vili scene: 1 = lantern, 2 = pills, 3 = key. Hold Left Alt + the same key to remove one.
/// Remove from the real player.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class InventoryDebug : MonoBehaviour
{
    [Serializable]
    public struct DebugItem
    {
        public ItemData item;
        public Key key;
    }

    [SerializeField] List<DebugItem> items = new List<DebugItem>();
    [Tooltip("Hold this together with an item key to remove one instead of adding.")]
    [SerializeField] Key removeModifier = Key.LeftAlt;

    PlayerInventory inventory;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        bool remove = removeModifier != Key.None && kb[removeModifier].isPressed;

        foreach (DebugItem d in items)
        {
            if (d.item == null || d.key == Key.None || !kb[d.key].wasPressedThisFrame) continue;
            if (remove) inventory.Remove(d.item, 1);
            else if (inventory.Add(d.item, 1) == 0) Debug.Log($"[InventoryDebug] {d.item.DisplayName} is full ({d.item.MaxCount}).", this);
        }
    }
}
