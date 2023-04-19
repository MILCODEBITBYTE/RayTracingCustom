using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField]
    private ComputeShader RayTracingShader;
    [SerializeField]
    private Texture SkyboxTexture;

    private RenderTexture _target;
    private Camera _camera;


    //자동 콜 : 카메라 렌더링이 끝날때 마다.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();

        Render(destination);
    }


    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        //결과 텍스쳐
        RayTracingShader.SetTexture(0, "Result", _target);


        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        //처리
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);


        //결과 텍스쳐를 스크린에 그리기
        Graphics.Blit(_target, destination);
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
        }
    }


    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);

        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    void Start()
    {

    }
}

