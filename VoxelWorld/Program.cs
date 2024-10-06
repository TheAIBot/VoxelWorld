using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using VoxelWorld.Render.Box;
using VoxelWorld.Render.VoxelGrid;
using VoxelWorld.Shaders;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

[assembly: InternalsVisibleTo("VoxelBench")]

namespace VoxelWorld
{
    internal record DirectionalLight(Vector4 Position, Vector4 Ambient, Vector4 Diffuse, Vector4 Specular);
    internal record Material(Vector4 Diffuse, Vector4 Specular, float Shininess);

    public static class Program
    {
        private static IWindow window;
        private static bool renderMesh = true;
        private static bool renderPoints = false;
        private static bool controlDummyCamera = false;
        private static PlayerCamera dummyCamera;
        private static PlayerCamera player;
        private static DirectionalLight light;
        private static Material material;
        private static PlayerCamera renderFrom;
        private static bool IsRunning;
        private static float angle;
        private static PerfNumAverage<int> avgFrameTime = new PerfNumAverage<int>(200, x => x);
        private static TimeCounter AvgFramesPerSecond = new TimeCounter(TimeSpan.FromSeconds(1), 5);
        private static GpuTimer gpuFrameTime;
        private static Thread cake;
        private static Frustum renderCheck = new Frustum();
        private static GL _openGl;
        private static VoxelSystem system;
        private static IKeyboard primaryKeyboard;
        private static OpenGLDebugging openGLDebugging;


        static void Main(string[] args)
        {
            int windowWidth = 2280;
            int windowHeight = 1720;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(windowWidth, windowHeight);
            options.Title = "Voxel World";
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Debug, new APIVersion(4, 6));

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;

            //Run the window.
            window.Run();

            // window.Run() is a BLOCKING method - this means that it will halt execution of any code in the current
            // method until the window has finished running. Therefore, this dispose method will not be called until you
            // close the window.
            window.Dispose();
        }

        private static void OnLoad()
        {
            _openGl = GL.GetApi(window);
            gpuFrameTime = new GpuTimer(_openGl);
            VoxelGridRenderManager.SetOpenGl(_openGl);
            BoxRenderManager.SetOpenGl(_openGl);

            var flags = _openGl.GetInteger(GLEnum.ContextFlags);
            if ((flags & (int)GLEnum.ContextFlagDebugBit) == 0)
            {
                Console.WriteLine("OpenGL context was not created with a debug flag.");
            }
            else
            {
                Console.WriteLine("OpenGL debug context enabled.");
            }
            Console.WriteLine($"OpenGL Version: {_openGl.GetStringS(GLEnum.Version)}");

            dummyCamera = new PlayerCamera(window.Size.X, window.Size.Y, new Vector3(15, 15, 15));
            player = new PlayerCamera(window.Size.X, window.Size.Y, new Vector3(-8, -8, -8));
            renderFrom = player;

            light = new DirectionalLight(
                new Vector4(-15, -15, 0.0f, 0.0f),
                new Vector4(0.01f, 0.01f, 0.01f, 0.4f),
                new Vector4(0.6f, 0.6f, 0.3f, 0.6f),
                new Vector4(0.3f, 0.3f, 0.1f, 0.3f)
            );

            material = new Material(
                new Vector4(0.6f, 0.3f, 0.3f, 0.4f),
                new Vector4(0.6f, 0.3f, 0.3f, 0.4f),
                25.0f
            );

            var planetGen = new PlanetGen(3, 8.0f, 3.0f, 3.0f);
            system = new VoxelSystem(30, new Vector3(0, 0, 0), 0.3f, planetGen);
            system.TestResizeToFindFirstGrid();

            WorkLimiter.StartWorkers(system.FirstLevelSystemData);

            IsRunning = true;

            cake = new Thread(() =>
            {
                Stopwatch watch = new Stopwatch();
                PerfNumAverage<int> avgCheckTime = new PerfNumAverage<int>(200, x => x);

                while (IsRunning)
                {
                    watch.Restart();

                    renderFrom = controlDummyCamera ? dummyCamera : player;
                    //renderFrom.UpdateCameraDirection(Input.MousePosition);
                    renderCheck.UpdateFrustum(renderFrom.Perspective, renderFrom.View);

                    system.UpdateModel(renderFrom, angle);
                    system.CheckVoxelResolution(renderCheck);

                    watch.Stop();
                    avgCheckTime.AddSample((int)watch.ElapsedMilliseconds);
                    //Console.WriteLine(avgCheckTime.GetAverage());
                }

            });

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            cake.Priority = ThreadPriority.AboveNormal;
            cake.Start();

            Task.Run(async () =>
            {
                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
                    while (IsRunning && await timer.WaitForNextTickAsync())
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Draw Buffers: {VoxelGridRenderManager.DrawBuffers}");
                        //Console.WriteLine(GetGPUBufferSizeInMB().ToString("N0") + "MB");
                        //Console.WriteLine(GridsDrawing.ToString("N0"));
                        Console.WriteLine($"Draw Buffer Utilization {(VoxelGridRenderManager.GetBufferUtilization() * 100):N2}%");
                        Console.WriteLine($"Rendered Triangles: {VoxelGridRenderManager.TrianglesDrawing:N0}");
                        Console.WriteLine($"Rendered Grids: {VoxelGridRenderManager.GridsDrawing:N0}");
                        Console.WriteLine($"Generated Triangles: {VoxelGridRenderManager.AvgNewTriangles.GetAveragePerTimeUnit(TimeSpan.FromSeconds(1)):N0}/s");
                        float newGridsPerSecond = VoxelGridRenderManager.AvgNewGrids.GetAveragePerTimeUnit(TimeSpan.FromSeconds(1));
                        Console.WriteLine($"Generated Grids: {newGridsPerSecond:N0}/s");
                        Console.WriteLine($"Copy commands: {VoxelGridRenderManager.AvgTransferedGridsFromAlmostEmptyBuffers.GetAveragePerTimeUnit(TimeSpan.FromSeconds(1)):N0}/s");
                        const long bytesToMBRatio = 1_000_000;
                        float transferedBytes = VoxelGridRenderManager.AvgTransferedBytes.GetAveragePerTimeUnit(TimeSpan.FromSeconds(1));
                        Console.WriteLine($"Transfered to GPU: {(transferedBytes / bytesToMBRatio):N0}MB/s");
                        const long bytesToKBRatio = 1_000;
                        Console.WriteLine($"Grid Size: {(VoxelGridRenderManager.AvgGridSize.GetAverage() / bytesToKBRatio):N0}KB");
                        Console.WriteLine($"Frame Time: {avgFrameTime.GetAverage():N0}ms");
                        Console.WriteLine($"FPS: {AvgFramesPerSecond.GetAverage():N1}");
                        //VoxelGridRenderManager.PrintDrawBufferUtilization();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            });


            //Set-up input context.
            IInputContext input = window.CreateInput();
            primaryKeyboard = input.Keyboards.FirstOrDefault();
            if (primaryKeyboard != null)
            {
                primaryKeyboard.KeyDown += KeyDown;
            }

            for (int i = 0; i < input.Mice.Count; i++)
            {
                input.Mice[i].MouseUp += Program_MouseUp;
                input.Mice[i].MouseDown += Program_MouseDown;
                input.Mice[i].MouseMove += Program_MouseMove;
            }
        }

        private static void OnRender(double obj)
        {
            if (openGLDebugging == null)
            {
                openGLDebugging = new OpenGLDebugging(_openGl);
            }

            _openGl.Enable(EnableCap.DepthTest);
            _openGl.Enable(EnableCap.CullFace);
            //Gl.Enable(EnableCap.Blend);
            _openGl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            gpuFrameTime.StartTimer();

            if (renderFrom.UpdateCameraDimensions(window.Size.X, window.Size.Y))
            {
                _openGl.Viewport(Vector2D<int>.Zero, window.Size);
            }


            _openGl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            VoxelGridRenderManager.ProcessCommands();
            BoxRenderManager.ProcessCommands();

            VoxelGridInfo.DrawCalls = 0;
            if (renderMesh)
            {
                SimpleShader.GetShader(_openGl).Use();
                SimpleShader.SetPVM(renderFrom.Perspective, renderFrom.View, system.Model);
                SimpleShader.SetLight(light, renderFrom.CameraPos);
                SimpleShader.SetMaterial(material);

                VoxelGridRenderManager.DrawGrids();
            }
            if (renderPoints)
            {
                _openGl.Enable(EnableCap.Blend);

                BoxShader.GetShader(_openGl).Use();
                BoxShader.SetPVM(renderFrom.Perspective, renderFrom.View, system.Model);

                BoxRenderManager.Draw();

                _openGl.Disable(EnableCap.Blend);
            }

            //Console.WriteLine(VoxelGridInfo.DrawCalls);

            gpuFrameTime.StopTimer();

            avgFrameTime.AddSample((int)gpuFrameTime.GetTimeInMS());
            AvgFramesPerSecond.IncrementCounter();
            //Console.WriteLine($"Frame Time: {avgFrameTime.GetAverage():N0}ms");
            //Console.WriteLine($"Empty: {VoxelGridInfo.GeneratedEmpty:N0}");
            //Console.WriteLine($"Not Empty: {VoxelGridInfo.GeneratedNotEmpty:N0}");
            //int totalGenerated = VoxelGridInfo.GeneratedEmpty + VoxelGridInfo.GeneratedNotEmpty;
            //float ratioEmpty = (float)VoxelGridInfo.GeneratedEmpty / totalGenerated;
            //Console.WriteLine($"Empty: {(100.0f * ratioEmpty):N0}%");
            //Console.WriteLine();



            angle += 0.002f;
        }

        private static void OnUpdate(double obj)
        {
            if (primaryKeyboard.IsKeyPressed(Key.I))
            {
                dummyCamera.MoveForward();
            }
            else if (primaryKeyboard.IsKeyPressed(Key.K))
            {
                dummyCamera.MoveBackward();
            }
            else if (primaryKeyboard.IsKeyPressed(Key.W))
            {
                player.MoveForward();
            }
            else if (primaryKeyboard.IsKeyPressed(Key.S))
            {
                player.MoveBackward();
            }
            else if (primaryKeyboard.IsKeyPressed(Key.A))
            {
                player.MoveLeft();
            }
            else if (primaryKeyboard.IsKeyPressed(Key.D))
            {
                player.MoveRight();
            }
        }

        private static void KeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            switch (arg2)
            {
                case Key.L:
                    controlDummyCamera = !controlDummyCamera;
                    break;
                case Key.Number1:
                    renderMesh = !renderMesh;
                    break;
                case Key.Number2:
                    renderPoints = !renderPoints;
                    break;
                default:
                    break;
            }

            if (arg2 == Key.Escape)
            {
                IsRunning = false;
                cake.Join();
                WorkLimiter.StopWorkers();
                window.Close();
            }
        }

        private static void Program_MouseDown(IMouse mouse, MouseButton mouseButton)
        {
            if (controlDummyCamera)
            {
                dummyCamera.UpdateCameraDirectionMouseDown(mouse, mouseButton);
            }
            else
            {
                player.UpdateCameraDirectionMouseDown(mouse, mouseButton);
            }
        }

        private static void Program_MouseUp(IMouse mouse, MouseButton mouseButton)
        {
            if (controlDummyCamera)
            {
                dummyCamera.UpdateCameraDirectionMouseUp(mouse, mouseButton);
            }
            else
            {
                player.UpdateCameraDirectionMouseUp(mouse, mouseButton);
            }
        }

        private static void Program_MouseMove(IMouse mouse, Vector2 mousePosition)
        {
            if (controlDummyCamera)
            {
                dummyCamera.UpdateCameraDirectionMouseMove(mouse);
            }
            else
            {
                player.UpdateCameraDirectionMouseMove(mouse);
            }
        }
    }
}
