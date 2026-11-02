using UnityEngine;

namespace PortableAnimation
{
    public static class GroundMotionCycleMath
    {
        // All directional run cycles sample the same phase; independently
        // authored clips can offset their foot-strike phase in the Inspector.
        public static float Wrap(float value) => Mathf.Repeat(value, 1f);
        public static float Sample(float sharedPhase, GroundPose pose, Vector4 offsets)
        {
            float offset = pose == GroundPose.MoveForward ? offsets.x :
                pose == GroundPose.MoveBackward ? offsets.y :
                pose == GroundPose.MoveLeft ? offsets.z :
                pose == GroundPose.MoveRight ? offsets.w : 0f;
            return Wrap(sharedPhase + offset);
        }


        // Clockwise 8-way order: F, FR, R, BR, B, BL, L, FL.
        // Diagonal clips inherit a wrapped midpoint of the neighboring
        // cardinal phase offsets so all loops can share one gait clock.
        public static float SampleDirectional(float sharedPhase, int direction, Vector4 offsets)
        {
            direction = (direction % 8 + 8) % 8;
            float offset;
            switch (direction)
            {
                case 0: offset = offsets.x; break; // F
                case 1: offset = Midpoint(offsets.x, offsets.w); break; // FR
                case 2: offset = offsets.w; break; // R
                case 3: offset = Midpoint(offsets.w, offsets.y); break; // BR
                case 4: offset = offsets.y; break; // B
                case 5: offset = Midpoint(offsets.y, offsets.z); break; // BL
                case 6: offset = offsets.z; break; // L
                default: offset = Midpoint(offsets.z, offsets.x); break; // FL
            }
            return Wrap(sharedPhase + offset);
        }

        private static float Midpoint(float a, float b)
        {
            float delta = Mathf.DeltaAngle(a * 360f, b * 360f) / 360f;
            return Wrap(a + delta * 0.5f);
        }
    }
}
