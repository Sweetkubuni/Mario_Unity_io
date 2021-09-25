using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class movement : MonoBehaviour
{
    #region general
    Animator animator;
    CharacterController controller;
    float DeltaTime { get => Time.deltaTime; }
    private void OnValidate()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogError("no children has an Animator component attached");
        if (walkingSpeed > runningSpeed)
            Debug.LogError("walkingSpeed is higher than runningSpeed");
    }
    private void Awake()
    {
        cameraTrans = Camera.main.transform;
        if (terrainLayerMask == 0)
            Debug.LogError("terrainLeyaerMask set to Nothing");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        canJummpAgain = true;
    }
    void Update()
    {
        LatteralMovement();
        Jump();
        UpdateGravityGrounded();
    }
    #endregion

    [Header("Latteral movement")]
    #region
    Vector3 latteralDir;
    Transform cameraTrans;
    [Range(1, 10)] public float walkingSpeed = 5;
    [Range(5, 20)] public float runningSpeed = 10;
    float _currentSpeed;
    [HideInInspector] public float CurrentSpeed
    {
        get => _currentSpeed;
        set
        {
            _currentSpeed = value;
            animator.SetFloat("latteralSpeed", CurrentSpeed / runningSpeed, 0.1f, DeltaTime);
        }
    }
    float turnSmoothVelocity;
    void LatteralMovement()
    {
        CurrentSpeed = 0;
        latteralDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
        if (latteralDir.magnitude != 0)
        {
            latteralDir.Normalize();
            var targetAngle = Mathf.Atan2(latteralDir.x, latteralDir.z) * Mathf.Rad2Deg + cameraTrans.eulerAngles.y;
            var actualAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, 0.1f);
            transform.rotation = Quaternion.Euler(0f, actualAngle, 0f);
            latteralDir = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;
            CurrentSpeed = Input.GetKey(KeyCode.LeftShift) && IsGrounded ? runningSpeed : walkingSpeed;
            latteralDir *= CurrentSpeed * DeltaTime;
            controller.Move(latteralDir);
        }
    }
    #endregion

    [Header("jumps")]
    #region
    bool canJummpAgain = true;
    public float jumpHeight = 2;
    void Jump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && (IsGrounded || canJummpAgain))
        {
            canJummpAgain = IsGrounded;
            Velocity.y = Mathf.Sqrt(jumpHeight * 2f * -gravity);
            Velocity += latteralDir * 15f;
            PlayRandom(jumpAudioClips);
            if (!IsGrounded)
                animator.SetTrigger("double jump");
            IsGrounded = false;
        }
    }
    #endregion

    [Header("velocity and grounded")]
    #region
    const float smallDist = 0.014f;
    Vector3 Velocity;
    const float gravity = -9.81f;
    public bool _isGrounded;
    bool IsGrounded
    {
        get => _isGrounded;
        set 
        {
            _isGrounded = value; 
            animator.SetBool("isGrounded", value);
            canJummpAgain = value ? true : canJummpAgain;
        }
    }
    [SerializeField] LayerMask terrainLayerMask;
    /// <summary>
    /// Updates gravity and isGrounded.
    /// </summary>
    float FeetRadius { get => controller.radius - smallDist / 1.5f; }
    Vector3 FeetPos { get => transform.position + controller.center + Vector3.down * (controller.height / 2f +  smallDist - FeetRadius); }
    private void UpdateGravityGrounded()
    {
        controller.Move(Velocity * DeltaTime);
        IsGrounded = Physics.CheckSphere(FeetPos, FeetRadius, terrainLayerMask);
        if (IsGrounded)
            Velocity = Vector3.down * 2f;
        else
            ApplyForce(DeltaTime * gravity * Vector3.up);
        animator.SetFloat("verticalSpeed", Mathf.Clamp(Velocity.y, -1, 1) + 1, 0.1f, DeltaTime);
    }
    public void ApplyForce(Vector3 force) => Velocity += force;
    #endregion

    [Header("Audio")]
    #region
    [SerializeField] AudioClip[] jumpAudioClips;
    AudioSource audioSource;
    void PlayRandom(AudioClip[] audioClips) => audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
    #endregion

    [Header("Debug")]
    #region
    [SerializeField] bool debug;
    private void OnDrawGizmos()
    {
        if (!debug)
            return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(FeetPos, FeetRadius);
    }
    #endregion
}
