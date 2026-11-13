using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Railbound.Editor
{
    /// <summary>
    /// Removes every shader and compute shader variant from the dedicated server build.
    ///
    /// The server runs headless (-batchmode -nographics, null graphics device) and never draws a frame, but a normal player build
    /// still compiles the whole HDRP shader library for it: hundreds of shaders, thousands of variants, about 16 compiler
    /// processes at 100% CPU for tens of minutes. None of that output can ever run on the server.
    ///
    /// It only strips while <see cref="Active"/> is set, which <see cref="NetcodeServerBuildPipeline"/> does around its own
    /// BuildPlayer call, so client builds and the editor are not touched.
    /// Set the environment variable RAILBOUND_KEEP_SERVER_SHADERS=1 to turn it off (for example to debug a rendering issue).
    /// </summary>
    public sealed class ServerShaderStripping : IPreprocessShaders, IPreprocessComputeShaders
    {
        /// <summary>True while a dedicated server build is running.</summary>
        public static bool Active;

        /// <summary>How many variants were removed in the current build (for the build log).</summary>
        public static int StrippedVariants { get; private set; }

        public int callbackOrder => int.MaxValue;   // after every other stripper

        public static void BeginServerBuild()
        {
            StrippedVariants = 0;
            Active = Environment.GetEnvironmentVariable("RAILBOUND_KEEP_SERVER_SHADERS") != "1";
            if (!Active) Debug.Log("[ServerBuild] Shader stripping is off (RAILBOUND_KEEP_SERVER_SHADERS=1).");
        }

        public static void EndServerBuild()
        {
            if (Active) Debug.Log($"[ServerBuild] Server shader stripping removed {StrippedVariants} shader variants.");
            Active = false;
        }

        // Both conditions: the flag (set around the server BuildPlayer call) AND the Dedicated Server subtarget. Even if the flag
        // were ever left on by mistake, a client / player build (subtarget Player) keeps all of its shaders.
        private static bool ShouldStrip =>
            Active && UnityEditor.EditorUserBuildSettings.standaloneBuildSubtarget == UnityEditor.StandaloneBuildSubtarget.Server;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!ShouldStrip) return;
            StrippedVariants += data.Count;
            data.Clear();
        }

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            if (!ShouldStrip) return;
            StrippedVariants += data.Count;
            data.Clear();
        }
    }
}
