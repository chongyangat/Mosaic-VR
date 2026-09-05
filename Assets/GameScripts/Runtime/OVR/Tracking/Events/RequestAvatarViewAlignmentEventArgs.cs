using GameFramework;
using GameFramework.Event;

namespace GameMain
{
    /// <summary>
    /// Requests an exact HMD alignment to the active motion-capture avatar.
    /// The runtime tracking layer raises this after a Meta system recenter has settled;
    /// the hot-update gameplay layer owns the avatar lookup and target-pose calculation.
    /// </summary>
    public sealed class RequestAvatarViewAlignmentEventArgs : GameEventArgs
    {
        public static readonly int EventId =
            typeof(RequestAvatarViewAlignmentEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Reason { get; private set; }

        public static RequestAvatarViewAlignmentEventArgs Create(string reason)
        {
            RequestAvatarViewAlignmentEventArgs args =
                ReferencePool.Acquire<RequestAvatarViewAlignmentEventArgs>();
            args.Reason = reason;
            return args;
        }

        public override void Clear()
        {
            Reason = null;
        }
    }

    /// <summary>
    /// Requests a local Meta tracking-origin recenter on the Quest. The tracking keeper
    /// marks this as an intentional system recenter so the resulting RecenteredPose event
    /// follows the same stabilization and avatar-alignment path as the user's hand gesture.
    /// </summary>
    public sealed class RequestOVRSystemRecenterEventArgs : GameEventArgs
    {
        public static readonly int EventId =
            typeof(RequestOVRSystemRecenterEventArgs).GetHashCode();

        public override int Id => EventId;

        public string Reason { get; private set; }

        public static RequestOVRSystemRecenterEventArgs Create(string reason)
        {
            RequestOVRSystemRecenterEventArgs args =
                ReferencePool.Acquire<RequestOVRSystemRecenterEventArgs>();
            args.Reason = reason;
            return args;
        }

        public override void Clear()
        {
            Reason = null;
        }
    }
}
