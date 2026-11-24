using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One dialogue sequence (list of lines).
/// Create > Curse of Mokki > Dialogue, save under Assets/Data/Dialogues/.
/// </summary>
[CreateAssetMenu(fileName = "DLG_NewDialogue", menuName = "Curse of Mokki/Dialogue")]
public class DialogueData : ScriptableObject
{
    [Serializable]
    public struct Line
    {
        [Tooltip("Speaker name shown above the text (e.g. \"Kaveri\"). Leave empty for player thoughts or narration.")]
        public string speakerName;

        [TextArea(2, 5)]
        [Tooltip("Subtitle text shown at the bottom of the screen.")]
        public string text;

        [Tooltip("Optional clip name in AudioManager (e.g. \"SFX_VACottage_Intro\"). Leave empty if using Voice Clip below or no audio.")]
        public string voiceClipName;

        [Tooltip("Optional direct AudioClip reference (used if Voice Clip Name is empty, or to read exact clip length).")]
        public AudioClip voiceClip;

        [Tooltip("Seconds this line stays on screen. 0 = automatic (uses Voice Clip length, or calculates from word count).")]
        [Min(0f)]
        public float duration;
    }

    [Header("Lines")]
    [SerializeField] List<Line> lines = new List<Line>();

    [Header("Behaviour")]
    [Tooltip("On = player cannot move while this dialogue plays (e.g. cutscenes). Off = player can keep walking.")]
    [SerializeField] bool blockMovement = false;

    [Tooltip("Can the player skip to the next line with E or left click?")]
    [SerializeField] bool canSkip = true;

    [Header("Automatic timing (when a line's Duration is 0)")]
    [Tooltip("Minimum seconds any line stays visible.")]
    [SerializeField, Min(0.5f)] float minLineDuration = 2f;

    [Tooltip("Seconds added per word when no Voice Clip is set.")]
    [SerializeField, Min(0.1f)] float secondsPerWord = 0.35f;

    public IReadOnlyList<Line> Lines => lines;
    public bool BlockMovement => blockMovement;
    public bool CanSkip => canSkip;

    /// <summary>Returns how long a line should stay on screen.</summary>
    public float GetDuration(int index)
    {
        if (index < 0 || index >= lines.Count) return minLineDuration;

        Line line = lines[index];
        if (line.duration > 0f) return line.duration;
        if (line.voiceClip != null) return Mathf.Max(minLineDuration, line.voiceClip.length);

        int words = CountWords(line.text);
        return Mathf.Max(minLineDuration, words * secondsPerWord);
    }

    static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int count = 0;
        bool inWord = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }
}