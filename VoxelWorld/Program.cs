﻿using OpenGL;
using OpenGL.Platform;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using VoxelWorld.Shaders;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("VoxelBench")]
namespace VoxelWorld
{
    internal record DirectionalLight(Vector4 Position, Vector4 Ambient, Vector4 Diffuse, Vector4 Specular);
    internal record Material(Vector4 Diffuse, Vector4 Specular, float Shininess);

    class Program
    {
        static void Main(string[] args)
        {
            WorkLimiter.StartWorkers();

            int windowWidth = 1280;
            int windowHeight = 720;

            Window.CreateWindow("Voxel World", windowWidth, windowHeight);
            Gl.Viewport(0, 0, Window.Width, Window.Height);
            SDL2.SDL.SDL_GL_SetSwapInterval(1);

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

            DirectionalLight light = new DirectionalLight(
                new Vector4(-15, -15, 0.0f, 0.0f),
                new Vector4(0.01f, 0.01f, 0.01f, 0.4f),
                new Vector4(0.6f, 0.6f, 0.3f, 0.6f),
                new Vector4(0.3f, 0.3f, 0.1f, 0.3f)
            );

            Material material = new Material(
                new Vector4(0.6f, 0.3f, 0.3f, 0.4f),
                new Vector4(0.6f, 0.3f, 0.3f, 0.4f),
                25.0f
            );

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

                SimpleShader.SetPVM(renderFrom.Perspective, renderFrom.View, system.Model);
                SimpleShader.SetLight(light, renderFrom.CameraPos);
                SimpleShader.SetMaterial(material);

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



                angle += 0.0002f;




                //Console.WriteLine(Gl.GetShaderInfoLog(vao.Program.ProgramID));
                //Console.WriteLine(Gl.GetProgramInfoLog(vao.ID));

                Window.SwapBuffers();
            }

            IsRunning = false;
            cake.Join();
            WorkLimiter.StopWorkers();
        }
    }
}
