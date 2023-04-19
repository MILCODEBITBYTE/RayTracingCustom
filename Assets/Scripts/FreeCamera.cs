using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    // Start is called before the first frame update

    [SerializeField]
    public float MoveSpeed = 10.0f;
    [SerializeField]
    public float LookSpeed = 760.0f;

    private bool bOnR2 = false;

    float fRotateX = 0;
    float fRotateY = 0;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.A))
        {
            transform.position = transform.position + (-transform.right * MoveSpeed * Time.deltaTime);
        }

        if(Input.GetKey(KeyCode.D))
        {
            transform.position = transform.position + (transform.right * MoveSpeed * Time.deltaTime);
        }

        if(Input.GetKey(KeyCode.W))
        {
            transform.position = transform.position + (transform.forward * MoveSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.S))
        {
            transform.position = transform.position + (-transform.forward * MoveSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.R))
        {
            transform.position = transform.position + (transform.up * MoveSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.F))
        {
            transform.position = transform.position + (-transform.up * MoveSpeed * Time.deltaTime);
        }

        if(Input.GetKeyDown(KeyCode.Mouse1))
        {
            bOnR2 = true;
        }
        else if(Input.GetKeyUp(KeyCode.Mouse1))
        {
            bOnR2 = false;
        }

        if(bOnR2)
        {
            float fRX = -Input.GetAxis("Mouse Y") * Time.deltaTime * LookSpeed * 10;
            float fRY = Input.GetAxis("Mouse X") * Time.deltaTime  * LookSpeed * 10;


            fRotateX = transform.localEulerAngles.x + fRX;
            fRotateY = transform.localEulerAngles.y + fRY;


            transform.localEulerAngles = new Vector3(fRotateX, fRotateY, 0);
        }



    }
}
