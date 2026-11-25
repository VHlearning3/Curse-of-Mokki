using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Anything that should go back to its start when the player respawns at a save point
/// (enemies, chases). Register in Awake with SavePointSystem.Register(this), unregister in OnDestroy.
/// </summary>
public interface IRespawnResettable
{
    void ResetForRespawn();
}

/// <summary>
/// Save Point System (GDD): remembers the last activated SavePoint and respawns the player there.
/// No scene setup: just place SavePoint prefabs. DeathEndScreen's Continue calls Respawn().
/// Only in memory (not saved to disk): quitting to the main menu or reloading the scene clears it.
/// </summary>
public static class SavePointSystem
{
    /// <summary>The save point the player respawns at, or null.</summary>
    public static SavePoint Active { get; private set; }

    public static bool HasActive => Active != null;

    /// <summary>A save point was activated (argument: the new one).</summary>
    public static event Action<SavePoint> OnActivated;

    /// <summary>The player was moved to a save point and everything was reset.</summary>
    public static event Action<SavePoint> OnRespawned;

    static readonly List<IRespawnResettable> resettables = new List<IRespawnResettable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = null;
        OnActivated = null;
        OnRespawned = null;
        resettables.Clear();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Active = null; // a new or reloaded scene starts without a save point
    }

    public static void Register(IRespawnResettable item)
    {
        if (item != null && !resettables.Contains(item)) resettables.Add(item);
    }

    public static void Unregister(IRespawnResettable item) => resettables.Remove(item);

    /// <summary>Makes this the respawn save point. Returns false if it was refused (only forward).</summary>
    public static bool Activate(SavePoint point, bool force = false)
    {
        if (point == null || point == Active) return false;
        if (!force && Active != null && point.OnlyForward && point.Order < Active.Order) return false;

        SavePoint previous = Active;
        Active = point;
        if (previous != null) previous.NotifyDeactivated();
        point.NotifyActivated();
        OnActivated?.Invoke(point);
        return true;
    }

    /// <summary>Clears the active save point (Continue will then restart the scene).</summary>
    public static void Clear()
    {
        SavePoint previous = Active;
        Active = null;
        if (previous != null) previous.NotifyDeactivated();
    }

    /// <summary>
    /// Moves the player to the active save point, resets madness and every registered enemy.
    /// Returns false if there is no active save point or no player.
    /// </summary>
    public static bool Respawn()
    {
        if (Active == null) return false;
        PlayerMadness madness = UnityEngine.Object.FindFirstObjectByType<PlayerMadness>();
        if (madness == null)
        {
            Debug.LogWarning("[SavePointSystem] No PlayerMadness (player) in the scene, can't respawn.");
            return false;
        }

        SavePoint point = Active;
        Transform player = madness.transform;
        point.GetSpawnPose(player, out Vector3 position, out Quaternion rotation);
        Teleport(player, position, rotation);
        madness.ResetMadness(point.RespawnMadness);

        // Copy first: a reset may activate / destroy objects that (un)register themselves.
        var items = resettables.ToArray();
        foreach (var item in items)
        {
            if (item is UnityEngine.Object o && o == null) { resettables.Remove(item); continue; }
            try { item.ResetForRespawn(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        point.NotifyRespawned();
        OnRespawned?.Invoke(point);
        return true;
    }

    /// <summary>Moves a player that has a CharacterController (which would otherwise undo the move).</summary>
    public static void Teleport(Transform player, Vector3 position, Quaternion rotation)
    {
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        if (wasEnabled) controller.enabled = true;
    }
}
