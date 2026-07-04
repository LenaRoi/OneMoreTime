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

    public bool isResetting = false;
}
