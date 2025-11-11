using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    // Update is called once per frame
    void Update()
    {
        // 缓冲变量
        float moveHorizontal = 0;
        float moveVertical = 0;
        
        if (Input.GetKey(KeyCode.A)) { moveHorizontal -= 1; }
        if (Input.GetKey(KeyCode.D)) { moveHorizontal += 1; }
        if (Input.GetKey(KeyCode.W)) { moveVertical += 1; }
        if (Input.GetKey(KeyCode.S)) { moveVertical -= 1; } // 预留：修改键位功能

        //应用缓冲
        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        movement.Normalize();
        this.gameObject.GetComponent<Rigidbody2D>().velocity = movement * speed;
    }
}
