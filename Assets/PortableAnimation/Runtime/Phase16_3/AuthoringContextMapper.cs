using PortableAnimation.Authoring;

namespace PortableAnimation
{
    /// <summary>
    /// Shared runtime mapping between visual-authoring movement labels and the
    /// contextual motion states consumed by the animation graph.
    /// </summary>
    public static class AuthoringContextMapper
    {
        public static ContextualMotionState ToContext(AuthoringMovementContext context)
        {
            switch (context)
            {
                case AuthoringMovementContext.Start:
                    return ContextualMotionState.Starting;
                case AuthoringMovementContext.Walk:
                    return ContextualMotionState.Walking;
                case AuthoringMovementContext.Run:
                    return ContextualMotionState.Running;
                case AuthoringMovementContext.Stop:
                    return ContextualMotionState.Stopping;
                case AuthoringMovementContext.JumpStart:
                    return ContextualMotionState.JumpStarting;
                case AuthoringMovementContext.Rising:
                    return ContextualMotionState.Rising;
                case AuthoringMovementContext.Falling:
                    return ContextualMotionState.Falling;
                case AuthoringMovementContext.Landing:
                    return ContextualMotionState.Landing;
                case AuthoringMovementContext.Hover:
                    return ContextualMotionState.Hovering;
                case AuthoringMovementContext.Flight:
                    return ContextualMotionState.Flying;
                case AuthoringMovementContext.Stunned:
                    return ContextualMotionState.Stunned;
                case AuthoringMovementContext.KnockedDown:
                    return ContextualMotionState.KnockedDown;
                case AuthoringMovementContext.Dead:
                    return ContextualMotionState.Dead;
                default:
                    return ContextualMotionState.Idle;
            }
        }
    }
}
