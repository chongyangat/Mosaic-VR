using UnityGameFramework.Runtime;

namespace GameLogic
{
    [EventInterface(EEventGroup.GroupUI)]
    public interface ITaskUI
    {
        public void OnTaskCompleted();

    }
}