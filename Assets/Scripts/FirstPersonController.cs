using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -15f;

    [Header("Look Settings")]
    public Transform cameraTransform;
    public float lookSensitivity = 0.1f;
    public float topClamp = 90f;
    public float bottomClamp = -90f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float verticalRotation = 0f;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");
        jumpAction = InputSystem.actions.FindAction("Jump");
        sprintAction = InputSystem.actions.FindAction("Sprint");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>() * lookSensitivity;

        // Horizontal rotation (Player body)
        transform.Rotate(Vector3.up * lookInput.x);

        // Vertical rotation (Camera)
        verticalRotation -= lookInput.y;
        verticalRotation = Mathf.Clamp(verticalRotation, bottomClamp, topClamp);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float currentSpeed = sprintAction.IsPressed() ? sprintSpeed : moveSpeed;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (jumpAction.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
