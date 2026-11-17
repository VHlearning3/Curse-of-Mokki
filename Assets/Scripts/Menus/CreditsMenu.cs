using System;
using UnityEngine;

/// <summary>
/// Credits panel. The text is a normal TextMeshPro object in the Menu_Credits prefab - edit it there.
/// </summary>
public class CreditsMenu : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Button backButton;

    /// <summary>The panel was closed (Back).</summary>
    public event Action OnClosed;

    public bool IsOpen => gameObject.activeSelf;

    bool wired;

    void Awake() => Wire();

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Wire();
        MenuUtility.Select(backButton != null ? backButton.gameObject : null);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }
}
