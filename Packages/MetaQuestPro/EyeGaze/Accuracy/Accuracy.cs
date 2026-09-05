using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MetaQuestProGazeAccuracy
{
    public class Accuracy : MonoBehaviour
    {
        public MeshRenderer[] points;
        public Transform origin;
        public Vector3 gazePoint;
        private int pointId = 0;
        public UnityEvent<string> onDataSend;
        // Start is called before the first frame update
        void Start()
        {
            StartTest();
        }

        public void UpdateGazePoint(Vector3 gazePoint)
        {
            this.gazePoint = gazePoint;
        }

        private void StartTest()
        {
            NextPoint();
        }

        private void NextPoint()
        {
            //随机点
            int r = Random.Range(0, points.Length);
            pointId = r;
            foreach (var item in points)
            {
                item.enabled = false;
            }
            points[pointId].enabled = true;
            StartCoroutine("SendData");
        }

        IEnumerator SendData()
        {
            yield return new WaitForSeconds(1);
            float t = 0.8f;
            while (t > 0)
            {
                t -= 0.1f;

                //获取目标向量和视线向量
                Vector3 targetDir = points[pointId].transform.position - origin.position;
                Vector3 viewDir = gazePoint - origin.position;

                targetDir = targetDir.normalized;
                viewDir = viewDir.normalized;
                //算角度，发数据给电脑
                float angle = Vector3.Angle(targetDir, viewDir);
                onDataSend?.Invoke("TargetDir:" + targetDir + "    ViewDir:" + viewDir + "    Angle:" + angle);
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(0.2f);
            points[pointId].enabled = false;

            //NextPoint
            yield return new WaitForSeconds(1);
            NextPoint();
        }
    }

}

