using UnityEngine;
using UnityEngine.InputSystem;

// On foot: WASD to walk, Shift to sprint, Space to jump, mouse to look, E to get in or out of a car.
public class PlayerController : MonoBehaviour
{
    public CarController car;
    public WeaponAim aim;
    Transform cam;
    CharacterController cc;
    float yaw, pitch, vy;
    bool driving;

    public static PlayerController Create(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Player");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var controller = go.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0, 0.9f, 0);
        var p = go.AddComponent<PlayerController>();
        p.cc = controller;
        var cam = Camera.main;
        if (!cam) cam = new GameObject("Camera").AddComponent<Camera>();
        p.cam = cam.transform;
        p.cam.SetParent(go.transform, false);
        p.cam.localPosition = new Vector3(0, 1.65f, 0);
        p.cam.localRotation = Quaternion.identity;
        Cursor.lockState = CursorLockMode.Locked;
        return p;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
        if (mouse.leftButton.wasPressedThisFrame && !(aim && aim.WheelOpen)) Cursor.lockState = CursorLockMode.Locked;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue() * 0.1f * (aim ? aim.SensScale : 1f);
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
        }

        if (kb.eKey.wasPressedThisFrame && car)
        {
            if (driving) TryExit();
            else if (Vector3.Distance(transform.position, car.transform.position) < 3.2f) Enter();
        }
        if (driving) return;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(x, 0, z);
        if (move.sqrMagnitude > 1f) move.Normalize();
        float speed = kb.leftShiftKey.isPressed ? 7.5f : 4.5f;
        if (cc.isGrounded)
        {
            vy = -2f;
            if (kb.spaceKey.wasPressedThisFrame) vy = 5.5f;
        }
        vy += -20f * Time.deltaTime;
        cc.Move((move * speed + Vector3.up * vy) * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        cam.localRotation = Quaternion.Euler(pitch, 0, 0);
    }

    void Enter()
    {
        driving = true;
        cc.enabled = false;
        car.occupied = true;
        cam.SetParent(car.transform, false);
        cam.localPosition = new Vector3(0, 2.6f, -7f);
        cam.localRotation = Quaternion.Euler(12f, 0, 0);
    }

    void TryExit()
    {
        if (car.rb.linearVelocity.magnitude > 3f) return;   // stop the car before getting out
        driving = false;
        car.occupied = false;
        var p = car.transform.position + car.transform.right * 2f;
        p.y = 0.1f;
        transform.position = p;
        yaw = car.transform.eulerAngles.y;
        cc.enabled = true;
        cam.SetParent(transform, false);
        cam.localPosition = new Vector3(0, 1.65f, 0);
    }
}
