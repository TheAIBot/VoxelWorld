using OpenGL;
using System.Numerics;

namespace VoxelWorld
{
    internal class ModelTransformations
    {
        public Matrix4 Rotation = Matrix4.Identity;
        public Matrix4 RevRotation = Matrix4.Identity;
        public Vector3 Translation = new Vector3(0, 0, 0);
        public Vector3 RotatedLookDir = new Vector3(0, 0, 0);
        public Vector3 CameraPos = new Vector3(0, 0, 0);
        public float FOV = 0.0f;

        public void Update(PlayerCamera camera, float yAngle)
        {
            Rotation = Matrix4.CreateRotationY(yAngle);
            RevRotation = Matrix4.CreateRotationY(-yAngle);
            RotatedLookDir = Rotation * camera.LookDirection;
            CameraPos = camera.CameraPos;
            FOV = camera.FieldOfView;
        }
    }
}
