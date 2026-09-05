using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic
{
    /// <summary>
    /// 试验方案数据类
    /// 存储试验方案的完整信息，包括元数据和关联的数据文件列表
    /// </summary>
    [Serializable]
    public class ExperimentSchemeData
    {
        /// <summary>
        /// 方案版本号
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 试验名称
        /// </summary>
        public string ExperimentName { get; set; }

        /// <summary>
        /// 创建时间（ISO 8601 格式字符串）
        /// </summary>
        public string CreateTime { get; set; }

        /// <summary>
        /// 试验描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 关联的数据文件列表
        /// </summary>
        public List<DataFileInfo> DataFiles { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ExperimentSchemeData()
        {
            CreateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            DataFiles = new List<DataFileInfo>();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="experimentName">试验名称</param>
        /// <param name="description">试验描述</param>
        public ExperimentSchemeData(string experimentName, string description = "")
        {
            ExperimentName = experimentName;
            Description = description;
            CreateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            DataFiles = new List<DataFileInfo>();
        }

        /// <summary>
        /// 添加数据文件信息
        /// </summary>
        /// <param name="dataFileInfo">数据文件信息</param>
        public void AddDataFile(DataFileInfo dataFileInfo)
        {
            if (dataFileInfo != null && dataFileInfo.IsValid())
            {
                DataFiles.Add(dataFileInfo);
            }
        }

        /// <summary>
        /// 添加数据文件信息
        /// </summary>
        /// <param name="type">数据文件类型</param>
        /// <param name="path">数据文件路径</param>
        /// <param name="description">数据文件描述</param>
        public void AddDataFile(string type, string path, string description = "")
        {
            AddDataFile(new DataFileInfo(type, path, description));
        }

        /// <summary>
        /// 根据类型获取数据文件信息
        /// </summary>
        /// <param name="type">数据文件类型</param>
        /// <returns>数据文件信息列表</returns>
        public List<DataFileInfo> GetDataFilesByType(string type)
        {
            return DataFiles.Where(f => f.Type == type).ToList();
        }

        /// <summary>
        /// 根据类型获取第一个匹配的数据文件信息
        /// </summary>
        /// <param name="type">数据文件类型</param>
        /// <returns>数据文件信息，未找到则返回null</returns>
        public DataFileInfo GetDataFileByType(string type)
        {
            return DataFiles.FirstOrDefault(f => f.Type == type);
        }

        /// <summary>
        /// 获取所有数据文件的完整路径
        /// </summary>
        /// <param name="baseDirectory">基准目录</param>
        /// <returns>完整路径列表</returns>
        public List<string> GetAllDataFilePaths(string baseDirectory)
        {
            return DataFiles
                .Where(f => f.IsValid())
                .Select(f => f.GetFullPath(baseDirectory))
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();
        }

        /// <summary>
        /// 验证方案数据是否有效
        /// </summary>
        /// <returns>是否有效</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ExperimentName) && 
                   DataFiles != null && 
                   DataFiles.Count > 0;
        }

        /// <summary>
        /// 获取数据文件数量
        /// </summary>
        /// <returns>数据文件数量</returns>
        public int GetDataFileCount()
        {
            return DataFiles?.Count ?? 0;
        }
    }
}
