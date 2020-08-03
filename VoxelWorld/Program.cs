using OpenGL;
using OpenGL.Platform;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using VoxelWorld.Shaders;

namespace VoxelWorld
{

    class Program
    {
        static void Main(string[] args)
        {
            int windowWidth = 1280;
            int windowHeight = 720;

            Window.CreateWindow("Voxel World", windowWidth, windowHeight);
            Gl.Viewport(0, 0, Window.Width, Window.Height);
            Window.ApplyVerticalSync(true);

            bool renderMesh = true;
            bool renderPoints = false;
            bool controlDummyCamera = false;


            PlayerCamera dummyCamera = new PlayerCamera(Window.Width, Window.Height, new Vector3(15, 15, 15));
            Input.Subscribe('i', dummyCamera.MoveForward);
            Input.Subscribe('k', dummyCamera.MoveBackward);
            Input.Subscribe('l', () => controlDummyCamera = !controlDummyCamera);

            PlayerCamera player = new PlayerCamera(Window.Width, Window.Height, new Vector3(-15, -15, -15));
            Input.MouseLeftClick = new Event(x => 
            {
                if (controlDummyCamera)
                {
                    dummyCamera.UpdateCameraDirection(x);
                }
                else
                {
                    player.UpdateCameraDirection(x);
                }
            });
            Input.Subscribe('w', player.MoveForward);
            Input.Subscribe('s', player.MoveBackward);
            Input.Subscribe('a', player.MoveLeft);
            Input.Subscribe('d', player.MoveRight);
            Input.Subscribe('1', () => renderMesh = !renderMesh);
            Input.Subscribe('2', () => renderPoints = !renderPoints);



            var planetGen = new PlanetGen(3, 8.0f, 3.0f, 3.0f);
            VoxelSystem system = new VoxelSystem(10, new Vector3(0, 0, 0), 0.3f, planetGen);
            system.TestResizeToFindFirstGrid();

            Frustum renderCheck = new Frustum();


            //return;

            //Task.Run(() => grid.Smooth(10));

            Gl.Enable(EnableCap.DepthTest);
            Gl.Enable(EnableCap.CullFace);
            //Gl.Enable(EnableCap.Blend);
            Gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            float angle = 0;

            Stopwatch watch = new Stopwatch();

            PlayerCamera renderFrom = player;

            bool IsRunning = true;

            Thread cake = new Thread(() =>
            {
                while (IsRunning)
                {
                    renderFrom = controlDummyCamera ? dummyCamera : player;
                    renderFrom.UpdateCameraDirection(Input.MousePosition);
                    renderCheck.UpdateFrustum(renderFrom.Perspective, renderFrom.View);

                    system.UpdateModel(renderFrom, angle);
                    system.CheckVoxelResolution(renderCheck);
                }

            });

            cake.Priority = ThreadPriority.AboveNormal;
            cake.Start();

            // handle events and render the frame
            while (Window.Open)
            {
                watch.Restart();

                Window.HandleEvents();     

                if (renderFrom.UpdateCameraDimensions(Window.Width, Window.Height))
                {
                    Gl.Viewport(0, 0, Window.Width, Window.Height);
                }


                Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                //vboPos.BufferSubData(grid.VoxelPoints);
                //vboNorm.BufferSubData(Geometry.CalculateNormals(grid.VoxelPoints, grid.indicesArr));

                ShaderProgram meshShader = SimpleShader.GetShader();
                meshShader.Use();
                meshShader["P"].SetValue(player.Perspective);
                meshShader["V"].SetValue(player.View);
                meshShader["M"].SetValue(system.Model);
                meshShader["N"].SetValue((system.Model * player.View).Transpose().Inverse());

                meshShader["light_pos"].SetValue(player.View * (system.Model * new Vector4(-3, -3, 0.0f, 0.0f)));
                meshShader["light_diff"].SetValue(new Vector4(0.6f, 0.6f, 0.3f, 0.6f));
                meshShader["light_spec"].SetValue(new Vector4(0.6f, 0.6f, 0.3f, 0.6f));
                meshShader["light_amb"].SetValue(new Vector4(0.3f, 0.4f, 0.6f, 0.4f));

                meshShader["mat_diff"].SetValue(new Vector4(0.6f, 0.3f, 0.3f, 0.4f));
                meshShader["mat_spec"].SetValue(new Vector4(0.6f, 0.3f, 0.3f, 0.4f));
                meshShader["mat_spec_exp"].SetValue(7.0f);

                VoxelGridInfo.DrawCalls = 0;
                if (renderMesh)
                {
                    Gl.Disable(EnableCap.Blend);
                    MainThreadWork.DrawGrids();
                }
                //if (renderPoints)
                //{
                //    Gl.Enable(EnableCap.Blend);
                //    system.DrawPoints();
                //}

                //Console.WriteLine(VoxelGridInfo.DrawCalls);

                watch.Stop();

                //Console.WriteLine(watch.ElapsedMilliseconds);



                angle += 0.2f;




                //Console.WriteLine(Gl.GetShaderInfoLog(vao.Program.ProgramID));
                //Console.WriteLine(Gl.GetProgramInfoLog(vao.ID));

                Window.SwapBuffers();
            }

            IsRunning = false;
            cake.Join();
        }
    }
}
