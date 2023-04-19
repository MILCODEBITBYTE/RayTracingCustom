using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private float _lastFieldOfView;
    private uint _currentSample = 0;
    private Material _addMaterial;
    private ComputeBuffer _sphereBuffer;
    private List<Transform> _transformsToWatch = new List<Transform>();
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
    }



    //자동 콜 : 카메라 렌더링이 끝날때 마다.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();

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

        if(_meshObjectBuffer != null)
        {
            _meshObjectBuffer.Release();
        }

        if(_vertexBuffer != null)
        {
            _vertexBuffer.Release();
        }

        if(_indexBuffer != null)
        {
            _indexBuffer.Release();
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void RebuildMeshObjectBuffers()
    {
        if(!_meshObjectsNeedRebuilding)
        {
            return;
        }

        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;

        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        foreach(RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            //Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);

            //Add index data - if the vertex buffer wasn't empty before, the indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(indexer => indexer + firstVertex));

            //Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }

        int strideSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshObject));
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, strideSize);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
        where T : struct
    {
        if(buffer != null)
        {
            if(data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if(data.Count != 0)
        {
            if(buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }

            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if(buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
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
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            //이미 할당된 텍스처가 있으면 비우기
            if (_target != null)
                _target.Release();

            //레이트레이싱용 render target 얻기
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();

            _converged = new RenderTexture(Screen.width, Screen.height, 0,
    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();

            // Reset sampling
            _currentSample = 0;
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

        //Randomness seed
        RayTracingShader.SetFloat("_Seed", Random.value);

        //GlobalIllumination
        RayTracingShader.SetFloat("_GlobalIllumination", _globalIllumination);


        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _reflections = 8;

        _transformsToWatch.Add(transform);
        _transformsToWatch.Add(DirectionalLight.transform);
    }

    private void OnValidate()
    {
        _currentSample = 0;
        SetupScene();
    }

    private void Update()
    {
        //Reset _currentSample count if camera has moved

        if(_camera.fieldOfView != _lastFieldOfView)
        {
            _currentSample = 0;
            _lastFieldOfView = _camera.fieldOfView;
        }

        foreach(Transform t in _transformsToWatch)
        {
            if(t.hasChanged)
            {
                t.hasChanged = false;
                _currentSample = 0;
            }
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
        if(spheres.Count > 0)
        {
            _sphereBuffer = new ComputeBuffer(spheres.Count, strideSize);
            _sphereBuffer.SetData(spheres);
        }
    }
}
