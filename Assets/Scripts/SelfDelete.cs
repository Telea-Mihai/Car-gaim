using System.Collections;
using UnityEngine;

public class SelfDelete : MonoBehaviour
{
    public float timeToDestroy = 5f;
    public AudioClip[] softClips, hardClips;
    public float SoftHardMagnintudeThreshold = 0.5f;
    public float magnitude;
    public float baseVolume = 1f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.clip = (magnitude > SoftHardMagnintudeThreshold) ? hardClips[Random.Range(0, hardClips.Length)] : softClips[Random.Range(0, softClips.Length)];
        source.volume = baseVolume * (magnitude / SoftHardMagnintudeThreshold);
        source.pitch = Random.Range(0.8f, 1.2f);
        source.loop = false;
        source.Play();
    }

    // Update is called once per frame
    void Update()
    {
        StartCoroutine(DestroyAfterTime());
    }

    IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(timeToDestroy);
        Destroy(gameObject);
    }
}
