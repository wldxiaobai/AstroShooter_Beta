using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CloseCanvas : MonoBehaviour
{
    [SerializeField] private GameObject canvasToClose;

    public void CloseTheCanvas()
    {
        Destroy(canvasToClose);
    }
}
