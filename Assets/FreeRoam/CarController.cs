using UnityEngine;
using UnityEngine.InputSystem;

// A simple arcade car. The engine pushes toward a target speed, tyre grip stops sideways sliding,
// and steering only turns the car while it is moving. W/S to drive, A/D to steer.
public class CarController : MonoBehaviour
{
    public Rigidbody rb;
    public bool occupied;
    const float MAX_FWD = 28f;   // about 100 km/h
    const float MAX_REV = 8f;

    public static CarController Create(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Car");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var body = go.AddComponent<Rigidbody>();
        body.mass = 1200f;
        body.linearDamping = 0.05f;
        body.angularDamping = 4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.6f, 0);
        col.size = new Vector3(1.9f, 1.2f, 4.3f);
        CityBuilder.Box(go.transform, new Vector3(0, 0.5f, 0), new Vector3(1.9f, 0.7f, 4.3f), new Color(0.85f, 0.12f, 0.12f));
        CityBuilder.Box(go.transform, new Vector3(0, 1.05f, -0.3f), new Vector3(1.6f, 0.55f, 2.2f), new Color(0.1f, 0.12f, 0.15f));
        var c = go.AddComponent<CarController>();
        c.rb = body;
        return c;
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        float throttle = 0f, steer = 0f;
        if (occupied && kb != null)
        {
            if (kb.wKey.isPressed) throttle = 1f;
            if (kb.sKey.isPressed) throttle = -1f;
            if (kb.aKey.isPressed) steer -= 1f;
            if (kb.dKey.isPressed) steer += 1f;
        }
        Vector3 fwd = transform.forward;
        float fwdSpeed = Vector3.Dot(rb.linearVelocity, fwd);
        float latSpeed = Vector3.Dot(rb.linearVelocity, transform.right);

        // engine and brakes push toward the target speed
        float target = throttle > 0 ? MAX_FWD : throttle < 0 ? (fwdSpeed > 0.5f ? 0f : -MAX_REV) : 0f;
        float gain = throttle == 0 ? 0.4f : 2f;   // coasting slows gently
        float accel = Mathf.Clamp((target - fwdSpeed) * gain, -10f, 6f);
        rb.AddForce(fwd * accel * rb.mass);

        // tyre grip: no sliding sideways
        rb.AddForce(-transform.right * latSpeed * rb.mass * 8f);

        // steering only works while moving, and reverses when going backwards
        float turn = steer * 1.9f * Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 6f) * Mathf.Sign(fwdSpeed);
        rb.angularVelocity = Vector3.up * turn;
    }
}
