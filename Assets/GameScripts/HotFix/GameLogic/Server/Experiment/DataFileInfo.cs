using System;

namespace GameLogic
{
    /// <summary>
    /// 数据文件信息类
    /// 存储单个数据文件的元数据信息
    /// </summary>
    [Serializable]
    public class DataFileInfo
    {
        /// <summary>
        /// 数据文件类型（如：EyesTracking、GazeTracking）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 数据文件路径（支持相对路径和绝对路径）
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 数据文件描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DataFileInfo()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="type">数据文件类型</param>
        /// <param name="path">数据文件路径</param>
        /// <param name="description">数据文件描述</param>
        public DataFileInfo(string type, string path, string description = "")
        {
            Type = type;
            Path = path;
            Description = description;
        }

        /// <summary>
        /// 获取完整路径
        /// 将相对路径转换为基于基准目录的绝对路径
        /// </summary>
        /// <param name="baseDirectory">基准目录</param>
        /// <returns>完整路径</returns>
        public string GetFullPath(string baseDirectory)
        {
            if (string.IsNullOrEmpty(Path))
            {
                return string.Empty;
            }

            if (System.IO.Path.IsPathRooted(Path))
            {
                return Path;
            }

            return System.IO.Path.Combine(baseDirectory, Path);
        }

        /// <summary>
        /// 验证文件信息是否有效
        /// </summary>
        /// <returns>是否有效</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Path);
        }
    }
}
