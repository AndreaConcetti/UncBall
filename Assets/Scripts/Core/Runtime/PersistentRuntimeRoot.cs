using UnityEngine;

public class PersistentRuntimeRoot : MonoBehaviour
{
    private static PersistentRuntimeRoot instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}