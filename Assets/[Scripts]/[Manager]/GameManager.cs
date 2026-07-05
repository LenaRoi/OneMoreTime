using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
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
    [Tooltip("Deadloop sonrası müziği loopStartTime'dan başlatmak için. Boşsa gameMusic'ten alınır.")]
    public MusicPlayer musicPlayer;
    [Tooltip("Deadloop sonrası 1sn bulanık->net açılış. Boşsa sahnede/Main Camera'da aranır.")]
    public ScreenBlurFade blurFade;

    public bool gameOver = false;

    public bool isResetting = false;

    public float score = 1;

    public CameraRewind rewing;
    public PlayerMovement playerMovement;

    public List<ButtonOpener> buttonOpeners;
    public List<Obstacle> allObstacles;

    private void Update()
    {
        if (!isResetting && !gameOver)
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

        // Deadloop (ölüm) animasyonu bittikten sonra müziği loopStartTime'dan (30sn) başlat.
        // Referansı sağlamlaştır: önce Inspector alanı, sonra gameMusic objesi, en son sahnede ara.
        if (musicPlayer == null && gameMusic != null)
            musicPlayer = gameMusic.GetComponent<MusicPlayer>();
        if (musicPlayer == null)
            musicPlayer = FindObjectOfType<MusicPlayer>();
        if (musicPlayer != null)
            musicPlayer.RestartFromLoop();
        else
            Debug.LogWarning("GameManager: Sahnede MusicPlayer bulunamadı; deadloop sonrası müzik 30sn'den başlatılamadı.", this);

        for (int i = 0; i < allObstacles.Count; i++)
        {
            allObstacles[i].GetComponent<Collider>().enabled = true;
        }

    }

    /// <summary>
    /// Bulanık geçişi başlatır: <paramref name="fadeIn"/> saniyede bulanıklaşır, sonra
    /// <paramref name="focusOut"/> saniyede nete açılır. CameraRewind tarafından deadloop
    /// BİTMEDEN ~0.5sn önce tetiklenir; böylece tam bulanık an reset karesine denk gelir
    /// ve kamera zıplaması görünmez.
    /// </summary>
    public void PlayRespawnBlur(float fadeIn = 0.5f, float focusOut = 1f)
    {
        if (blurFade == null)
            blurFade = FindObjectOfType<ScreenBlurFade>();
        if (blurFade == null && Camera.main != null)
            blurFade = Camera.main.GetComponent<ScreenBlurFade>();
        if (blurFade != null)
            blurFade.PlayFadeFocus(fadeIn, focusOut);
    }

    public void GameOver()
    {
        gameOver = true;
        score = 1;
    }
}
