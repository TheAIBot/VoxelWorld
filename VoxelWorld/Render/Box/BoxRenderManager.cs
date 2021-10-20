using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace VoxelWorld.Render.Box
{
    internal static class BoxRenderManager
    {
        private static readonly ConcurrentQueue<BoxRenderCommand> BoxCommands = new ConcurrentQueue<BoxRenderCommand>();
        private static readonly List<BoxRender> Renderers = new List<BoxRender>();
        private static readonly Dictionary<Vector3, BoxRender> GridCenterToBoxRenderer = new Dictionary<Vector3, BoxRender>();

        public static void AddBox(in Vector3 gridCenter, float gridSideLength)
        {
            BoxCommands.Enqueue(new BoxRenderCommand(BoxRenderCommandType.Add, new BoxRenderInfo(in gridCenter, gridSideLength / 2.0f)));
        }

        public static void RemoveBox(in Vector3 gridCenter)
        {
            BoxCommands.Enqueue(new BoxRenderCommand(BoxRenderCommandType.Remove, new BoxRenderInfo(in gridCenter, 0)));
        }

        public static void ProcessCommands()
        {
            int boxCommands = BoxCommands.Count;
            for (int i = 0; i < boxCommands; i++)
            {
                BoxRenderCommand boxCommand;
                if (!BoxCommands.TryDequeue(out boxCommand))
                {
                    throw new Exception("Expected a box command to be in the queue but there was none.");
                }

                switch (boxCommand.CType)
                {
                    case BoxRenderCommandType.Add:
                        AddBoxToRenderer(in boxCommand.BoxInfo);
                        break;
                    case BoxRenderCommandType.Remove:
                        RemoveBoxFromRenderer(in boxCommand.BoxInfo);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(boxCommand.CType));
                }
            }
        }

        private static void AddBoxToRenderer(in BoxRenderInfo boxInfo)
        {
            while (true)
            {
                foreach (var renderer in Renderers)
                {
                    if (renderer.TryAdd(in boxInfo))
                    {
                        GridCenterToBoxRenderer.Add(boxInfo.GridCenter, renderer);
                        return;
                    }
                }

                Renderers.Add(new BoxRender());
            }
        }

        private static void RemoveBoxFromRenderer(in BoxRenderInfo boxInfo)
        {
            if (GridCenterToBoxRenderer.TryGetValue(boxInfo.GridCenter, out BoxRender boxRenderer))
            {
                GridCenterToBoxRenderer.Remove(boxInfo.GridCenter);
                boxRenderer.Remove(in boxInfo.GridCenter);
            }
            else
            {
                throw new Exception("Failed to find the renderer that the box resides in.");
            }
        }

        public static void Draw()
        {
            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderers[i].CopyToGPU();
            }
            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderers[i].Draw();
            }
        }
    }
}
