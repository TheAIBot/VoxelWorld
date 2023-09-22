using System.Numerics;

namespace VoxelWorld
{
    /// <summary>
    /// A viewing frustum, brought in from Orchard Sun.
    /// </summary>
    public sealed class Frustum
    {
        private readonly Plane[] planes = new Plane[6];

        /// <summary>
        /// Builds the Planes so that they make up the left, right, up, down, front and back of the Frustum.
        /// </summary>
        /// <param name="clipMatrix">The combined projection and view matrix (usually from the camera).</param>
        public void UpdateFrustum(Matrix4x4 clipMatrix)
        {
            planes[0] = new Plane(clipMatrix.M44 - clipMatrix.M41, new Vector3(clipMatrix.M14 - clipMatrix.M11, clipMatrix.M24 - clipMatrix.M21, clipMatrix.M34 - clipMatrix.M31));
            planes[1] = new Plane(clipMatrix.M44 + clipMatrix.M41, new Vector3(clipMatrix.M14 + clipMatrix.M11, clipMatrix.M24 + clipMatrix.M21, clipMatrix.M34 + clipMatrix.M31));
            planes[2] = new Plane(clipMatrix.M44 + clipMatrix.M42, new Vector3(clipMatrix.M14 + clipMatrix.M12, clipMatrix.M24 + clipMatrix.M22, clipMatrix.M34 + clipMatrix.M32));
            planes[3] = new Plane(clipMatrix.M44 - clipMatrix.M42, new Vector3(clipMatrix.M14 - clipMatrix.M12, clipMatrix.M24 - clipMatrix.M22, clipMatrix.M34 - clipMatrix.M32));
            planes[4] = new Plane(clipMatrix.M44 - clipMatrix.M43, new Vector3(clipMatrix.M14 - clipMatrix.M13, clipMatrix.M24 - clipMatrix.M23, clipMatrix.M34 - clipMatrix.M33));
            planes[5] = new Plane(clipMatrix.M44 + clipMatrix.M43, new Vector3(clipMatrix.M14 + clipMatrix.M13, clipMatrix.M24 + clipMatrix.M23, clipMatrix.M34 + clipMatrix.M33));

            for (int i = 0; i < 6; i++)
            {
                float length = planes[i].Normal.Length();
                planes[i] = new Plane(planes[i].D / length, planes[i].Normal / length);
            }
        }

        /// <summary>
        /// Builds the Planes so that they make up the left, right, up, down, front and back of the Frustum.
        /// </summary>
        public void UpdateFrustum(Matrix4x4 projectionMatrix, Matrix4x4 modelViewMatrix)
        {
            UpdateFrustum(modelViewMatrix * projectionMatrix);
        }

        /// <summary>
        /// True if the BoundingCircle is in (or partially in) the Frustum.
        /// </summary>
        /// <param name="circle">BoundingCircle to check.</param>
        /// <returns>True if an intersection exists.</returns>
        public bool Intersects(BoundingCircle circle)
        {
            return Intersects(circle.Center, new Vector3(circle.Radius, circle.Radius, circle.Radius) * 2.0f);
        }

        private bool Intersects(Vector3 center, Vector3 size)
        {
            for (int i = 0; i < 6; i++)
            {
                Plane p = planes[i];

                float d = Vector3.Dot(center, p.Normal);
                float r = Vector3.Dot(size, Vector3.Abs(p.Normal));
                float dpr = d + r;

                if (dpr < -p.D)
                {
                    return false;
                }
            }
            return true;
        }

        private readonly record struct Plane(float D, Vector3 Normal);
    }
}
