using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public bool fakeObstacle;
    public int index = 0;

    public KeyCode key;

    public Transform entryPos;
    public Transform exitPos;

    public int buttonIndex;

    public bool immune = false;

    private void Start()
    {
        if (!immune)
        {
            GameManager.instance.allObstacles.Add(this);
        }
    }
}