using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>TEST ONLY: press L to make the player look at Target. Works on any object in the scene. Remove before the real game.</summary>
public class LookAtDebug : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float seconds = 3f;

    private PlayerMovement player;

    void Awake()
    {
        player = FindFirstObjectByType<PlayerMovement>();
    }

    void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame && target != null && player != null)
        {
            player.LookAt(target.position, seconds);
        }
    }
}
