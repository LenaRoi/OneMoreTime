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
            Debug.Log(hit.transform.gameObject);
            movement.targetObstacle = hit.transform.gameObject.GetComponent<Obstacle>();
        }
        else
        {
            movement.targetObstacle = null;
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
}