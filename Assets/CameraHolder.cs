using UnityEngine;

public class CameraHolderController : MonoBehaviour
{
    [Header("Hassasiyet ve Hız")]
    public float mouseSensitivity = 100f;
    public float moveSpeed = 10f;

    [Header("Bağlı Kamera")]
    [Tooltip("Holder'ın içindeki kamerayı buraya sürükleyin")]
    public Transform childCamera;

    private float xRotation = 0f;

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
        // 1. FARE GİRDİLERİNİ ALMA
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 2. YUKARI / AŞAĞI BAKMA (Sadece içerdeki Kamera döner)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Kafanın geriye takla atmasını engeller
        childCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 3. SAĞA / SOLA BAKMA (Holder objesinin kendisi döner)
        transform.Rotate(Vector3.up * mouseX);
    }   
}