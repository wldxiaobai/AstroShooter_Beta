using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugMethod : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.T))PlayerControl.GetHurt(1);
        if(Input.GetKeyDown(KeyCode.Y))PlayerControl.Heal(1);
    }
}
