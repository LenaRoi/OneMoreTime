using DG.Tweening.CustomPlugins;
using UnityEngine;

public class GameEndArea : MonoBehaviour
{
    public GameObject daft;
    public Animator animator;

    public GameObject camPivot;
    public GameObject camTarget;

    private void Start()
    {
        animator.SetTrigger("HipHop");
    }

    private void Update()
    {
        camPivot.transform.Rotate(0, 100 * Time.deltaTime, 0);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            GameManager.instance.GameOver();
        }
    }
}
