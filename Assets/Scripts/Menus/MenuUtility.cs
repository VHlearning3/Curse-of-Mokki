using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>Small helpers shared by the menus.</summary>
public static class MenuUtility
{
    /// <summary>Makes sure the scene has an EventSystem (needed for clicking buttons) that uses the new Input System.</summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem (runtime)", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    /// <summary>Selects a button so keyboard / gamepad navigation works.</summary>
    public static void Select(GameObject target)
    {
        if (EventSystem.current != null && target != null && target.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(target);
    }

    /// <summary>
    /// Loads a scene by name. In the Editor the scene doesn't need to be in Build Settings yet.
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        if (string.IsNullOrEmpty(sceneName)) { Debug.LogWarning("[Menus] No scene name set."); return; }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
            return;
        }
#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != sceneName) continue;
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        Debug.LogError($"[Menus] Scene \"{sceneName}\" not found. Add it to File > Build Profiles (Scene List).");
    }

    /// <summary>
    /// Restarts the current scene (placeholder for save point respawn).
    /// skipMainMenu = true: a main menu in the scene stays closed after the reload.
    /// </summary>
    public static void ReloadCurrentScene(bool skipMainMenu)
    {
        Time.timeScale = 1f;
        MainMenu.SkipOpenOnNextStart = skipMainMenu;
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    /// <summary>Quits the game (stops Play Mode in the Editor).</summary>
    public static void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
