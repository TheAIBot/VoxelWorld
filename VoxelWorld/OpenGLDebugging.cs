using Silk.NET.OpenGL;
using System;

namespace VoxelWorld
{
    public class OpenGLDebugging
    {
        private GL _openGl;

        public OpenGLDebugging(GL openGl)
        {
            _openGl = openGl;
            EnableDebugOutput();
        }

        // This method enables OpenGL debug output and sets the callback
        private void EnableDebugOutput()
        {
            // Enable debug output
            _openGl.Enable(GLEnum.DebugOutput);
            _openGl.Enable(GLEnum.DebugOutputSynchronous); // For synchronous callback

            // Set the debug message callback
            DebugProc callback = OpenGLDebugCallback;
            _openGl.DebugMessageCallback(callback, IntPtr.Zero);

            // Optionally, control the severity of the messages you want to receive
            _openGl.DebugMessageControl(GLEnum.DontCare, GLEnum.DontCare, GLEnum.DontCare, 0, 0, true);
        }

        // Callback function for OpenGL debug messages
        private static void OpenGLDebugCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam)
        {
            string messageString = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);

            // Filter out notifications (optional)
            //if (severity == GLEnum.DebugSeverityNotification) return;

            Console.WriteLine("OpenGL Debug Message:");
            Console.WriteLine($"Source: {GetSourceString(source)}");
            Console.WriteLine($"Type: {GetTypeString(type)}");
            Console.WriteLine($"Severity: {GetSeverityString(severity)}");
            Console.WriteLine($"Message: {messageString}");
            Console.WriteLine($"ID: {id}");
        }

        // Helper method to convert the source enum to a string
        private static string GetSourceString(GLEnum source)
        {
            return source switch
            {
                GLEnum.DebugSourceApi => "API",
                GLEnum.DebugSourceWindowSystem => "Window System",
                GLEnum.DebugSourceShaderCompiler => "Shader Compiler",
                GLEnum.DebugSourceThirdParty => "Third Party",
                GLEnum.DebugSourceApplication => "Application",
                GLEnum.DebugSourceOther => "Other",
                _ => "Unknown"
            };
        }

        // Helper method to convert the type enum to a string
        private static string GetTypeString(GLEnum type)
        {
            return type switch
            {
                GLEnum.DebugTypeError => "Error",
                GLEnum.DebugTypeDeprecatedBehavior => "Deprecated Behavior",
                GLEnum.DebugTypeUndefinedBehavior => "Undefined Behavior",
                GLEnum.DebugTypePortability => "Portability",
                GLEnum.DebugTypePerformance => "Performance",
                GLEnum.DebugTypeMarker => "Marker",
                GLEnum.DebugTypePushGroup => "Push Group",
                GLEnum.DebugTypePopGroup => "Pop Group",
                GLEnum.DebugTypeOther => "Other",
                _ => "Unknown"
            };
        }

        // Helper method to convert the severity enum to a string
        private static string GetSeverityString(GLEnum severity)
        {
            return severity switch
            {
                GLEnum.DebugSeverityHigh => "High",
                GLEnum.DebugSeverityMedium => "Medium",
                GLEnum.DebugSeverityLow => "Low",
                GLEnum.DebugSeverityNotification => "Notification",
                _ => "Unknown"
            };
        }
    }
}
