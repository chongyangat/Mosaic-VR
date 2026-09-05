
using System;

namespace GameMain
{
    public interface INetwork
    {
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="msg"></param>
        void Send(string msg);

        /// <summary>
        /// 接收消息
        /// </summary>
        event Action<string> OnReceive;

    }
}
