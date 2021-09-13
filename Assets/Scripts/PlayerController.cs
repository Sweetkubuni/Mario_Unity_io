using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed;
    public float jumpForce;
    public CharacterController controller;
    public Animator animator;

    private Vector3 moveDirection;
    public float gravityScale;
    private float jumpMomentum = -1f;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.Move(new Vector3(0f, -1f, 0f));
    }

    // Update is called once per frame
    void Update()
    {

        if (controller.isGrounded)
        {
            Debug.Log("is grounded");
            animator.SetBool("isgrounded", true);
            //if (Input.GetKeyDown("space"))
            //{
            //    animator.SetBool("isgrounded", false);
            //    jumpMomentum = Mathf.Sqrt(jumpForce);
            //}

            Vector3 move = new Vector3(0, jumpMomentum, Input.GetAxisRaw("Vertical") * Time.deltaTime);


            animator.SetBool("isrunning", (Input.GetAxisRaw("Vertical") * 10000) != 0.0);

            move = this.transform.TransformDirection(move);
            controller.Move(move * moveSpeed);

           controller.transform.Rotate(0, Input.GetAxisRaw("Horizontal") * 60 * Time.deltaTime, 0);
        }
    }
}
