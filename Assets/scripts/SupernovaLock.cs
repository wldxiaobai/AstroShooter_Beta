using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SupernovaLock : MonoBehaviour
{
    [SerializeField] private string preLevel_1; //前置关卡名称1
    [SerializeField] private string preLevel_2; //前置关卡名称2
    [SerializeField] private GameObject canvasToOpen; //需要打开的消息画布

    public void FinalCheck()
    {
        LevelControl lc = LevelControl.Instance; //获取关卡控制单例
        LoadRoom lr = gameObject.GetComponent<LoadRoom>(); //获取加载房间组件

        //检查前置关卡是否完成其中之一
        if (lc.IsLevelCompleted(preLevel_1) || lc.IsLevelCompleted(preLevel_2))
        {
            //进入超新星关卡
            lr.GetLoading();
        }
        else
        {
            //显示信息
            Instantiate(canvasToOpen);
        }
    }
}
