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

        //required to make a grid
        private readonly Vector3 Center;
        private readonly float VoxelSize;
        private readonly int GridSize;
        private readonly Func<Vector3, float> WeightGen;
        private readonly Vector3?[] GridCenters = new Vector3?[GridLocations.Length];

        //keeps track of grids
        private readonly VoxelGridInfo[] Grids = new VoxelGridInfo[GridLocations.Length];
        private readonly bool[] IsGeneratingHierarchy = new bool[GridLocations.Length];
        private AxisAlignedBoundingBox BoundingBox = null;

        //keeps track of sub hierarchies
        private readonly VoxelHierarchy[] SubHierarchies = new VoxelHierarchy[GridLocations.Length];
        private readonly AxisAlignedBoundingBox[] SubHirBoundBoxes = new AxisAlignedBoundingBox[GridLocations.Length];
        private readonly bool[] IsGeneratingGrids = new bool[GridLocations.Length];

        //make sure all grids and hierarchies are disposed of
        //no matter if they are being generated
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
            BoundingBox = new AxisAlignedBoundingBox(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(float.MinValue, float.MinValue, float.MinValue));

            bool addedBox = false;
            for (int i = 0; i < GridLocations.Length; i++)
            {
                var gridInfo = GenerateGrid(GridLocations[i], cameraPos);
                GridCenters[i] = gridInfo.center;
                Grids[i] = gridInfo.grid;

                if (gridInfo.box != null)
                {
                    BoundingBox.AddBoundingBox(gridInfo.box);
                    addedBox = true;
                }
            }

            if (!addedBox)
            {
                BoundingBox = null;
            }
        }

        private (VoxelGridInfo grid, Vector3? center, AxisAlignedBoundingBox box) GenerateGrid(Vector3I gridDir, Vector3 cameraPos)
        {
            Vector3 gridCenter = Center + gridDir.AsFloatVector3() * 0.5f * (GridSize - 2) * VoxelSize;
            VoxelGridInfo grid = new VoxelGridInfo();

            grid.GenerateGrid(GridSize, gridCenter, VoxelSize, WeightGen);
            if (grid.IsgridEmpty())
            {
                grid.Dispose();
                return (null, null, null);
            }

            grid.Interpolate();
            AxisAlignedBoundingBox box = grid.GetBoundingBox();
            //grid.SmoothGrid(1);
            grid.MakeDrawMethods();

            return (grid, gridCenter, box);
        }

        private bool IsEmpty()
        {
            return Grids.All(x => x == null);
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, Vector3 cameraPos)
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
                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        return;
                    }
                }

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
                    SubHirBoundBoxes[index] = subHireachy?.BoundingBox;
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
                    Grids[i].Dispose();
                    Grids[i] = null;
                }
            }

            for (int i = 0; i < Grids.Length; i++)
            {
                if (!GridCenters[i].HasValue)
                {
                    continue;
                }

                AxisAlignedBoundingBox subHirBox = SubHierarchies[i]?.BoundingBox;
                if (subHirBox != null && !renderCheck.Intersects(subHirBox))
                {
                    SubHierarchies[i]?.Dispose();
                    SubHierarchies[i] = null;
                    continue;
                }

                Vector3 gridCenter = /*Grids[i]?.BoundingBox?.Center ??*/ GridCenters[i].Value;
                if (IsHighEnoughResolution(gridCenter, camera.CameraPos))
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
                    {
                        if (SubHirBoundBoxes[i] == null || renderCheck.Intersects(SubHirBoundBoxes[i]))
                        {
                            QueueHierarchyGen(i, camera.CameraPos);
                        }
                    }
                }
                else
                {
                    SubHierarchies[i]?.CheckAndIncreaseResolution(camera, renderCheck);
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

                if (!IsGeneratingGrids[i])
                {
                    VoxelGridInfo grid = Grids[i];
                    if (grid != null)
                    {
                        grid.DrawMesh(renderCheck);
                    }
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
