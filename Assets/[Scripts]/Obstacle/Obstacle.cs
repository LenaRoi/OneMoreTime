using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public bool fakeObstacle;
    public int index = 0;

    public KeyCode key;

    public Transform entryPos;
    public Transform exitPos;

    public int buttonIndex;

    private void Start()
    {
        GameManager.instance.allObstacles.Add(this);
    }
}