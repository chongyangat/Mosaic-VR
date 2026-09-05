using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameLogic
{
    /// <summary>
    /// 试验方案管理器
    /// 负责试验方案文件的加载、保存和管理
    /// </summary>
    public class ExperimentSchemeManager
    {
        private static ExperimentSchemeManager s_Instance;
        public static ExperimentSchemeManager Instance => s_Instance ??= new ExperimentSchemeManager();

        private ExperimentSchemeManager()
        {
        }

        /// <summary>
        /// 从文件加载试验方案
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <returns>试验方案数据，加载失败返回null</returns>
        public ExperimentSchemeData LoadScheme(string schemeFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(schemeFilePath))
                {
                    Log.Error("ExperimentSchemeManager: 方案文件路径为空");
                    return null;
                }

                if (!File.Exists(schemeFilePath))
                {
                    Log.Error("ExperimentSchemeManager: 方案文件不存在: {0}", schemeFilePath);
                    return null;
                }

                string jsonContent = File.ReadAllText(schemeFilePath);
                ExperimentSchemeData schemeData = JsonConvert.DeserializeObject<ExperimentSchemeData>(jsonContent);

                if (schemeData == null)
                {
                    Log.Error("ExperimentSchemeManager: 解析方案文件失败: {0}", schemeFilePath);
                    return null;
                }

                if (!schemeData.IsValid())
                {
                    Log.Warning("ExperimentSchemeManager: 方案数据无效: {0}", schemeFilePath);
                    return null;
                }

                Log.Info("ExperimentSchemeManager: 成功加载试验方案 '{0}', 包含 {1} 个数据文件",
                    schemeData.ExperimentName, schemeData.GetDataFileCount());

                return schemeData;
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 加载方案文件异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 保存试验方案到文件
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <param name="schemeData">试验方案数据</param>
        /// <returns>是否保存成功</returns>
        public bool SaveScheme(string schemeFilePath, ExperimentSchemeData schemeData)
        {
            try
            {
                if (string.IsNullOrEmpty(schemeFilePath))
                {
                    Log.Error("ExperimentSchemeManager: 方案文件路径为空");
                    return false;
                }

                if (schemeData == null)
                {
                    Log.Error("ExperimentSchemeManager: 试验方案数据为空");
                    return false;
                }

                if (!schemeData.IsValid())
                {
                    Log.Error("ExperimentSchemeManager: 试验方案数据无效");
                    return false;
                }

                string directory = Path.GetDirectoryName(schemeFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.Info("ExperimentSchemeManager: 创建目录: {0}", directory);
                }

                string jsonContent = JsonConvert.SerializeObject(schemeData, Formatting.Indented);
                File.WriteAllText(schemeFilePath, jsonContent);

                Log.Info("ExperimentSchemeManager: 成功保存试验方案 '{0}' 到文件: {1}",
                    schemeData.ExperimentName, schemeFilePath);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 保存方案文件异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取方案文件所在目录
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <returns>目录路径，失败返回空字符串</returns>
        public string GetSchemeDirectory(string schemeFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(schemeFilePath))
                {
                    return string.Empty;
                }

                return Path.GetDirectoryName(schemeFilePath) ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 获取方案目录异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取方案关联的所有数据文件完整路径
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <returns>数据文件完整路径列表</returns>
        public string[] GetDataFilePaths(string schemeFilePath)
        {
            try
            {
                ExperimentSchemeData schemeData = LoadScheme(schemeFilePath);
                if (schemeData == null)
                {
                    return Array.Empty<string>();
                }

                string baseDirectory = GetSchemeDirectory(schemeFilePath);
                return schemeData.GetAllDataFilePaths(baseDirectory).ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 获取数据文件路径异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// 根据类型获取方案关联的数据文件完整路径
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <param name="fileType">数据文件类型</param>
        /// <returns>数据文件完整路径，未找到返回空字符串</returns>
        public string GetDataFilePathByType(string schemeFilePath, string fileType)
        {
            try
            {
                ExperimentSchemeData schemeData = LoadScheme(schemeFilePath);
                if (schemeData == null)
                {
                    return string.Empty;
                }

                DataFileInfo fileInfo = schemeData.GetDataFileByType(fileType);
                if (fileInfo == null)
                {
                    Log.Warning("ExperimentSchemeManager: 未找到类型为 '{0}' 的数据文件", fileType);
                    return string.Empty;
                }

                string baseDirectory = GetSchemeDirectory(schemeFilePath);
                string fullPath = fileInfo.GetFullPath(baseDirectory);

                if (!File.Exists(fullPath))
                {
                    Log.Warning("ExperimentSchemeManager: 数据文件不存在: {0}", fullPath);
                    return string.Empty;
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 根据类型获取数据文件路径异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 验证方案文件中的所有数据文件是否存在
        /// </summary>
        /// <param name="schemeFilePath">方案文件路径</param>
        /// <returns>是否所有数据文件都存在</returns>
        public bool ValidateDataFiles(string schemeFilePath)
        {
            try
            {
                string[] filePaths = GetDataFilePaths(schemeFilePath);
                if (filePaths.Length == 0)
                {
                    return false;
                }

                foreach (string filePath in filePaths)
                {
                    if (!File.Exists(filePath))
                    {
                        Log.Warning("ExperimentSchemeManager: 数据文件不存在: {0}", filePath);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("ExperimentSchemeManager: 验证数据文件异常: {0}, 错误: {1}", schemeFilePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 创建新的试验方案
        /// </summary>
        /// <param name="experimentName">试验名称</param>
        /// <param name="description">试验描述</param>
        /// <returns>新的试验方案数据</returns>
        public ExperimentSchemeData CreateScheme(string experimentName, string description = "")
        {
            return new ExperimentSchemeData(experimentName, description);
        }
    }
}
