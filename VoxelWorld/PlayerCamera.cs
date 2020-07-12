using OpenGL;
using OpenGL.Platform;
using System;
using System.Numerics;

namespace VoxelWorld
{
    class PlayerCamera
    {
        private int WindowWidth;
        private int WindowHeight;

        public Vector3 CameraPos;
        public Vector3 LookDirection;
        public Vector3 UpVector;
        private bool PrevLeftMouseDown;
        private Vector2 PrevMousePos;
        private Vector2 CameraAngles;
        private bool FirstLeftClick = true;

        private float FieldOfView = 45.0f * (MathF.PI / 180.0f);

        public Matrix4 Perspective 
        {
            get
            {
                return Matrix4.CreatePerspectiveFieldOfView(FieldOfView, (float)WindowWidth / WindowHeight, 0.1f, 100f);
            }
        }

        public Matrix4 View
        {
            get
            {
                return Matrix4.LookAt(CameraPos, CameraPos + LookDirection, UpVector);
            }
        }

        public PlayerCamera(int width, int height, Vector3 cameraPos)
        {
            this.WindowWidth = width;
            this.WindowHeight = height;

            this.CameraPos = cameraPos;
            this.LookDirection = -CameraPos.Normalize();//new Vector3(MathF.Cos(0) * MathF.Cos(0) - 3, MathF.Sin(0) * MathF.Cos(0), MathF.Sin(0));
            this.UpVector = new Vector3(0, 1, 0);
            this.PrevLeftMouseDown = false;
            this.PrevMousePos = new Vector2(0, 0);

            float yAngle = MathF.Asin(LookDirection.Y);
            float xAngle = MathF.Acos(LookDirection.X / MathF.Cos(yAngle));
            this.CameraAngles = new Vector2(xAngle, yAngle);
        }

        public void MoveLeft() => CameraPos += new Vector3(-0.01f, 0, 0);
        public void MoveRight() => CameraPos += new Vector3(0.01f, 0, 0);
        public void MoveForward() => CameraPos += 0.08f * LookDirection;
        public void MoveBackward() => CameraPos += -0.08f * LookDirection;

        public void UpdateCameraDirection(Click mouse)
        {
            if (PrevLeftMouseDown)
            {
                float dx = 0.6f * MathF.PI * (PrevMousePos.X - mouse.X) / WindowWidth;
                float dy = 0.3f * MathF.PI * (PrevMousePos.Y - mouse.Y) / WindowHeight;
                CameraAngles += new Vector2(dx, -dy);
                Console.WriteLine(CameraAngles);
                LookDirection = new Vector3(MathF.Cos(CameraAngles.X) * MathF.Cos(CameraAngles.Y), MathF.Sin(CameraAngles.Y), MathF.Sin(CameraAngles.X) * MathF.Cos(CameraAngles.Y));
                //LookDirection += new Vector3(MathF.Cos(dx) * MathF.Cos(-dy), MathF.Sin(-dy), MathF.Sin(dx) * MathF.Cos(-dy)) - new Vector3(MathF.Cos(0) * MathF.Cos(0), MathF.Sin(0), MathF.Sin(0) * MathF.Cos(0));
                //LookDirection = Matrix4.CreateRotationX(dx) * Matrix4.CreateRotationY(-dy) * LookDirection;

                PrevMousePos = new Vector2(mouse.X, mouse.Y);


                if (mouse.Button == MouseButton.Left && mouse.State == MouseState.Up)
                {
                    PrevLeftMouseDown = false;
                }
            }
            else
            {
                PrevLeftMouseDown = mouse.Button == MouseButton.Left && mouse.State == MouseState.Down;
                PrevMousePos = new Vector2(mouse.X, mouse.Y);
            }
        }

        public bool UpdateCameraDimensions(int width, int height)
        {
            if (WindowWidth != width || WindowHeight != height)
            {
                this.WindowWidth = width;
                this.WindowHeight = height;
                return true;
            }
            return false;
        }
    }
}
