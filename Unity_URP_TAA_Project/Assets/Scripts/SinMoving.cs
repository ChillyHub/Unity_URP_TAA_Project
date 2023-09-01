using System;
using Unity.Mathematics;
using UnityEngine;

public class SinMoving : MonoBehaviour
{
    [Range(0.0f, 10.0f)]
    public float speed = 1.0f;

    [Range(0.0f, 10.0f)] 
    public float distance = 1.0f;
    
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
        newPos.z += Mathf.Sin(Time.time * speed) * distance;
        newPos.x += Mathf.Sin(Time.time * speed) * distance;
        _transform.position = newPos;
    }
}