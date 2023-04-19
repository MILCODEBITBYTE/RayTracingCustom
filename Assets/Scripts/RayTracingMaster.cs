using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField]
    private ComputeShader RayTracingShader;
    [SerializeField]
    private Texture SkyboxTexture;
    [SerializeField]
    private Light DirectionalLight;

    [SerializeField, Range(0.0f, 1.9f)] private float _globalIllumination = 1.0f;
    [SerializeField, Range(1, 9)] private int _reflections;

    [Header("Spheres")]
    [SerializeField] private int _sphereSeed = 111;
    [SerializeField] private Vector2 _sphereRadius = new Vector2(3.0f, 8.0f);
    [SerializeField, Range(0, 200)] private int _spheresMax = 146;
    [SerializeField] private float _spherePlacementRadius = 100.0f;

    private RenderTexture _target;
    private RenderTexture _converged;
    private Camera _camera;
    private uint _currentSample = 0;
    private Material _addMaterial;
    private ComputeBuffer _sphereBuffer;


    //자동 콜 : 카메라 렌더링이 끝날때 마다.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();

        Render(destination);
    }

    private void OnEnable()
    {
        _currentSample = 0;
        SetupScene();
    }

    private void OnDisable()
    {
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        //결과 텍스쳐
        RayTracingShader.SetTexture(0, "Result", _target);

        float threadGroupSizeX = 8.0f;
        float threadGroupSizeY = 8.0f;
        int threadGroupsX = Mathf.CeilToInt(Screen.width / threadGroupSizeX);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / threadGroupSizeY);

        //처리
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //progressive smapling by addShader
        if (_addMaterial == null)
        {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }

        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);

        _currentSample++;
    }


    private void InitRenderTexture()
    {
        if (_converged == null || _converged.height != Screen.height || _converged.width != Screen.width)
        {
            _currentSample = 0;

            if (_converged != null)
            {
                _converged.Release();
            }

            _converged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
        }

        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            _currentSample = 0;
            //이미 할당된 텍스처가 있으면 비우기
            if (_target != null)
                _target.Release();

            //레이트레이싱용 render target 얻기
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }


    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);

        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        RayTracingShader.SetFloat("_GroundPlaneY", 0.0f);

        //progressive sampling
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        //Number of reflections
        RayTracingShader.SetInt("_Reflections", _reflections);

        //Directional  Light
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        //Spheres
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);

        //Randomness seed
        RayTracingShader.SetFloat("_Seed", Random.value);

        //GlobalIllumination
        RayTracingShader.SetFloat("_GlobalIllumination", _globalIllumination);
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _reflections = 8;
    }

    private void OnValidate()
    {
        _currentSample = 0;
        SetupScene();
    }

    private void Update()
    {
        //Reset _currentSample count if camera has moved
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }

        if (DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            DirectionalLight.transform.hasChanged = false;
        }
    }

    void Start()
    {

    }


    private void SetupScene()
    {
        Random.InitState(_sphereSeed);
        List<Sphere> spheres = new List<Sphere>();
        // Add a number of random spheres
        for (int i = 0; i < _spheresMax; i++)
        {
            Sphere sphere = new Sphere();
            // Create random position and radius
            sphere.radius = _sphereRadius.x + Random.value * (_sphereRadius.y - _sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * _spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                {
                    goto SkipSphere;
                }

            }

            // Albedo, specular color and smoothness and emission
            Color color = Random.ColorHSV();
            float chance = Random.value;
            if (chance < 0.85f)
            {
                bool metal = chance < 0.7f;
                sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                sphere.smoothness = Random.value;
            }
            else
            {
                Color emission = Random.ColorHSV(0, 1, 0, 1, 1.0f, 3.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }

            // Add the sphere to the list
            spheres.Add(sphere);
        SkipSphere:
            continue;
        }


        // Compute size of Sphere struct in bytes
        int strideSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere));
        // Create and ssign to compute buffer
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }
        _sphereBuffer = new ComputeBuffer(spheres.Count, strideSize);
        _sphereBuffer.SetData(spheres);
    }
}
