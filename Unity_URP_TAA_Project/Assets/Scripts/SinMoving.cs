using System;
using UnityEngine;

public class SinMoving : MonoBehaviour
{
    [Range(0.0f, 10.0f)]
    public float speed = 1.0f;
    
    private Transform _transform;
    private Vector3 _originPos;
    
    private void Start()
    {
        _transform = GetComponent<Transform>();
        _originPos = _transform.position;
    }

    private void Update()
    {
        Vector3 newPos = _originPos;
        newPos.z += Mathf.Sin(Time.time * speed);
        _transform.position = newPos;
    }
}