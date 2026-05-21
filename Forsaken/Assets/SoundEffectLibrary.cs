using UnityEngine;
using System.Collections.Generic;
using System.Collections;


public class SoundEffectLibrary : MonoBehaviour
{
    [SerializeField] private SoundFxGroup[] soundFxGroups;
    private Dictionary<string, List<AudioClip>> soundDictionary;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        InitializeDictionary();
    }

    private void InitializeDictionary()
    {
        soundDictionary = new Dictionary<string, List<AudioClip>>();

        foreach (SoundFxGroup group in soundFxGroups)
        {
            if (!soundDictionary.ContainsKey(group.name))
            {
                soundDictionary[group.name] = group.audioClips;
            }
        }
    }

    public AudioClip GetRandomSound(string name)
    {
        if (soundDictionary.ContainsKey(name))
        {
            List<AudioClip> audioClips = soundDictionary[name];
            if(audioClips.Count > 0){
                return audioClips[Random.Range(0, audioClips.Count)];
            }
        
        }
        else
        {
            Debug.LogWarning($"Sound group '{name}' not found.");
            return null;
        }
        return null;
    }
    
}


[System.Serializable]
public struct SoundFxGroup{
    public string name;
    public List<AudioClip> audioClips;
}
