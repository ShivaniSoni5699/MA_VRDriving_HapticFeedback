using UnityEngine;

public class CollisionCounterTiny : MonoBehaviour
{
    public int count; //to keep viewing in inspector

    void OnCollisionEnter(Collision c)
    {
        count++;
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            Debug.Log($"[CollisionCounter] Total collisions this play: {count}");
    }
}
