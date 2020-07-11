using OpenGL;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace VoxelWorld
{

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
        public readonly Vector3?[] GridCenters = new Vector3?[GridLocations.Length];
        public VoxelGridInfo[] Grids = new VoxelGridInfo[GridLocations.Length];
        public VoxelHierarchy[] SubHierarchies = new VoxelHierarchy[GridLocations.Length];
        public bool[] IsGeneratingHierarchy = new bool[GridLocations.Length];
        public bool[] IsGeneratingGrids = new bool[GridLocations.Length];
        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

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
                var gridInfo = GenerateGrid(GridLocations[i], cameraPos);
                GridCenters[i] = gridInfo.center;
                newGrids[i] = gridInfo.grid;
            }

            Grids = newGrids;
        }

        private (VoxelGridInfo grid, Vector3? center) GenerateGrid(Vector3I gridDir, Vector3 cameraPos)
        {
            Vector3 gridCenter = Center + gridDir.AsFloatVector3() * 0.5f * (GridSize - 2) * VoxelSize;
            VoxelGridInfo grid = new VoxelGridInfo();

            if (!IsHighEnoughResolution(gridCenter, cameraPos))
            {
                grid.Dispose();   
                return (null, gridCenter);
            }

            grid.GenerateGrid(GridSize, gridCenter, VoxelSize, WeightGen);
            if (grid.IsgridEmpty())
            {
                grid.Dispose();
                return (null, null);
            }

            grid.Interpolate();
            //grid.SmoothGrid(1);
            grid.MakeDrawMethods(false);

            return (grid, gridCenter);
        }

        public bool IsEmpty()
        {
            return Grids.All(x => x == null);
        }

        public bool IsHighEnoughResolution(Vector3 voxelCenter, Vector3 cameraPos)
        {
            Matrix4 model = Matrix4.Identity;
            Vector3 a = model * voxelCenter;
            Vector3 c = model * cameraPos;

            float resolution = (VoxelSize * 100.0f) / (a - c).Length();
            return resolution < 0.7f;
        }

        private void QueueGridGen(int index, Vector3 cameraPos)
        {
            IsGeneratingGrids[index] = true;
            WorkLimiter.QueueWork(() =>
            {
                VoxelGridInfo newGrid = GenerateGrid(GridLocations[index], cameraPos).grid;
                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        newGrid?.Dispose();
                        newGrid = null;
                    }

                    Grids[index] = newGrid;
                    IsGeneratingGrids[index] = false;
                }
            });
        }

        private void QueueHierarchyGen(int index, Vector3 cameraPos)
        {
            Vector3 gridCenter = GridCenters[index].Value;

            IsGeneratingHierarchy[index] = true;
            WorkLimiter.QueueWork(() =>
            {
                VoxelHierarchy subHireachy = new VoxelHierarchy(GridSize, gridCenter, VoxelSize, WeightGen);
                subHireachy.Generate(cameraPos);

                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        //Console.WriteLine("Wasted work");
                        subHireachy.Dispose();
                        subHireachy = null;
                    }
                    else
                    {
                        //Console.WriteLine("Done work");
                    }
                    SubHierarchies[index] = subHireachy;
                    IsGeneratingHierarchy[index] = false;
                }
            });
        }

        public void CheckAndIncreaseResolution(PlayerCamera camera, Frustum renderCheck)
        {
            if (IsGeneratingHierarchy.Any(x => x))
            {
                return;
            }

            if (IsGeneratingGrids.Any(x => x))
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

            for (int i = 0; i < Grids.Length; i++)
            {
                if (!GridCenters[i].HasValue)
                {
                    continue;
                }

                //if (Grids[i]?.BoundingBox != null && !renderCheck.Intersects(Grids[i].BoundingBox))
                //{
                //    gridResolutions[i] = ResolutionResult.Ignored;
                //    continue;
                //}

                if (IsHighEnoughResolution(GridCenters[i].Value, camera.CameraPos))
                {
                    if (SubHierarchies[i] != null)
                    {
                        SubHierarchies[i]?.Dispose();
                        SubHierarchies[i] = null;

                        QueueGridGen(i, camera.CameraPos);
                    }
                    continue;
                }

                if (SubHierarchies[i] == null)
                {
                    QueueHierarchyGen(i, camera.CameraPos);
                }
                else
                {
                    SubHierarchies[i]?.CheckAndIncreaseResolution(camera, renderCheck);
                }
            }
        }

        public bool DrawMesh(Frustum renderCheck)
        {
            bool renderedSomething = false;
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null)
                    {
                        hir.DrawMesh(renderCheck);
                        //if (hir.DrawMesh(renderCheck))
                        //{
                        //    renderedSomething = true;
                            continue;
                        //}
                    }
                }

                if (!IsGeneratingGrids[i])
                {
                    VoxelGridInfo grid = Grids[i];
                    if (grid != null)
                    {
                        grid.DrawMesh(renderCheck);
                        renderedSomething = true;
                    }
                }
            }

            return renderedSomething;
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

                if (!IsGeneratingGrids[i])
                {
                    VoxelGridInfo grid = Grids[i];
                    if (grid != null)
                    {
                        grid.DrawPoints(renderCheck);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;

                foreach (var grid in Grids)
                {
                    grid?.Dispose();
                }

                foreach (var hierarchy in SubHierarchies)
                {
                    hierarchy?.Dispose();
                }
            }
        }
    }
}
