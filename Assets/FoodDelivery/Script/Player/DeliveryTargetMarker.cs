using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class DeliveryTargetMarker : MonoBehaviour
{
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool showHudArrow;
    [SerializeField, Min(0f)] private float edgePadding = 48f;
    [SerializeField] private float rotationOffset = -90f;
    [SerializeField, Min(2f)] private float beamHeight = 12f;
    [SerializeField, Min(0.1f)] private float beamTopRadius = 1.5f;
    [SerializeField, Min(0.05f)] private float beamBottomRadius = 0.3f;
    [SerializeField] private Color beamColor = new Color(1f, 0.78f, 0.28f, 0.18f);
    [SerializeField, Min(0f)] private float spotlightIntensity = 2f;

    private Transform target;
    private Transform beamTarget;
    private Image markerImage;
    private GameObject beamObject;
    private Mesh beamMesh;
    private Material beamMaterial;
    private Light spotlight;

    private void Awake()
    {
        if (marker == null)
        {
            marker = transform as RectTransform;
        }
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        if (canvasRect == null && marker != null)
        {
            Canvas canvas = marker.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
            }
        }

        if (marker != null)
        {
            markerImage = marker.GetComponent<Image>();
            if (markerImage != null)
            {
                markerImage.enabled = false;
            }
        }

        CreateBeamVisual();
    }

    public void SetTarget(Transform newTarget)
    {
        SetTarget(newTarget, newTarget);
    }

    public void SetTarget(Transform newTarget, Transform newBeamTarget)
    {
        target = newTarget;
        beamTarget = newBeamTarget;
        if (beamTarget == null)
        {
            beamTarget = target;
        }

        bool hasTarget = target != null;
        if (markerImage != null)
        {
            markerImage.enabled = showHudArrow && hasTarget;
        }
        SetBeamVisible(hasTarget && beamTarget != null);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            SetBeamVisible(false);
            return;
        }

        if (beamTarget != null && beamObject != null)
        {
            beamObject.transform.position = beamTarget.position;
        }
        else
        {
            SetBeamVisible(false);
        }

        if (showHudArrow == false || marker == null || canvasRect == null || targetCamera == null)
        {
            return;
        }

        Vector3 screenPosition = targetCamera.WorldToScreenPoint(target.position);
        if (screenPosition.z < 0f)
        {
            screenPosition.x = Screen.width - screenPosition.x;
            screenPosition.y = Screen.height - screenPosition.y;
        }
        screenPosition.x = Mathf.Clamp(screenPosition.x, edgePadding, Screen.width - edgePadding);
        screenPosition.y = Mathf.Clamp(screenPosition.y, edgePadding, Screen.height - edgePadding);
        Vector2 localPosition;
        Camera canvasCamera = null;
        Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = canvas.worldCamera;
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, canvasCamera, out localPosition))
        {
            marker.anchoredPosition = localPosition;
        }
        Vector3 directionFromCenter = screenPosition - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        if (directionFromCenter.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x) * Mathf.Rad2Deg;
            marker.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }
    }

    private void CreateBeamVisual()
    {
        beamMaterial = CreateBeamMaterial();
        if (beamMaterial == null)
        {
            return;
        }

        beamObject = new GameObject("delivery target beam");
        beamMesh = CreateBeamMesh();
        MeshFilter meshFilter = beamObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = beamMesh;
        MeshRenderer meshRenderer = beamObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = beamMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        GameObject spotlightObject = new GameObject("delivery target spotlight");
        spotlightObject.transform.SetParent(beamObject.transform, false);
        spotlightObject.transform.localPosition = Vector3.up * beamHeight;
        spotlightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        spotlight = spotlightObject.AddComponent<Light>();
        spotlight.type = LightType.Spot;
        spotlight.color = beamColor;
        spotlight.intensity = spotlightIntensity;
        spotlight.range = beamHeight + 2f;
        spotlight.spotAngle = 25f;
        spotlight.shadows = LightShadows.None;

        beamObject.SetActive(false);
    }

    private Material CreateBeamMaterial()
    {
        Shader beamShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (beamShader == null)
        {
            beamShader = Shader.Find("Unlit/Color");
        }
        if (beamShader == null)
        {
            return null;
        }

        Material material = new Material(beamShader);
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", beamColor);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", beamColor);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        return material;
    }

    private Mesh CreateBeamMesh()
    {
        const int segments = 24;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int index = 0; index <= segments; index++)
        {
            float angle = Mathf.PI * 2f * index / segments;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            vertices.Add(new Vector3(x * beamBottomRadius, 0f, z * beamBottomRadius));
            vertices.Add(new Vector3(x * beamTopRadius, beamHeight, z * beamTopRadius));
        }

        for (int index = 0; index < segments; index++)
        {
            int first = index * 2;
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first + 3);
            triangles.Add(first + 2);
        }

        Mesh mesh = new Mesh();
        mesh.name = "delivery target beam mesh";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    private void SetBeamVisible(bool isVisible)
    {
        if (beamObject != null && beamObject.activeSelf != isVisible)
        {
            beamObject.SetActive(isVisible);
        }
    }

    private void OnDisable()
    {
        SetBeamVisible(false);
    }

    private void OnEnable()
    {
        if (target != null && beamTarget != null)
        {
            SetBeamVisible(true);
        }
    }

    private void OnDestroy()
    {
        if (beamMesh != null)
        {
            Destroy(beamMesh);
        }
        if (beamMaterial != null)
        {
            Destroy(beamMaterial);
        }
        if (beamObject != null)
        {
            Destroy(beamObject);
        }
    }
}
