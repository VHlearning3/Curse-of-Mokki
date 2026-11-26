
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    // ----- Camera -----
    [SerializeField] private Transform cameraTransform;
    private PlayerMovement movement;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (MenuUtility.AnyMenuOpen)
        {
            return;
        }
    }
}