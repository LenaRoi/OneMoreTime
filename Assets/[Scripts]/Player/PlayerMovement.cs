using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

    

    public SkinnedMeshRenderer bodysmr;
    public SkinnedMeshRenderer headsmr;

    public Door targetDoor;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (canMove) Move();
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
            float dist = Vector3.Distance(transform.position, targetObstacle.entryPos.transform.position);
            float targetDist = 2;
            if (targetObstacle.index == 6) targetDist = dist;
            if (dist <= targetDist) Jump();
        }
        if (targetObstacle != null && !Input.GetKey(KeyCode.W) && Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (targetDoor != null && (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
        {
            if (!targetDoor.isOpened) cameraHolderController.Shake();
            targetDoor.Opened();
        }
    }
    public void Jump()
    {
        UIManager.instance.CloseButton();
        velocity = Vector3.zero;
        canMove = false;
        Obstacle currentObstacle = targetObstacle;
        obstacleIndex = currentObstacle.index;

        if(!currentObstacle.immune)
        currentObstacle.GetComponent<Collider>().enabled = false;

        if (!currentObstacle.fakeObstacle)
        {
            GameManager.instance.AddScore();
        }

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

        else if (obstacleIndex == 6)
        {
            GameObject head = cameraHolderController.playerHead;
            Vector3 localPos = head.transform.localPosition;
            head.transform.DOLocalMoveZ(localPos.z - 0f, 0.1f);
            canMove = false;
            canLook = false;
            animator.SetTrigger("BarJump");
            transform.DOMove(currentObstacle.entryPos.position, 0.95f).SetEase(Ease.OutSine).OnComplete(() =>
            {
                transform.DOMove(currentObstacle.exitPos.position, 0.95f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    head.transform.DOLocalMoveZ(localPos.z, 0.1f);
                    animator.SetFloat("MoveY", 0);
                    animator.SetFloat("MoveX", 0);
                    canLook = true;
                    canMove = true;
                });
            });
        }
    }

    public void ResetPosition()
    {
        controller.enabled = false;

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0,0,0));

        // Kamerayı origin'e SNAP etme; doğru poza (playerHead + sıfır bakış) yerleştir.
        // Böylece deadloop sonrası kamera zıplaması (origin'den kafaya lerp) olmaz.
        // Bulanıklık artık burada değil, CameraRewind tarafından rewind bitmeden ~0.5sn önce tetikleniyor.
        cameraHolderController.ResetView();

        velocity = Vector3.zero;

        

        bodysmr.enabled = true;
        

        StartCoroutine(OneMoreTime());
    }

    IEnumerator OneMoreTime()
    {
        yield return new WaitForSeconds(0.1f);
        GameManager.instance.StartGame();
        controller.enabled = true;
        canMove = true;
        canLook = true;
    }

    public void GetHead()
    {
        headsmr.shadowCastingMode = ShadowCastingMode.On;
        animator.SetTrigger("HipHop");
    }
}