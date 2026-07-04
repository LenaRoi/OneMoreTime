using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public bool canMove = true;
    public bool canLook = true;
    public CameraHolderController cameraHolderController;

    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    public Obstacle targetObstacle;

    public int obstacleIndex = 0;

    public Animator animator;

    public CameraRewind rewing;

    public SkinnedMeshRenderer bodysmr;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (canMove) Move();

        if (Input.GetKeyDown(KeyCode.R))
        {
            GameManager.instance.isResetting = true;
            canMove = false;
            canLook = false;
            bodysmr.enabled = false;
            rewing.StartRewind();
        }
    }

    public void Move()
    {
        // Yerde mi kontrolü
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Eski Input Sistemi
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Zıplama

        animator.SetFloat("MoveX", x, 0.1f, Time.deltaTime * 10);
        animator.SetFloat("MoveY", z, 0.1f, Time.deltaTime * 10);
        if (Input.GetButtonDown("Jump") && isGrounded && targetObstacle == null)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Yer çekimi
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (targetObstacle != null && Input.GetKey(KeyCode.W) && Input.GetKeyDown(targetObstacle.key))
        {
            Jump();
        }
        if (targetObstacle != null && !Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
    public void Jump()
    {
        velocity = Vector3.zero;
        canMove = false;
        Obstacle currentObstacle = targetObstacle;
        obstacleIndex = currentObstacle.index;


        float dist = Vector3.Distance(transform.position, currentObstacle.entryPos.transform.position);

        if (obstacleIndex == 1)
        {
            transform.DOMove(currentObstacle.entryPos.position, dist / 6).SetEase(Ease.InQuad).OnComplete(() =>
            {
                animator.SetTrigger("Jump");
                transform.DOMove(currentObstacle.exitPos.position, 0.9f).SetEase(Ease.OutSine).OnComplete(() =>
                {
                    canMove = true;
                });
            });
        }

        else if (obstacleIndex == 2)
        {
            transform.DOMove(currentObstacle.entryPos.position, dist / 6).SetEase(Ease.InQuad).OnComplete(() =>
            {
                animator.SetTrigger("Slide");
                transform.DOMove(currentObstacle.exitPos.position, 0.75f).SetEase(Ease.OutSine).OnComplete(() =>
                {
                    canMove = true;
                });
            });
        }

        else if (obstacleIndex == 3)
        {
            cameraHolderController.GetInHead();
            canLook = false;
            transform.DOMove(currentObstacle.entryPos.position, dist / 6).SetEase(Ease.InQuad).OnComplete(() =>
            {
                animator.SetTrigger("BigJump");
                transform.DOMove(currentObstacle.exitPos.position, 1.65f).SetEase(Ease.OutSine).OnComplete(() =>
                {
                    cameraHolderController.GetOutHead();
                    canLook = true;
                    canMove = true;
                });
            });
        }

        else if (obstacleIndex == 4)
        {
            GameObject head = cameraHolderController.playerHead;
            Vector3 localPos = head.transform.localPosition;
            head.transform.DOLocalMoveZ(localPos.z - 0.2f, 0.1f);
            canLook = false;
            canMove = false;
            animator.SetTrigger("Climb");
            transform.DOMove(currentObstacle.entryPos.position, 0.63f).SetEase(Ease.Linear).OnComplete(() =>
            {
                transform.DOMove(currentObstacle.exitPos.position, 0.6f).SetEase(Ease.Linear).OnComplete(() =>
                {
                    head.transform.DOLocalMoveZ(localPos.z, 0.1f);
                    animator.SetFloat("MoveY", 0);
                    animator.SetFloat("MoveX", 0);
                    canLook = true;
                    canMove = true;
                });
            });
        }
        else if (obstacleIndex == 5)
        {
            canMove = false;
            transform.DOMove(currentObstacle.entryPos.position, dist / 6).SetEase(Ease.InQuad).OnComplete(() =>
            {
                animator.SetTrigger("WallWalkSide");
                transform.DOMove(currentObstacle.exitPos.position, 1f).SetEase(Ease.Linear).OnComplete(() =>
                {
                    animator.SetFloat("MoveY", 0);
                    animator.SetFloat("MoveX", 0);
                    canLook = true;
                    canMove = true;
                });
            });
        }
    }
}