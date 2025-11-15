using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrosshairControl : MonoBehaviour
{
    [SerializeField] private GameObject crosshair;

    private GameObject curCrosshair = null;

    void Start()
    {
    }
    void Update()
    {
        if (curCrosshair == null)
        {
            curCrosshair = Instantiate(crosshair, Vector3.zero, Quaternion.identity);
            return;
        }
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        curCrosshair.transform.position = mouseWorld;
    }
    void OnDestroy()
    {
        if (curCrosshair != null)
        {
            Destroy(curCrosshair);
        }
    }
}
