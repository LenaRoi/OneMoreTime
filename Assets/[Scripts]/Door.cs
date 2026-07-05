using UnityEngine;
using DG.Tweening;

public class Door : MonoBehaviour
{
    public bool isOpened = false;

    public void Opened()
    {
        if (isOpened) return;
        isOpened = true;
        transform.DORotate(new Vector3(0, 170, 0), 0.3f, RotateMode.LocalAxisAdd);
    }
}
