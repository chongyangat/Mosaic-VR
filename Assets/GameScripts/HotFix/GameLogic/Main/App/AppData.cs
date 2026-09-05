
namespace GameLogic
{
    public static class AppData 
    {
#if MANAGER_SERVER
        /// <summary>
        /// 用户名
        /// </summary>
        public static string UserName { get; set; } 
#endif
    }
}
