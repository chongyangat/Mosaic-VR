using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaQuestProEyeGazePointDraw
{
    /// <summary>
    /// 让点始终看向相机
    /// </summary>
    public class Point : MonoBehaviour
    {
        private Transform cameraPos;
        private void Awake()
        {
            cameraPos = Camera.main.transform;
        }

        private void Update()
        {
            transform.LookAt(cameraPos);
        }
    }

}
