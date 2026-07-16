using UnityEngine;

namespace MarioNet
{
    // Sits on the local mario and packages its state for NetworkManager to send.
    [RequireComponent(typeof(movement))]
    public class LocalPlayerSync : MonoBehaviour
    {
        movement mov;
        float lastY;
        float verticalSpeed;

        void Awake()
        {
            mov = GetComponent<movement>();
            lastY = transform.position.y;
        }

        void Update()
        {
            if (Time.deltaTime <= 0)
                return;
            float vy = (transform.position.y - lastY) / Time.deltaTime;
            lastY = transform.position.y;
            // Same mapping movement.cs feeds its own animator: clamp(vy,-1,1)+1
            verticalSpeed = Mathf.Clamp(vy, -1, 1) + 1;
        }

        public StateSend BuildState()
        {
            var pos = transform.position;
            return new StateSend
            {
                x = pos.x,
                y = pos.y,
                z = pos.z,
                ry = transform.eulerAngles.y,
                speed = mov.enabled ? mov.CurrentSpeed / mov.runningSpeed : 0,
                vspeed = verticalSpeed,
                grounded = mov._isGrounded
            };
        }
    }
}
