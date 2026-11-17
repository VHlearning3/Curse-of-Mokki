
using System.Timers;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerMovement :
MonoBehaviour
{
    [SerializeField]
    private float
    walkSpeed = 3f;
    private CharacterController
    controller;
    [SerializeField] private float gravity = -9.81f;
    private float fallspeed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

    }
    void Update()
    {
        Vector3 direction = Vector3.zero;
        if
                    (Keyboard.current.wKey.isPressed)

        {
            direction +=
                transform.forward;

        }

        if
         (Keyboard.current.aKey.isPressed)
        {
            direction -=
                transform.right;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            direction +=
                transform.right;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            direction -=
                transform.forward;
        }
        direction = direction.normalized;
        if (controller.isGrounded)
            fallspeed = -2f;
        fallspeed += gravity * Time.deltaTime;
        Vector3 velocity = direction *
             walkSpeed;
        velocity.y = fallspeed;
        controller.Move(velocity * Time.deltaTime);
    }

}
