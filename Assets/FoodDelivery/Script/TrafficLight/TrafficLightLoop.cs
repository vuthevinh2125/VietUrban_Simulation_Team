using System.Collections;
using UnityEngine;

public class TrafficLightLoop : MonoBehaviour
{
    [Header("North side")]
    public MeshRenderer greenLens;
    public MeshRenderer amberLens;
    public MeshRenderer redLens;

    [Header("South side")]
    public MeshRenderer greenLens1;
    public MeshRenderer amberLens1;
    public MeshRenderer redLens1;

    [Header("Turn left in north side")]
    public MeshRenderer turnLeftLens;

    [Header("ON Status")]
    public Material greenOnMat;
    public Material amberOnMat;
    public Material redOnMat;
    public Material turnLeftOnMat;

    [Header("OFF Status")]
    public Material greenOffMat;
    public Material amberOffMat;
    public Material redOffMat;
    public Material turnLeftOffMat;

    [Header("Duration")]
    public float greenDuration = 10f;
    public float amberDuration = 3f;
    public float redDuration = 15f;

    void Start()
    {
        StartCoroutine(LightCycle());
    }

    IEnumerator LightCycle()
    {
        while (true)
        {
            //turn on the red light for both side (North and South)
            SetLightState(redLens, redOnMat, redOffMat, true);
            SetLightState(amberLens, amberOnMat, amberOffMat, false);
            SetLightState(greenLens, greenOnMat, greenOffMat, false);

            SetLightState(redLens1, redOnMat, redOffMat, true);
            SetLightState(amberLens1, amberOnMat, amberOffMat, false);
            SetLightState(greenLens1, greenOnMat, greenOffMat, false);

            SetLightState(turnLeftLens, turnLeftOnMat, turnLeftOffMat, false);

            yield return new WaitForSeconds(redDuration);

            //turn on the green light and the left turn light
            SetLightState(redLens, redOnMat, redOffMat, false);
            SetLightState(amberLens, amberOnMat, amberOffMat, false);
            SetLightState(greenLens, greenOnMat, greenOffMat, true);

            SetLightState(redLens1, redOnMat, redOffMat, false);
            SetLightState(amberLens1, amberOnMat, amberOffMat, false);
            SetLightState(greenLens1, greenOnMat, greenOffMat, true);

            SetLightState(turnLeftLens, turnLeftOnMat, turnLeftOffMat, true);

            yield return new WaitForSeconds(greenDuration);

            //turn on the amber light
            SetLightState(redLens, redOnMat, redOffMat, false);
            SetLightState(amberLens, amberOnMat, amberOffMat, true);
            SetLightState(greenLens, greenOnMat, greenOffMat, false);

            SetLightState(redLens1, redOnMat, redOffMat, false);
            SetLightState(amberLens1, amberOnMat, amberOffMat, true);
            SetLightState(greenLens1, greenOnMat, greenOffMat, false);

            SetLightState(turnLeftLens, turnLeftOnMat, turnLeftOffMat, false);

            yield return new WaitForSeconds(amberDuration);
        }
    }
    
    void SetLightState(MeshRenderer lens, Material matOn, Material matOff, bool isOn)
    {
        if (lens != null)
        {
            Material[] mats = lens.materials;
            if (mats.Length > 1)
            {
                if (isOn)
                {
                    mats[1] = matOn;
                }
                else
                {
                    mats[1] = matOff;
                }
            }
            else
            {
                if (isOn)
                {
                    mats[0] = matOn;
                }
                else
                {
                    mats[0] = matOff;
                }
            }
            lens.materials = mats;
        }
    }
}