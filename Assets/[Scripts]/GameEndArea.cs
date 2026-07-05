using UnityEngine;

public class GameEndArea : MonoBehaviour
{
    public GameObject daft;
    public Animator animator;

    private void Start()
    {
        animator.SetTrigger("HipHop");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            GameManager.instance.GameOver();
        }
    }
}
