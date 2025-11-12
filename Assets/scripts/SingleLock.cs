using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SingleLock : MonoBehaviour
{
    [SerializeField] private string preLevel; //Ç°ÖÃ¹Ø¿¨Ãû³Æ

    private void Awake()
    {
        gameObject.GetComponent<Button>().interactable = LevelControl.IsLevelCompleted(preLevel);
    }
}
