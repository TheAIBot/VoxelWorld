using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace VoxelWorld.Render.Box
{
    internal static class BoxRenderManager
    {
        private static readonly BoxRender Render = new BoxRender();
        private static readonly ConcurrentQueue<BoxRenderCommand> BoxCommands = new ConcurrentQueue<BoxRenderCommand>();

        public static void AddBox(in Vector3 gridCenter, float gridSideLength)
        {
            BoxCommands.Enqueue(new BoxRenderCommand(BoxRenderCommandType.Add, new BoxRenderInfo(in gridCenter, gridSideLength / 2.0f)));
        }

        public static void RemoveBox(in Vector3 gridCenter)
        {
            BoxCommands.Enqueue(new BoxRenderCommand(BoxRenderCommandType.Remove, new BoxRenderInfo(in gridCenter, 0)));
        }

        public static void Draw()
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
                        Render.TryAdd(in boxCommand.BoxInfo);
                        break;
                    case BoxRenderCommandType.Remove:
                        Render.Remove(in boxCommand.BoxInfo.GridCenter);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(boxCommand.CType));
                }
            }

            Render.CopyToGPU();
            Render.Draw();
        }
    }
}
