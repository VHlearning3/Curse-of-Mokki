using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool: writes the project's C# scripts, scene hierarchies and ScriptableObject values into one
/// Markdown file for NotebookLM. Output: &lt;project root&gt;/NotebookExport/ (outside Assets, git-ignored).
/// Window: Tools > NotebookLM Script Exporter. Editor only (Assets/Editor), not in builds.
/// </summary>
public class NotebookScriptExporter : EditorWindow
{
    const string Prefix = "CurseOfMokki.NotebookExporter.";
    const int MaxArrayItems = 25;
    const int MaxDepth = 4;

    // ---------- Settings (EditorPrefs, per computer) ----------

    public static bool AutoUpdate { get => EditorPrefs.GetBool(Prefix + "AutoUpdate", true); set => EditorPrefs.SetBool(Prefix + "AutoUpdate", value); }
    public static bool IncludeScripts { get => EditorPrefs.GetBool(Prefix + "Scripts", true); set => EditorPrefs.SetBool(Prefix + "Scripts", value); }
    public static bool IncludeActiveScene { get => EditorPrefs.GetBool(Prefix + "ActiveScene", true); set => EditorPrefs.SetBool(Prefix + "ActiveScene", value); }
    public static bool IncludeBuildScenes { get => EditorPrefs.GetBool(Prefix + "BuildScenes", true); set => EditorPrefs.SetBool(Prefix + "BuildScenes", value); }
    public static bool IncludeDisabledBuildScenes { get => EditorPrefs.GetBool(Prefix + "DisabledBuildScenes", false); set => EditorPrefs.SetBool(Prefix + "DisabledBuildScenes", value); }
    public static bool IncludeInspectorValues { get => EditorPrefs.GetBool(Prefix + "InspectorValues", true); set => EditorPrefs.SetBool(Prefix + "InspectorValues", value); }
    public static bool IncludeScriptableObjects { get => EditorPrefs.GetBool(Prefix + "ScriptableObjects", true); set => EditorPrefs.SetBool(Prefix + "ScriptableObjects", value); }
    public static string FileName { get => EditorPrefs.GetString(Prefix + "FileName", "ProjectContext_NotebookLM.md"); set => EditorPrefs.SetString(Prefix + "FileName", value); }
    public static string IgnoredFolders { get => EditorPrefs.GetString(Prefix + "IgnoredFolders", "Plugins, TextMesh Pro, TutorialInfo"); set => EditorPrefs.SetString(Prefix + "IgnoredFolders", value); }

    public static string ExportFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "NotebookExport");

    public static string ExportPath
    {
        get
        {
            string name = FileName;
            if (string.IsNullOrWhiteSpace(name)) name = "ProjectContext_NotebookLM.md";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name += ".md";
            return Path.Combine(ExportFolder, name);
        }
    }

    // ---------- Window ----------

    [MenuItem("Tools/NotebookLM Script Exporter")]
    static void OpenWindow()
    {
        GetWindow<NotebookScriptExporter>("NotebookLM Exporter");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("NotebookLM Script Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Writes scripts, scene hierarchies and ScriptableObject values into one Markdown file outside Assets (NotebookExport/). Settings are saved on this computer only.", MessageType.Info);

        AutoUpdate = EditorGUILayout.Toggle(new GUIContent("Päivitä aina automaattisesti", "Export again after scripts, assets or scenes are saved."), AutoUpdate);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sisällytettävät osiot", EditorStyles.boldLabel);
        IncludeScripts = EditorGUILayout.Toggle("C#-skriptit", IncludeScripts);
        IncludeActiveScene = EditorGUILayout.Toggle("Aktiivinen scene", IncludeActiveScene);
        IncludeBuildScenes = EditorGUILayout.Toggle("Build Settings -scenet", IncludeBuildScenes);
        using (new EditorGUI.DisabledScope(!IncludeBuildScenes))
        {
            EditorGUI.indentLevel++;
            IncludeDisabledBuildScenes = EditorGUILayout.Toggle("Myös pois käytöstä poistetut", IncludeDisabledBuildScenes);
            EditorGUI.indentLevel--;
        }
        IncludeInspectorValues = EditorGUILayout.Toggle("Skriptien Inspector-arvot", IncludeInspectorValues);
        IncludeScriptableObjects = EditorGUILayout.Toggle("ScriptableObject-arvot", IncludeScriptableObjects);

        EditorGUILayout.Space();
        FileName = EditorGUILayout.TextField("Tiedostonimi", FileName);
        IgnoredFolders = EditorGUILayout.TextField(new GUIContent("Ohitettavat kansiot", "Comma separated folder names, matched anywhere under Assets."), IgnoredFolders);

        EditorGUILayout.Space();
        if (GUILayout.Button("Vie projektin tiedot nyt")) ExportAll(true);
        if (GUILayout.Button("Avaa vientikansio"))
        {
            Directory.CreateDirectory(ExportFolder);
            string target = File.Exists(ExportPath) ? ExportPath : ExportFolder;
            EditorUtility.RevealInFinder(target);
        }
        EditorGUILayout.LabelField("Tiedosto:", ExportPath, EditorStyles.miniLabel);
    }

    // ---------- Export ----------

    class Stats
    {
        public int scenes, gameObjects, scriptableObjects, scripts;
        public readonly List<string> notes = new List<string>();
    }

    /// <summary>Writes the Markdown file. Returns true on success. Never throws.</summary>
    public static bool ExportAll(bool logSummary)
    {
        var stats = new Stats();
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Project context: " + PlayerSettings.productName);
            sb.AppendLine();
            sb.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " by Tools > NotebookLM Script Exporter. Unity " + Application.unityVersion + ".");
            sb.AppendLine();

            if (IncludeActiveScene || IncludeBuildScenes) WriteScenes(sb, stats);
            if (IncludeScriptableObjects) WriteScriptableObjects(sb, stats);
            if (IncludeScripts) WriteScripts(sb, stats);

            Directory.CreateDirectory(ExportFolder);
            File.WriteAllText(ExportPath, sb.ToString(), new UTF8Encoding(false));

            if (logSummary)
            {
                string notes = stats.notes.Count > 0 ? " Notes: " + string.Join("; ", stats.notes) : "";
                Debug.Log($"[NotebookLM Exporter] Wrote {ExportPath} — {stats.scenes} scene(s), {stats.gameObjects} GameObjects, {stats.scriptableObjects} ScriptableObject(s), {stats.scripts} script(s).{notes}");
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NotebookLM Exporter] Export failed: " + e.Message);
            return false;
        }
    }

    // ---------- Scenes ----------

    static void WriteScenes(StringBuilder sb, Stats stats)
    {
        sb.AppendLine("## Scenes");
        sb.AppendLine();
        Scene active = SceneManager.GetActiveScene();
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        bool values = IncludeInspectorValues;

        if (IncludeActiveScene && active.IsValid())
        {
            string role = "Aktiivinen";
            int index = BuildIndexOf(buildScenes, active.path);
            if (index >= 0) role += ", Build Index: " + index;
            if (active.isDirty) role += ", unsaved changes included";
            WriteScene(sb, active, SceneTitle(active.path, active.name) + " [" + role + "]", values, stats);
        }

        if (!IncludeBuildScenes) return;

        bool canOpen = !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying
                       && UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null;
        int buildIndex = 0;
        foreach (EditorBuildSettingsScene entry in buildScenes)
        {
            int thisIndex = entry.enabled ? buildIndex++ : -1;
            if (!entry.enabled && !IncludeDisabledBuildScenes) continue;
            if (IncludeActiveScene && active.IsValid() && entry.path == active.path) continue;

            string role = entry.enabled ? "Build Index: " + thisIndex : "Pois käytöstä";
            string title = SceneTitle(entry.path, null) + " [" + role + "]";

            Scene loaded = SceneManager.GetSceneByPath(entry.path);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                WriteScene(sb, loaded, title, values, stats);
                continue;
            }
            if (!File.Exists(entry.path))
            {
                sb.AppendLine("### " + title);
                sb.AppendLine("Scene file not found: `" + entry.path + "`");
                sb.AppendLine();
                continue;
            }
            if (!canOpen)
            {
                sb.AppendLine("### " + title);
                sb.AppendLine("Skipped: closed scenes are not opened in Play Mode or while a prefab is being edited.");
                sb.AppendLine();
                stats.notes.Add("skipped closed scene " + entry.path);
                continue;
            }

            Scene temp = default;
            try
            {
                temp = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                WriteScene(sb, temp, title, values, stats);
            }
            finally
            {
                if (temp.IsValid()) EditorSceneManager.CloseScene(temp, true);
            }
        }
    }

    static int BuildIndexOf(EditorBuildSettingsScene[] scenes, string path)
    {
        int index = 0;
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (!s.enabled) continue;
            if (s.path == path) return index;
            index++;
        }
        return -1;
    }

    static string SceneTitle(string path, string fallback)
    {
        if (string.IsNullOrEmpty(path)) return string.IsNullOrEmpty(fallback) ? "Untitled" : fallback;
        return Path.GetFileNameWithoutExtension(path);
    }

    static void WriteScene(StringBuilder sb, Scene scene, string title, bool values, Stats stats)
    {
        stats.scenes++;
        sb.AppendLine("### " + title);
        if (!string.IsNullOrEmpty(scene.path)) sb.AppendLine("Path: `" + scene.path + "`");
        sb.AppendLine();
        sb.AppendLine("```text");
        foreach (GameObject root in scene.GetRootGameObjects()) WriteGameObject(sb, root, 0, values, stats);
        sb.AppendLine("```");
        sb.AppendLine();
    }

    static void WriteGameObject(StringBuilder sb, GameObject go, int depth, bool values, Stats stats)
    {
        stats.gameObjects++;
        string indent = new string(' ', depth * 2);
        var tags = new List<string>();
        if (!go.activeSelf) tags.Add("Inactive");
        if (!go.CompareTag("Untagged")) tags.Add("Tag: " + go.tag);
        if (go.layer != 0) tags.Add("Layer: " + LayerMask.LayerToName(go.layer));

        Component[] components = go.GetComponents<Component>();
        var names = new List<string>();
        foreach (Component c in components)
        {
            if (c == null) names.Add("Missing Script");
            else if (!(c is Transform)) names.Add(c.GetType().Name);
        }

        var line = new StringBuilder(indent + "- " + go.name);
        if (tags.Count > 0) line.Append(" [" + string.Join(", ", tags) + "]");
        if (names.Count > 0) line.Append(": " + string.Join(", ", names));
        sb.AppendLine(line.ToString());

        if (values)
        {
            foreach (Component c in components)
            {
                if (c is MonoBehaviour mb && IsOwnScript(mb))
                {
                    sb.AppendLine(indent + "    " + mb.GetType().Name + ":");
                    WriteProperties(sb, new SerializedObject(mb), indent + "      ");
                }
            }
        }

        foreach (Transform child in go.transform) WriteGameObject(sb, child.gameObject, depth + 1, values, stats);
    }

    // ---------- ScriptableObjects ----------

    static void WriteScriptableObjects(StringBuilder sb, Stats stats)
    {
        sb.AppendLine("## ScriptableObjects");
        sb.AppendLine();
        string[] ignored = IgnoredList();
        var paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        int count = 0;
        foreach (string path in paths)
        {
            if (IsIgnored(path, ignored)) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (asset == null || IsBuiltInType(asset.GetType())) continue;
            MonoScript script = MonoScript.FromScriptableObject(asset);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : "";
            if (string.IsNullOrEmpty(scriptPath) || !scriptPath.StartsWith("Assets/") || IsIgnored(scriptPath, ignored)) continue;

            count++;
            sb.AppendLine("### " + asset.name + " (" + asset.GetType().Name + ")");
            sb.AppendLine("Path: `" + path + "`");
            sb.AppendLine();
            sb.AppendLine("```text");
            WriteProperties(sb, new SerializedObject(asset), "");
            sb.AppendLine("```");
            sb.AppendLine();
        }
        if (count == 0) { sb.AppendLine("(none)"); sb.AppendLine(); }
        stats.scriptableObjects = count;
    }

    // ---------- C# scripts ----------

    static void WriteScripts(StringBuilder sb, Stats stats)
    {
        sb.AppendLine("## C# scripts");
        sb.AppendLine();
        string[] ignored = IgnoredList();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Substring(projectRoot.Length + 1).Replace('\\', '/'))
            .Where(p => !IsIgnored(p, ignored))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        stats.scripts = files.Count;

        sb.AppendLine("```text");
        string lastFolder = null;
        foreach (string file in files)
        {
            string folder = Path.GetDirectoryName(file).Replace('\\', '/');
            if (folder != lastFolder)
            {
                sb.AppendLine(folder + "/");
                lastFolder = folder;
            }
            sb.AppendLine("  " + Path.GetFileName(file));
        }
        sb.AppendLine("```");
        sb.AppendLine();

        foreach (string file in files)
        {
            sb.AppendLine("### " + file);
            sb.AppendLine();
            sb.AppendLine("```csharp");
            string text = File.ReadAllText(Path.Combine(projectRoot, file)).Replace("\r\n", "\n").TrimEnd('\n');
            sb.AppendLine(text);
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    // ---------- Serialized values ----------

    static void WriteProperties(StringBuilder sb, SerializedObject so, string indent)
    {
        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;
            WriteProperty(sb, it.Copy(), it.name, indent, 0);
        }
    }

    static void WriteProperty(StringBuilder sb, SerializedProperty p, string label, string indent, int depth)
    {
        if (p.isArray && p.propertyType != SerializedPropertyType.String)
        {
            sb.AppendLine(indent + label + ": [" + p.arraySize + "]");
            if (depth >= MaxDepth) return;
            int shown = Math.Min(p.arraySize, MaxArrayItems);
            for (int i = 0; i < shown; i++)
                WriteProperty(sb, p.GetArrayElementAtIndex(i), "[" + i + "]", indent + "  ", depth + 1);
            if (p.arraySize > shown) sb.AppendLine(indent + "  … " + (p.arraySize - shown) + " more");
            return;
        }

        if (p.propertyType == SerializedPropertyType.Generic && p.hasVisibleChildren)
        {
            sb.AppendLine(indent + label + ":");
            if (depth >= MaxDepth) { sb.AppendLine(indent + "  …"); return; }
            SerializedProperty child = p.Copy();
            SerializedProperty end = p.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                WriteProperty(sb, child.Copy(), child.name, indent + "  ", depth + 1);
            }
            return;
        }

        sb.AppendLine(indent + label + ": " + FormatValue(p));
    }

    static string FormatValue(SerializedProperty p)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.longValue.ToString(ci);
            case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
            case SerializedPropertyType.Float: return p.floatValue.ToString("0.####", ci);
            case SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
            case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(p.colorValue);
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue != null ? p.objectReferenceValue.name + " (" + p.objectReferenceValue.GetType().Name + ")" : "None";
            case SerializedPropertyType.LayerMask: return "LayerMask " + p.intValue.ToString(ci);
            case SerializedPropertyType.Enum:
                if (p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length) return p.enumDisplayNames[p.enumValueIndex];
                return p.intValue.ToString(ci);
            case SerializedPropertyType.Vector2: return V(p.vector2Value.x, p.vector2Value.y);
            case SerializedPropertyType.Vector3: return V(p.vector3Value.x, p.vector3Value.y, p.vector3Value.z);
            case SerializedPropertyType.Vector4: return V(p.vector4Value.x, p.vector4Value.y, p.vector4Value.z, p.vector4Value.w);
            case SerializedPropertyType.Vector2Int: return "(" + p.vector2IntValue.x + ", " + p.vector2IntValue.y + ")";
            case SerializedPropertyType.Vector3Int: return "(" + p.vector3IntValue.x + ", " + p.vector3IntValue.y + ", " + p.vector3IntValue.z + ")";
            case SerializedPropertyType.Quaternion: Vector3 e = p.quaternionValue.eulerAngles; return "euler " + V(e.x, e.y, e.z);
            case SerializedPropertyType.Rect: return "Rect " + V(p.rectValue.x, p.rectValue.y, p.rectValue.width, p.rectValue.height);
            case SerializedPropertyType.Bounds: return "Bounds center " + V(p.boundsValue.center.x, p.boundsValue.center.y, p.boundsValue.center.z);
            case SerializedPropertyType.AnimationCurve: return "AnimationCurve (" + (p.animationCurveValue != null ? p.animationCurveValue.length : 0) + " keys)";
            case SerializedPropertyType.Gradient: return "Gradient";
            case SerializedPropertyType.Character: return "'" + (char)p.intValue + "'";
            case SerializedPropertyType.ManagedReference: return string.IsNullOrEmpty(p.managedReferenceFullTypename) ? "None" : p.managedReferenceFullTypename;
            default: return p.propertyType.ToString();
        }
    }

    static string V(params float[] values)
    {
        return "(" + string.Join(", ", values.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))) + ")";
    }

    // ---------- Filters ----------

    static bool IsBuiltInType(Type t)
    {
        string ns = t.Namespace ?? "";
        return ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor") || ns.StartsWith("TMPro");
    }

    /// <summary>The project's own MonoBehaviour: not a built-in type and its script file is under Assets (not a package).</summary>
    static bool IsOwnScript(MonoBehaviour mb)
    {
        if (IsBuiltInType(mb.GetType())) return false;
        MonoScript script = MonoScript.FromMonoBehaviour(mb);
        string path = script != null ? AssetDatabase.GetAssetPath(script) : "";
        return path.StartsWith("Assets/") && !IsIgnored(path, IgnoredList());
    }

    static string[] IgnoredList()
    {
        return IgnoredFolders.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }

    static bool IsIgnored(string assetPath, string[] ignored)
    {
        string[] parts = assetPath.Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
            foreach (string folder in ignored)
                if (string.Equals(parts[i], folder, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

/// <summary>
/// Re-exports in the background after scripts, assets, scenes or the Build Settings list change.
/// Debounced via EditorApplication.update: waits until compiling and importing are done and
/// nothing changed for a moment; never runs in Play Mode (a pending export runs after leaving it).
/// </summary>
[InitializeOnLoad]
public class NotebookScriptExporterAutoUpdate : AssetPostprocessor
{
    const string PendingKey = "CurseOfMokki.NotebookExporter.Pending";
    const double QuietSeconds = 2.0;
    static double dueTime;
    static bool queued;

    static NotebookScriptExporterAutoUpdate()
    {
        EditorSceneManager.sceneSaved += _ => Schedule();
        EditorBuildSettings.sceneListChanged += Schedule;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingKey, false)) Schedule();
        };
        // An export requested just before a script recompile survives the domain reload.
        if (SessionState.GetBool(PendingKey, false)) Schedule();
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!NotebookScriptExporter.AutoUpdate) return;
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsRelevant)) Schedule();
    }

    static bool IsRelevant(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }

    public static void Schedule()
    {
        if (!NotebookScriptExporter.AutoUpdate) { SessionState.SetBool(PendingKey, false); return; }
        SessionState.SetBool(PendingKey, true);
        dueTime = EditorApplication.timeSinceStartup + QuietSeconds;
        if (!queued)
        {
            queued = true;
            // Polled from EditorApplication.update (not only delayCall): delayCall may not fire while the
            // Unity window is in the background. Tick waits until compiling/importing is done.
            StartWaiting();
        }
    }

    static void StartWaiting()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(PendingKey, false) || !NotebookScriptExporter.AutoUpdate) { Stop(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Stop(); return; } // runs again after leaving Play Mode
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < dueTime) return;
        Stop();
        SessionState.SetBool(PendingKey, false);
        NotebookScriptExporter.ExportAll(false);
    }

    static void Stop()
    {
        EditorApplication.update -= Tick;
        queued = false;
    }
}
