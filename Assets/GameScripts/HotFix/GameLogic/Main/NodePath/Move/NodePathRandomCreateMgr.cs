using UnityEngine;
using System.Collections.Generic;

namespace VBSOED
{
    public class NodePathRandomCreateMgr : MonoBehaviour
    {

        [Header("Objects")]
        [Tooltip("创建对象随机")]
        [SerializeField] private bool m_IsRndCreateObjs = true;

        [SerializeField] private List<CarObject> objectList = new List<CarObject>();
        [SerializeField] private List<NodeLine> lineList = new List<NodeLine>();
        [SerializeField] private float createIntervalMin = 5;
        [SerializeField] private float createIntervalMax = 10;
        [SerializeField] private bool randomAllPath = false;

        private float createInterval;
        float time = 0;
        private void Update()
        {
            time += Time.deltaTime;
            if (time > createInterval) 
            {
                createInterval = UnityEngine.Random.Range(createIntervalMin, createIntervalMax);
                time = 0;
                if (randomAllPath)
                    CreateObject();
                else 
                {
                    var pcount = lineList.Count;
                    for (int i = 0; i < pcount; i++) 
                    {
                        var obj = CreateCar();
                        var path = lineList[i];
                        obj.Move(path, () =>
                        {
                            GameObject.Destroy(obj.gameObject);
                        });
                    }
                }
            }
        }

        private CarObject CreateObject() 
        {
            int count = objectList.Count;
            var index = UnityEngine.Random.Range(0, count);
            CarObject prefab = objectList[index];
            var obj = GameObject.Instantiate(prefab, transform);
            obj.gameObject.SetActive(true);
            var pcount = lineList.Count;
            var pindex = UnityEngine.Random.Range(0, pcount);
            var path = lineList[pindex];
            obj.Move(path, () => 
            {
                GameObject.Destroy(obj.gameObject);
            });
            return obj;
        }

        /// <summary>
        /// 当前创建对象索引
        /// </summary>
        private int m_CurObjCreateIndex = -1;

        private CarObject CreateCar()
        {
            int count = objectList.Count;
            // 随机
            if (m_IsRndCreateObjs)
            {
                m_CurObjCreateIndex = Random.Range(0, count);
            }
            // 循环
            else
            {
                m_CurObjCreateIndex++;
                if (m_CurObjCreateIndex >= count)
                    m_CurObjCreateIndex = 0;
            }
            CarObject prefab = objectList[m_CurObjCreateIndex];
            var obj = GameObject.Instantiate(prefab, transform);
            obj.gameObject.SetActive(true);
            return obj;
        }
    }
}
