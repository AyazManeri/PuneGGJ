using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField] private bool useAxisForMovement = true;


    [Space]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;


    private PlayerController playercontroller;
    private FrameInput frameInput;

    private void Awake()
    {
        playercontroller = GetComponent<PlayerController>();
    }
    private void Update()
    {
        if (playercontroller != null)
        {
            Vector2 moveInput;
            if (useAxisForMovement)
            {
                moveInput = new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis));
            }
            else
            {
                moveInput = new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis));
            }

            frameInput = new FrameInput
            {
                Move = moveInput,
                JumpDown = Input.GetKeyDown(jumpKey),
                JumpHeld = Input.GetKey(jumpKey),
                JumpUp = Input.GetKeyUp(jumpKey),
                DashDown = Input.GetKeyDown(dashKey)

            };
            SendInputToController();
        }
    }

    void SendInputToController()
    {
        playercontroller.SetInput(frameInput);
    }



}