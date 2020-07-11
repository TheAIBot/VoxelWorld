using OpenGL;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace VoxelWorld
{
    internal static class WorkLimiter
    {
        private static readonly ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 7, MaxMessagesPerTask = 5 };
        private static readonly ActionBlock<Action> DoWork = new ActionBlock<Action>(x => x.Invoke(), options);

        public static void QueueWork(Action work)
        {
            DoWork.Post(work);
        }
    }

    internal class VoxelHierarchy : IDisposable
    {
        private static readonly Vector3I[] GridLocations = new Vector3I[]
        {
            new Vector3I(-1, -1, -1),
            new Vector3I( 1, -1, -1),
            new Vector3I(-1,  1, -1),
            new Vector3I( 1,  1, -1),
            new Vector3I(-1, -1,  1),
            new Vector3I( 1, -1,  1),
            new Vector3I(-1,  1,  1),
            new Vector3I( 1,  1,  1)
        };

        public readonly Vector3 Center;
        public readonly float VoxelSize;
        public readonly int GridSize;
        public readonly Func<Vector3, float> WeightGen;
        public VoxelGridInfo[] Grids = new VoxelGridInfo[GridLocations.Length];
        public VoxelHierarchy[] SubHierarchies = new VoxelHierarchy[GridLocations.Length];
        public bool[] IsGeneratingHierarchy = new bool[GridLocations.Length];

        public VoxelHierarchy(int gridSize, Vector3 center, float voxelSize, Func<Vector3, float> generator)
        {
            this.Center = center;
            this.VoxelSize = voxelSize / 2.0f;
            this.GridSize = gridSize;// + 2;
            this.WeightGen = generator;
        }

        public void Generate(Vector3 cameraPos)
        {
            VoxelGridInfo[] newGrids = new VoxelGridInfo[Grids.Length];


            for (int i = 0; i < GridLocations.Length; i++)
            {
                Vector3 gridCenter = Center + GridLocations[i].AsFloatVector3() * 0.5f * (GridSize - 2) * VoxelSize;
                VoxelGridInfo grid = new VoxelGridInfo();

                float resolution = GetResolution(gridCenter, cameraPos);
                if (!IsHighEnoughResolution(resolution))
                {
                    grid.Center = gridCenter;
                    Grids[i] = grid;
                    continue;
                }

                grid.GenerateGrid(GridSize, gridCenter, VoxelSize, WeightGen);
                if (grid.IsgridEmpty())
                {
                    grid.Dispose();
                    Grids[i] = null;
                    continue;
                }

                grid.Interpolate();
                //grid.SmoothGrid(1);
                grid.MakeDrawMethods(false);

                Grids[i] = grid;
            }

            //Grids = newGrids;
        }

        public bool IsEmpty()
        {
            return Grids.All(x => x == null);
        }

        private float GetResolution(Vector3 voxelCenter, Vector3 cameraPos)
        {
            Matrix4 model = Matrix4.Identity;
            Vector3 a = model * voxelCenter;
            Vector3 c = model * cameraPos;

            return (VoxelSize * 100.0f) / (a - c).Length();
        }

        public bool IsHighEnoughResolution(float resolution)
        {
            return resolution < 0.7f;
        }

        private enum ResolutionResult
        {
            TooMuch,
            Enough,
            NotEnough,
            Ignored
        }

        public void CheckAndIncreaseResolution(PlayerCamera camera)
        {
            if (IsGeneratingHierarchy.Any(x => x))
            {
                return;
            }

            for (int i = 0; i < SubHierarchies.Length; i++)
            {
                if (SubHierarchies[i] != null && Grids[i] != null)
                {
                    Grids[i]?.Dispose();
                    Grids[i] = null;
                }
            }

            Span<ResolutionResult> gridResolutions = stackalloc ResolutionResult[Grids.Length];
            float[] resolutions = new float[Grids.Length];
            for (int i = 0; i < Grids.Length; i++)
            {
                if (Grids[i] == null)
                {
                    gridResolutions[i] = ResolutionResult.Ignored;
                    continue;
                }

                resolutions[i] = GetResolution(Grids[i].Center, camera.CameraPos);
                if (IsHighEnoughResolution(resolutions[i]))
                {
                    if (SubHierarchies[i] != null)
                    {
                        gridResolutions[i] = ResolutionResult.TooMuch;
                    }
                    else
                    {
                        gridResolutions[i] = ResolutionResult.Enough;
                    }
                    continue;
                }

                gridResolutions[i] = ResolutionResult.NotEnough;
            }

            //lock (SubHierarchies)
            //{
            //    for (int i = 0; i < gridResolutions.Length; i++)
            //    {
            //        if (gridResolutions[i] == ResolutionResult.TooMuch)
            //        {
            //            SubHierarchies[i]?.Dispose();
            //            SubHierarchies[i] = null;
            //        }
            //    }
            //}


            bool needToGenerateNewHierarchies = false;
            (Vector3?, int, float)[] dwa = new (Vector3?, int, float)[GridLocations.Length];
            for (int i = 0; i < gridResolutions.Length; i++)
            {
                if (gridResolutions[i] == ResolutionResult.NotEnough && SubHierarchies[i] == null)
                {
                    dwa[i] = ((Grids[i].Center, i, resolutions[i]));
                    needToGenerateNewHierarchies = true;
                }
                else
                {
                    dwa[i] = ((null, i, resolutions[i]));
                    SubHierarchies[i]?.CheckAndIncreaseResolution(camera);
                }
            }

            if (needToGenerateNewHierarchies)
            {
                dwa = dwa.OrderByDescending(x => x.Item3).ToArray();

                for (int i = 0; i < dwa.Length; i++)
                {
                    if (dwa[i].Item1.HasValue)
                    {
                        if (!dwa[i].Item1.HasValue)
                        {
                            continue;
                        }

                        Vector3 gridCenter = dwa[i].Item1.Value;
                        int index = dwa[i].Item2;

                        IsGeneratingHierarchy[index] = true;
                        WorkLimiter.QueueWork(() =>
                        {
                            VoxelHierarchy subHireachy = new VoxelHierarchy(GridSize, gridCenter, VoxelSize, WeightGen);
                            subHireachy.Generate(camera.CameraPos);

                            if (subHireachy.IsEmpty())
                            {
                                //Console.WriteLine("Wasted work");
                                subHireachy.Dispose();
                            }
                            else
                            {
                                //Console.WriteLine("Done work");
                            }

                            lock (SubHierarchies)
                            {
                                SubHierarchies[index] = subHireachy;
                                IsGeneratingHierarchy[index] = false;
                            }
                        });
                    }
                }
            }
        }

        public void DrawMesh(Frustum renderCheck)
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null)
                    {
                        hir.DrawMesh(renderCheck);
                        continue;
                    }
                }

                VoxelGridInfo grid = Grids[i];
                if (grid != null)
                {
                    grid.DrawMesh(renderCheck);
                }
            }
        }

        public void DrawPoints(Frustum renderCheck)
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null)
                    {
                        hir.DrawPoints(renderCheck);
                        continue;
                    }
                }

                VoxelGridInfo grid = Grids[i];
                if (grid != null)
                {
                    grid.DrawPoints(renderCheck);
                }
            }
        }

        public void Dispose()
        {
            //this is bad, fix in future
            while (IsGeneratingHierarchy.Any(x => x))
            {
                Thread.Sleep(1);
            }

            if (Grids != null)
            {
                foreach (var grid in Grids)
                {
                    grid?.Dispose();
                }
            }

            if (SubHierarchies != null)
            {
                foreach (var hierarchy in SubHierarchies)
                {
                    hierarchy?.Dispose();
                }
            }
        }
    }
}
