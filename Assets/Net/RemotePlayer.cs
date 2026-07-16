using UnityEngine;

namespace MarioNet
{
    // Interpolates a remote mario toward its last networked state and drives its animator.
    public class RemotePlayer : MonoBehaviour
    {
        [Range(1, 30)] public float positionLerpSpeed = 12f;
        [Range(1, 30)] public float rotationLerpSpeed = 12f;
        [Tooltip("Snap instead of lerp when the target is further than this")]
        public float teleportDistance = 10f;

        public uint Id { get; private set; }
        public string Username { get; private set; }

        Animator animator;
        TextMesh nameplate;
        Vector3 targetPos;
        float targetRotY;
        float animSpeed;
        float animVSpeed = 1f;
        bool grounded = true;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            var plate = transform.Find("Nameplate");
            if (plate != null)
                nameplate = plate.GetComponent<TextMesh>();
            targetPos = transform.position;
            targetRotY = transform.eulerAngles.y;
        }

        public void Init(uint id, string username)
        {
            Id = id;
            Username = username;
            if (nameplate != null)
                nameplate.text = username;
        }

        public void ApplyState(StateRecv state)
        {
            targetPos = new Vector3(state.x, state.y, state.z);
            targetRotY = state.ry;
            animSpeed = state.speed;
            animVSpeed = state.vspeed;
            grounded = state.grounded;
        }

        void Update()
        {
            if (Vector3.Distance(transform.position, targetPos) > teleportDistance)
                transform.position = targetPos;
            else
            {
                float t = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, t);
            }
            float rt = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.Euler(0, targetRotY, 0), rt);

            if (animator != null)
            {
                animator.SetFloat("latteralSpeed", animSpeed, 0.1f, Time.deltaTime);
                animator.SetFloat("verticalSpeed", animVSpeed, 0.1f, Time.deltaTime);
                animator.SetBool("isGrounded", grounded);
            }
        }

        void LateUpdate()
        {
            if (nameplate == null || Camera.main == null)
                return;
            var toCamera = nameplate.transform.position - Camera.main.transform.position;
            toCamera.y = 0;
            if (toCamera.sqrMagnitude > 0.001f)
                nameplate.transform.rotation = Quaternion.LookRotation(toCamera);
        }
    }
}
