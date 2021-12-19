using System.Numerics;

namespace Spark
{
    public class Spline
    {
        private CameraTransform[] keyframes;
        private Vector3[] velocities;

        public Spline(CameraTransform[] keyframes)
        {
            this.keyframes = keyframes;
            velocities = new Vector3[keyframes.Length];

            // set the start and end velocities to 0
            velocities[0] = Vector3.Zero;
            velocities[keyframes.Length] = Vector3.Zero;
            
            if (keyframes.Length > 2)
            {
                for (int i = 1; i < keyframes.Length-1; i++)
                {
                    // velocities[i] = (keyframes.)
                }
            }
        }

        // public Vector3 GetPoint(float t)
        // {
        //     
        // }
    }
}