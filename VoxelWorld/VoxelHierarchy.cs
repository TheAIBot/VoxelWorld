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
        private readonly AxisAlignedBoundingBox[] GridBoundBoxes = new AxisAlignedBoundingBox[GridLocations.Length];
        private readonly GridNormal[] GridNormals = new GridNormal[GridLocations.Length];

        private AxisAlignedBoundingBox BoundingBox = null;
        private GridNormal HirNormal = new GridNormal();

        //keeps track of sub hierarchies
        private readonly VoxelHierarchy[] SubHierarchies = new VoxelHierarchy[GridLocations.Length];
        private readonly bool[] IsEmptyHierarchies = new bool[GridLocations.Length];
        private readonly AxisAlignedBoundingBox[] SubHirBoundBoxes = new AxisAlignedBoundingBox[GridLocations.Length];
        private readonly GridNormal[] SubHirNormals = new GridNormal[GridLocations.Length];
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

        public void Generate(Matrix4 model_rot, Vector3 lookDir)
        {
            BoundingBox = new AxisAlignedBoundingBox(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(float.MinValue, float.MinValue, float.MinValue));

            bool addedBox = false;
            for (int i = 0; i < GridLocations.Length; i++)
            {
                var gridInfo = GenerateGrid(GridLocations[i], model_rot, lookDir);
                GridCenters[i] = gridInfo.center;
                Grids[i] = gridInfo.grid;
                GridNormals[i] = gridInfo.normal;
                HirNormal.AddNormal(gridInfo.normal);

                if (gridInfo.box != null)
                {
                    GridBoundBoxes[i] = gridInfo.box;
                    BoundingBox.AddBoundingBox(gridInfo.box);
                    addedBox = true;
                }
            }

            if (!addedBox)
            {
                BoundingBox = null;
            }
        }

        private (VoxelGridInfo grid, Vector3? center, AxisAlignedBoundingBox box, GridNormal normal) GenerateGrid(Vector3I gridDir, Matrix4 model_rot, Vector3 lookDir)
        {
            Vector3 gridCenter = Center + gridDir.AsFloatVector3() * 0.5f * (GridSize - 2) * VoxelSize;
            VoxelGridInfo grid = new VoxelGridInfo();

            grid.GenerateGrid(GridSize, gridCenter, VoxelSize, WeightGen);
            if (grid.IsgridEmpty())
            {
                grid.Dispose();
                return (null, null, null, new GridNormal());
            }

            grid.Interpolate();
            AxisAlignedBoundingBox box = grid.GetBoundingBox();
            GridNormal normal = grid.GetGridNormal();

            if (!normal.CanSee(model_rot, lookDir))
            {
                grid.Dispose();
                return (null, gridCenter, box, normal);
            }

            //grid.SmoothGrid(1);
            grid.MakeDrawMethods();

            return (grid, gridCenter, box, normal);
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

        private void QueueGridGen(int index, Matrix4 model_rot, Vector3 lookDir)
        {
            IsGeneratingGrids[index] = true;
            WorkLimiter.QueueWork(() =>
            {
                VoxelGridInfo newGrid = GenerateGrid(GridLocations[index], model_rot, lookDir).grid;
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

        private void QueueHierarchyGen(int index, Matrix4 model_rot, Vector3 lookDir)
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
                subHireachy.Generate(model_rot, lookDir);

                if (subHireachy.IsEmpty())
                {
                    IsEmptyHierarchies[index] = true;
                    subHireachy.Dispose();
                    subHireachy = null;
                }

                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        //Console.WriteLine("Wasted work");
                        subHireachy?.Dispose();
                        subHireachy = null;
                    }
                    else
                    {
                        //Console.WriteLine("Done work");
                    }

                    if (subHireachy != null)
                    {
                        SubHirBoundBoxes[index] = subHireachy.BoundingBox;
                        SubHirNormals[index] = subHireachy.HirNormal;
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
                if (Grids[i] != null)
                {
                    if (SubHierarchies[i] != null)
                    {
                        Grids[i].Dispose();
                        Grids[i] = null;
                    }
                    else if (!GridNormals[i].CanSee(Matrix4.Identity, camera.LookDirection))
                    {
                        Grids[i].Dispose();
                        Grids[i] = null;
                    }
                    else if (!renderCheck.Intersects(Grids[i].BoundingBox))
                    {
                        Grids[i].Dispose();
                        Grids[i] = null;
                    }
                }
            }

            for (int i = 0; i < Grids.Length; i++)
            {
                if (!GridCenters[i].HasValue)
                {
                    continue;
                }

                if (SubHierarchies[i] != null)
                {
                    if (!SubHierarchies[i].HirNormal.CanSee(Matrix4.Identity, camera.LookDirection))
                    {
                        SubHierarchies[i].Dispose();
                        SubHierarchies[i] = null;
                        continue;
                    }
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
                    }
                    if (Grids[i] == null && GridNormals[i].CanSee(Matrix4.Identity, camera.LookDirection) && GridBoundBoxes[i] != null && renderCheck.Intersects(GridBoundBoxes[i]))
                    {
                        QueueGridGen(i, Matrix4.Identity, camera.LookDirection);
                    }
                    continue;
                }

                if (SubHierarchies[i] == null)
                {
                    if (!IsEmptyHierarchies[i])
                    {
                        if (SubHirBoundBoxes[i] == null || renderCheck.Intersects(SubHirBoundBoxes[i]))
                        {
                            if (SubHirBoundBoxes[i] == null || SubHirNormals[i].CanSee(Matrix4.Identity, camera.LookDirection))
                            {
                                QueueHierarchyGen(i, Matrix4.Identity, camera.LookDirection);
                            }
                        }
                    }
                }
                else
                {
                    SubHierarchies[i]?.CheckAndIncreaseResolution(camera, renderCheck);
                }
            }
        }

        public void DrawMesh()
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null)
                    {
                        hir.DrawMesh();
                        continue;
                    }
                }

                if (!IsGeneratingGrids[i])
                {
                    VoxelGridInfo grid = Grids[i];
                    if (grid != null)
                    {
                        grid.DrawMesh();
                    }
                }
            }
        }

        public void DrawPoints()
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null)
                    {
                        hir.DrawPoints();
                        continue;
                    }
                }

                if (!IsGeneratingGrids[i])
                {
                    VoxelGridInfo grid = Grids[i];
                    if (grid != null)
                    {
                        grid.DrawPoints();
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
