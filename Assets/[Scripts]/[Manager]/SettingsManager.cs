using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;
    private void Awake()
    {
        if (instance == null) instance = this;

        DontDestroyOnLoad(instance);
    }
    public float volume;
    public float mouseSens;
}
