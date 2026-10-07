using UnityEngine;

public sealed class FirstPersonWalker : MonoBehaviour
{
    public Camera viewCamera;
    public float walkSpeed = 3.5f;
    public float runMultiplier = 1.7f;
    public float mouseSensitivity = 2f;
    public float eyeHeight = 1.65f;
    public float floorHeight;
    public bool lockCursorOnStart;

    private float pitch;

    private void Start()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null)
        {
            viewCamera.transform.SetParent(transform, false);
            viewCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            viewCamera.transform.localRotation = Quaternion.identity;
        }
        LockCursor(lockCursorOnStart);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        if (Input.GetMouseButtonDown(1) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector3 direction = transform.right * Input.GetAxisRaw("Horizontal")
                          + transform.forward * Input.GetAxisRaw("Vertical");
        direction.y = 0f;
        float speed = walkSpeed * (Input.GetKey(KeyCode.LeftShift) ? runMultiplier : 1f);
        transform.position += direction.normalized * speed * Time.deltaTime;
        transform.position = new Vector3(transform.position.x, floorHeight, transform.position.z);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
