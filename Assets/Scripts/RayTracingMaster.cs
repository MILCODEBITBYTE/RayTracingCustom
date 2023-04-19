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


    //�ڵ� �� : ī�޶� �������� ������ ����.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();

        Render(destination);
    }


    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        //��� �ؽ���
        RayTracingShader.SetTexture(0, "Result", _target);


        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        //ó��
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);


        //��� �ؽ��ĸ� ��ũ���� �׸���
        Graphics.Blit(_target, destination);
    }


    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            //�̹� �Ҵ�� �ؽ�ó�� ������ ����
            if (_target != null)
                _target.Release();

            //����Ʈ���̽̿� render target ���
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

