using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonEnabledTrigger : MonoBehaviour
{
    public ButtonOpener buttonOpener;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            buttonOpener.GoDaft();
        }
    }
}
