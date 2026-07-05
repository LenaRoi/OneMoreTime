using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    #region Singleton
    private void Awake()
    {
        if (instance == null) instance = this;
    }
    #endregion

    public AudioSource gameMusic;

    public bool isResetting = false;

    public float score = 1;

    public CameraRewind rewing;
    public PlayerMovement playerMovement;

    public List<ButtonOpener> buttonOpeners;

    private void Update()
    {
        if (!isResetting)
        {
            score -= 0.1f * Time.deltaTime;
            gameMusic.volume = score;
            if (score <= 0)
            {
                score = 0;
                ResetGame();
            }
        }
    }

    public void AddScore()
    {
        if (score < 0.4f) score = score + 0.4f;
        else score = 1;
    }

    public  void ResetGame()
    {
        isResetting = true;
        playerMovement.canMove = false;
        playerMovement.canLook = false;
        playerMovement.bodysmr.enabled = false;
        rewing.StartRewind();
    }

    public void ResetPlayer()
    {
        playerMovement.ResetPosition();
    }

    public void StartGame()
    {
        for (int i = 0; i < buttonOpeners.Count; i++)
        {
            buttonOpeners[i].ResetDaft();
        }
        isResetting = false;
        score = 1;
    }
}
