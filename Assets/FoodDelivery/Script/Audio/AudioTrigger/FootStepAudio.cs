using StarterAssets;
using UnityEngine;

public class FootStepAudio : MonoBehaviour
{
    private FirstPersonController controller;
    private StarterAssetsInputs inputs;
    private AudioSource audioSource;
    [SerializeField] private AudioClip grassFootStep;
    [SerializeField] private AudioClip dirtFootStep;
    [SerializeField] private float walkTimerSound = 1;
    [SerializeField] private float runTimerSound = 0.4f;
    private float stepTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        controller = GetComponent<FirstPersonController>();
        inputs = GetComponent<StarterAssetsInputs>();
    }

    // Update is called once per frame
    void Update()
    {
        ManageFootStepSound();
    }
    private void ManageFootStepSound()
    {
        if (controller.Grounded == false)
        {
            return;
        }
        float currentTimer;
        if (inputs.sprint)
        {
            currentTimer = runTimerSound;
        }
        else
        {
            currentTimer = walkTimerSound;
        }
        stepTimer -= Time.deltaTime;
        if (stepTimer < 0 && inputs.move != Vector2.zero)
        {
            PlayFootStepSound();
            stepTimer = currentTimer;
        }
    }
    private void PlayFootStepSound()
    {
        if(!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            return;
        }
        Terrain terrain = hit.collider.GetComponent<Terrain>();
        if (terrain == null)
        {
            return;
        }
        TerrainData data = terrain.terrainData;

        //convert world position to terrain local position bcs terrain textture are stored in local terrain space not world
        Vector3 terrainPosition = hit.point - terrain.transform.position;

        //convert to alphamap coordinate bcs unity stores terrain textures insside an alphamap, not directly in world unit
        int mapx = Mathf.Clamp(Mathf.FloorToInt(terrainPosition.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1); //avoid stand in the edge of the terrain will notify errors
        int mapz = Mathf.Clamp(Mathf.FloorToInt(terrainPosition.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1); //bcs mapx mapzc exactly equal alphamapwidth

        //read the textures blending data at that position bcs terrain can mix textures and we need to know which one is the strongest
        float[,,] splatmap = data.GetAlphamaps(mapx, mapz, 1, 1);
        int textureIndex = 0;
        float strongest = 0f;
        
        //find the strongest texture
        for (int i = 0; i < splatmap.GetLength(2); i++)
        {
            if (splatmap[0, 0, i] > strongest)
            {
                strongest = splatmap[0, 0, i];
                textureIndex = i;
            }
        }
        //assign sound based on texture
        AudioClip clip = null;
        if (textureIndex == 0)
        {
            clip = grassFootStep;
        }
        else if(textureIndex == 1) 
        {
            clip = dirtFootStep;
        }
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
