using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Items the player carries. Pickups and doors use Add / Has / Remove.</summary>
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public struct StartItem
    {
        public ItemData item;
        [Min(1)] public int count;
    }

    [Serializable]
    public class Slot
    {
        public ItemData item;
        public int count;
    }

    [Tooltip("Items the player has when the scene starts (handy for testing).")]
    [SerializeField] List<StartItem> startItems = new List<StartItem>();

    [Header("Debug (live contents)")]
    [Tooltip("What the player carries right now. Shown for debugging.")]
    [SerializeField] List<Slot> slots = new List<Slot>();

    /// <summary>An item was added. Arguments: item, amount actually added.</summary>
    public event Action<ItemData, int> OnItemAdded;

    /// <summary>An item was removed. Arguments: item, amount removed.</summary>
    public event Action<ItemData, int> OnItemRemoved;

    /// <summary>Contents changed in any way.</summary>
    public event Action OnChanged;

    /// <summary>Current contents in the order they were picked up.</summary>
    public IReadOnlyList<Slot> Slots => slots;

    void Awake()
    {
        slots.Clear();
        foreach (StartItem s in startItems)
            if (s.item != null) AddInternal(s.item, Mathf.Max(1, s.count));
    }

    // --- Public API ---------------------------------------------------------

    /// <summary>Adds items. Returns how many were actually added (0 if already full).</summary>
    public int Add(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return 0;
        int added = AddInternal(item, amount);
        if (added > 0)
        {
            OnItemAdded?.Invoke(item, added);
            OnChanged?.Invoke();
        }
        return added;
    }

    /// <summary>Removes items. Returns false (and removes nothing) if the player doesn't have enough.</summary>
    public bool Remove(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        Slot slot = Find(item);
        if (slot == null || slot.count < amount) return false;

        slot.count -= amount;
        if (slot.count <= 0) slots.Remove(slot);
        OnItemRemoved?.Invoke(item, amount);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>True if the player carries at least this many (e.g. a door checking for its key).</summary>
    public bool Has(ItemData item, int amount = 1) => Count(item) >= amount;

    public int Count(ItemData item)
    {
        Slot slot = Find(item);
        return slot != null ? slot.count : 0;
    }

    /// <summary>True if one more of this item would fit.</summary>
    public bool CanAdd(ItemData item) => item != null && Count(item) < item.MaxCount;

    /// <summary>Empties the inventory (e.g. a new game).</summary>
    public void Clear()
    {
        if (slots.Count == 0) return;
        slots.Clear();
        OnChanged?.Invoke();
    }

    // --- Helpers ------------------------------------------------------------

    int AddInternal(ItemData item, int amount)
    {
        Slot slot = Find(item);
        int current = slot != null ? slot.count : 0;
        int added = Mathf.Min(amount, item.MaxCount - current);
        if (added <= 0) return 0;

        if (slot == null)
        {
            slot = new Slot { item = item, count = 0 };
            slots.Add(slot);
        }
        slot.count += added;
        return added;
    }

    Slot Find(ItemData item)
    {
        if (item == null) return null;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].item == item) return slots[i];
        return null;
    }
}
