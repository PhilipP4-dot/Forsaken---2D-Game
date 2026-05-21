using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance{ get; private set;}
    private  AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        Instance = this;
    
    }
    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            source.PlayOneShot(clip);
        }
    }
}
