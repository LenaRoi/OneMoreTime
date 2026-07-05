using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    public Image fill; 

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++) 
        {
            buttons[i].transform.DOScale(1.2f, 0.1f).SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void Update()
    {
        fill.fillAmount = GameManager.instance.score;
    }

    public List<GameObject> buttons;

    public void OpenButton(int index)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(false);
        }
        buttons[index].gameObject.SetActive(true);
    }

    public void CloseButton()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(false);
        }
    }

    public void OpenButtonDoor()
    {
        buttons[2].gameObject.SetActive(true);
    }

    public void CloseButtonDoor()
    {
        buttons[2].gameObject.SetActive(false);
    }
}
