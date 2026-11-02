using UnityEngine;

namespace PortableAnimation
{
    /// <summary>
    /// Deterministic, optional demo motor with real vertical travel. It does NOT
    /// read root motion or create a second Animator graph. Game projects provide
    /// their own motor and pass its grounded/velocity data to the IK solver.
    /// </summary>
    public sealed class GroundingTestMotor
    {
        private readonly Transform root;
        private readonly FootGroundingProfile settings;
        private Vector3 origin;
        private float rootToGround;
        private bool started;
        private bool jumpRequested;
        private bool forceLand;
        private bool forceFall;
        private bool awaitingTakeoff;
        private float takeoffCountdown;

        public bool Grounded { get; private set; } = true;
        public float VerticalVelocity { get; private set; }
        public bool JumpEdge { get; private set; }

        public GroundingTestMotor(Transform root, FootGroundingProfile profile)
        {
            this.root = root;
            settings = profile;
            origin = root.position;
        }

        public void RequestJump() { jumpRequested = true; }
        public void RequestFall() { forceFall = true; }
        public void RequestLand() { forceLand = true; }

        public void Tick(float dt, float forward, float sideways)
        {
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            JumpEdge = false;
            if (!started)
            {
                started = true;
                if (Probe(out RaycastHit startHit))
                    rootToGround = root.position.y - startHit.point.y;
            }

            // Clamped movement, so the camera doesn't lose the character.
            Vector3 flat = root.position;
            flat += (root.forward * forward * settings.ForwardMetersPerSecond +
                root.right * sideways * settings.SidewaysMetersPerSecond) * dt;
            Vector3 offset = Vector3.ProjectOnPlane(flat - origin, Vector3.up);
            if (offset.magnitude > settings.MaxTravelFromOrigin)
                flat = origin + offset.normalized * settings.MaxTravelFromOrigin +
                    Vector3.up * (flat.y - origin.y);
            root.position = flat;

            if (jumpRequested && Grounded && !awaitingTakeoff)
            {
                // The authored JumpStart plays on the ground first.
                // Only after its windup do we move the character vertically.
                awaitingTakeoff = true;
                takeoffCountdown = settings.JumpWindupSeconds;
                JumpEdge = true;
            }
            jumpRequested = false;
            if (awaitingTakeoff)
            {
                takeoffCountdown -= dt;
                if (takeoffCountdown <= 0f)
                {
                    awaitingTakeoff = false;
                    Grounded = false;
                    VerticalVelocity = settings.JumpVelocity;
                }
            }
            if (forceFall)
            {
                forceFall = false;
                awaitingTakeoff = false;
                if (Grounded) root.position += Vector3.up * 0.2f;
                Grounded = false;
                VerticalVelocity = -Mathf.Max(1f, Mathf.Abs(VerticalVelocity));
            }
            if (forceLand)
            {
                forceLand = false;
                awaitingTakeoff = false;
                if (Probe(out RaycastHit forced))
                    root.position = new Vector3(root.position.x,
                        forced.point.y + rootToGround, root.position.z);
                Grounded = true;
                VerticalVelocity = 0f;
            }
            if (!Grounded)
            {
                VerticalVelocity -= settings.Gravity * dt;
                root.position += Vector3.up * VerticalVelocity * dt;
                if (VerticalVelocity <= 0f && Probe(out RaycastHit ground) &&
                    root.position.y <= ground.point.y + rootToGround)
                {
                    root.position = new Vector3(root.position.x,
                        ground.point.y + rootToGround, root.position.z);
                    VerticalVelocity = 0f;
                    Grounded = true;
                }
            }
        }

        private bool Probe(out RaycastHit hit)
        {
            Vector3 from = root.position + Vector3.up *
                (settings.ProbeHeight + 0.5f);
            return Physics.Raycast(from, Vector3.down, out hit,
                settings.ProbeDistance + 3f, settings.GroundLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
