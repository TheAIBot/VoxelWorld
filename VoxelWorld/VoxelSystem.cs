using OpenGL;
using System;
using System.Collections.Generic;
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

        public void Update(PlayerCamera camera, float yAngle)
        {
            Rotation = Matrix4.CreateRotationY(yAngle);
            RevRotation = Matrix4.CreateRotationY(-yAngle);
            RotatedLookDir = Rotation * camera.LookDirection;
            CameraPos = camera.CameraPos;
        }
    }


    internal class VoxelSystem
    {
        private readonly Dictionary<Vector3I, VoxelHierarchy> Grids = new Dictionary<Vector3I, VoxelHierarchy>();
        private readonly Vector3 Center;
        private float VoxelSize;
        private readonly int GridSize;
        private readonly Func<Vector3, float> WeightGen;
        private readonly ModelTransformations ModelTrans = new ModelTransformations();

        public Matrix4 Model { get { return ModelTrans.Rotation; } }

        public VoxelSystem(int gridSize, Vector3 center, float voxelSize, Func<Vector3, float> generator)
        {
            this.Center = center;
            this.VoxelSize = voxelSize;
            this.GridSize = gridSize;
            this.WeightGen = generator;
        }

        public void TestResizeToFindFirstGrid()
        {
            while (true)
            {
                Vector3I gridPos = new Vector3I(0, 0, 0);
                Vector3 gridCenter = Center + gridPos.AsFloatVector3() * GridSize * VoxelSize;
                VoxelGridInfo grid = new VoxelGridInfo(gridCenter);

                grid.GenerateGridAction(GridSize, VoxelSize, WeightGen, new Vector3(0, 0, 0))();
                if (grid.IsEmpty)
                {
                    grid.Dispose();
                    VoxelSize *= 2;
                    continue;
                }


                if (grid.VoxelsAtEdge)
                {
                    grid.Dispose();
                    VoxelSize *= 2;
                    continue;
                }


                grid.Dispose();

                VoxelHierarchy hir = new VoxelHierarchy(GridSize, gridCenter, VoxelSize, WeightGen, 0);
                hir.Generate(new Vector3(0, 0, 0));

                if (!TryAddGrid(gridPos, hir))
                {
                    Console.Error.WriteLine("failed to add hirearchy to system.");
                }



                break;
            }
        }

        private bool TryAddGrid(Vector3I gridPos, VoxelHierarchy grid)
        {
            lock (Grids)
            {
                if (!Grids.TryAdd(gridPos, grid))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryRemoveGrid(Vector3I gridPos)
        {
            lock (Grids)
            {
                return Grids.Remove(gridPos);
            }
        }

        public void UpdateModel(PlayerCamera camera, float yAngle)
        {
            ModelTrans.Update(camera, yAngle);
        }

        public void CheckVoxelResolution(Frustum renderCheck)
        {
            lock (Grids)
            {
                foreach (var grid in Grids.Values)
                {
                    grid.CheckAndIncreaseResolution(renderCheck, ModelTrans);
                }
            }
        }
    }
}
