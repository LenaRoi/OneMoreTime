using UnityEngine;
using DG.Tweening;

public class ButtonOpener : MonoBehaviour
{
    public bool openedOnce;

    public float closedYPos;
    public float openedYPos;
    public GameObject wall;

    public float speed = 5;

    public GameObject daft;
    public Animator animator;

    public Vector3 daftFirstPos;


    private void Start()
    {
        daftFirstPos = daft.transform.position;
        GameManager.instance.buttonOpeners.Add(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("Daft"))
        {
            OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player") || other.gameObject.CompareTag("Daft"))
        {
            CloseDoor();
        }
    }

    public void OpenDoor()
    {
        openedOnce = true;
        wall.transform.DOKill();
        wall.transform.DOMoveY(openedYPos, speed).SetSpeedBased(true);
    }

    public void CloseDoor()
    {
        wall.transform.DOKill();
        wall.transform.DOMoveY(closedYPos, speed).SetSpeedBased(true);
    }

    public void GoDaft()
    {
        if (!openedOnce || daft.gameObject.activeInHierarchy) return;

        GetComponent<Collider>().enabled = false;
        daft.gameObject.SetActive(true);
        animator.SetFloat("MoveY", 1);
        daft.transform.DOMove(new Vector3(transform.position.x, transform.position.y + 0.2f, transform.position.z), 2).SetEase(Ease.Linear).OnComplete(() =>
        {
            animator.SetFloat("MoveY", 0);
            animator.SetTrigger("Dance");
            OpenDoor();
        });
    }

    public void ResetDaft()
    {
        if (daft.gameObject.activeInHierarchy)
        {
            daft.gameObject.SetActive(false);
            daft.transform.position = daftFirstPos;
            CloseDoor();
        }
    }
}
