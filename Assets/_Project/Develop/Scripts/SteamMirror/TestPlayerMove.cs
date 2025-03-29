using Mirror;
using UnityEngine;

public class TestPlayerMove : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float speed = 3f;
    private float verticalRotation = 0f;

    private void Start()
    {
        if (!isLocalPlayer)
        {
            cameraTransform.gameObject.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        Move();
        CameraRotate();
    }


    private void Move()
    {
        float directionX = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float directionZ = Input.GetAxis("Vertical") * speed * Time.deltaTime;

        Vector3 moveDirection = transform.right * directionX + transform.forward * directionZ;
        transform.position += moveDirection;
    }

    private void CameraRotate()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
}
