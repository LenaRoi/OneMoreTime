using DG.Tweening;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class CameraHolderController : MonoBehaviour
{
    public PlayerMovement movement;

    [Header("Hassasiyet ve Hız")]
    public float mouseSensitivity = 100f;
    public float moveSpeed = 10f;

    [Header("Bağlı Kamera")]
    [Tooltip("Holder'ın içindeki kamerayı buraya sürükleyin")]
    public Transform childCamera;

    private float xRotation = 0f;
    private float yRotation = 0f;

    public GameObject playerHead;
    public LayerMask obstacleLayer;
    public LayerMask doorLayer;
    void Start()
    {
        // Fare imlecini ekrana kilitler ve gizler
        Cursor.lockState = CursorLockMode.Locked;

        // Eğer editörden kamera atanmadıysa, otomatik olarak alt objelerden bulmaya çalışır
        if (childCamera == null)
        {
            childCamera = GetComponentInChildren<Camera>().transform;
        }
    }

    void Update()
    {
        if (movement.canLook) Look();
    }

    void LateUpdate()
    {
        if (!GameManager.instance.isResetting)
        {
            Vector3 targetPosition = playerHead.transform.position;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                100 * Time.deltaTime
            );
        }
    }

    public void Look()
    {

        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 2f, obstacleLayer))
        {
            if (movement.targetObstacle != hit.transform.gameObject.GetComponent<Obstacle>())
            {
                movement.targetObstacle = hit.transform.gameObject.GetComponent<Obstacle>();
                UIManager.instance.OpenButton(movement.targetObstacle.buttonIndex);
            }
            
        }
        else
        {
            movement.targetObstacle = null;
            UIManager.instance.CloseButton();
        }

        Ray ray2 = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray2, out RaycastHit hit2, 2f, doorLayer))
        {
            if (movement.targetDoor != hit2.transform.gameObject.GetComponent<Door>())
            {
                movement.targetDoor = hit2.transform.gameObject.GetComponent<Door>();
                UIManager.instance.OpenButton(2);
            }
            
        }
        else
        {
            movement.targetDoor = null;
            UIManager.instance.CloseButton();
        }
        // 1. FARE GİRDİLERİNİ ALMA
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 2. YUKARI / AŞAĞI BAKMA (Sadece içerdeki Kamera döner)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -70f, 70f); // Kafanın geriye takla atmasını engeller

        childCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 3. SAĞA / SOLA BAKMA (Holder objesinin kendisi döner)
        movement.transform.Rotate(Vector3.up * mouseX);
        transform.Rotate(Vector3.up * mouseX);
    }

    public void GetInHead()
    {
        transform.SetParent(playerHead.transform);
    }

    public void GetOutHead()
    {
        transform.SetParent(null);
        transform.localRotation = Quaternion.Euler(0, movement.transform.rotation.eulerAngles.y, 0);
    }

    public void Shake()
    {
        transform.GetChild(0).transform.DOShakePosition(
            duration: 0.32f,
            strength: 0.8f,
            vibrato: 10,
            randomness: 30,
            snapping: false,
            fadeOut: true
        );
    }
}