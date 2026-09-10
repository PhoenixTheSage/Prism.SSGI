using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ClientPlugin.Common
{
    public class FileShaderCompiler : IShaderCompiler
    {
        private class FileIncludeHandler : Include
        {
            public IDisposable Shadow { get; set; }

            private readonly string _compileFilePath;
            private readonly string _baseSystemIncludePath;

            public FileIncludeHandler(string compileFilePath, string baseSystemIncludePath)
            {
                _compileFilePath = Path.GetDirectoryName(compileFilePath);
                _baseSystemIncludePath = baseSystemIncludePath;
            }

            public void Close(Stream stream)
            {
                stream.Dispose();
            }

            public Stream Open(IncludeType type, string fileName, Stream parentStream)
            {
                if (fileName == null)
                {
                    throw new ArgumentNullException(nameof(fileName));
                }

                string includeFilePath;
                if (type is IncludeType.Local)
                {
                    if (parentStream is FileStream fs)
                    {
                        string parentFilePath = Path.GetDirectoryName(fs.Name);
                        includeFilePath = Path.Combine(parentFilePath, fileName);
                    }
                    else
                    {
                        includeFilePath = Path.Combine(_compileFilePath, fileName);
                    }
                }
                else if (type is IncludeType.System)
                {
                    includeFilePath = Path.Combine(_baseSystemIncludePath, fileName);
                }
                else
                {
                    throw new ArgumentException($"Invalid {nameof(IncludeType)}.");
                }

                return File.OpenRead(includeFilePath);
            }

            public void Dispose()
            {
                Shadow?.Dispose();
            }
        }

        private readonly string _baseShaderPath;
        private readonly string _gameShaderBasePath;
        private readonly string? _shaderCacheDirectory;

        public FileShaderCompiler(string baseShaderPath, string gameShaderBasePath)
            : this(baseShaderPath, gameShaderBasePath, null)
        {
        }

        public FileShaderCompiler(string baseShaderPath, string gameShaderBasePath, string? shaderCacheDirectory = null)
        {
            _baseShaderPath = baseShaderPath;
            _gameShaderBasePath = gameShaderBasePath;
            _shaderCacheDirectory = shaderCacheDirectory;
        }

        public VertexShader CompileVertex(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompileVertexBytecode(id, entryPoint, defines);
            return new VertexShader(device, compilation);
        }

        public PixelShader CompilePixel(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompilePixelBytecode(id, entryPoint, defines);
            return new PixelShader(device, compilation);
        }

        public ComputeShader CompileCompute(Device device, string id, string entryPoint, params ShaderMacro[] defines)
        {
            CompilationResult compilation = CompileComputeBytecode(id, entryPoint, defines);
            return new ComputeShader(device, compilation);
        }

        public CompilationResult CompileVertexBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "vs_5_0", defines);
        public CompilationResult CompilePixelBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "ps_5_0", defines);
        public CompilationResult CompileComputeBytecode(string id, string entryPoint, params ShaderMacro[] defines) => CompileBytecodeInternal(id, entryPoint, "cs_5_0", defines);

        private CompilationResult CompileBytecodeInternal(string id, string entryPoint, string profile, ShaderMacro[] defines)
        {
            string filePath = Path.Combine(_baseShaderPath, id);
            using StreamReader sr = new(filePath, Encoding.UTF8);
            string shaderSource = sr.ReadToEnd();

            if (string.IsNullOrEmpty(shaderSource))
            {
                throw new ArgumentNullException(nameof(shaderSource));
            }

            using Include include = new FileIncludeHandler(filePath, _gameShaderBasePath);
            string preprocessedSource = ShaderBytecode.Preprocess(shaderSource, defines, include, out string preprocessErrors, filePath);
            if (preprocessedSource == null)
            {
                throw new Exception($"Shader preprocessing failed. Reason: {preprocessErrors}");
            }

            string cacheFilePath = null;
            if (_shaderCacheDirectory != null)
            {
                using SHA1 sha1 = SHA1.Create();

                byte[] entryPointHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(entryPoint));
                byte[] profileHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(profile));
                byte[] shaderSourceBytes = Encoding.UTF8.GetBytes(preprocessedSource);

                byte[] shaderHashDataBuffer = new byte[entryPointHash.Length + profileHash.Length + shaderSourceBytes.Length];
                shaderSourceBytes.CopyTo(shaderHashDataBuffer, 0);
                entryPointHash   .CopyTo(shaderHashDataBuffer, shaderSourceBytes.Length);
                profileHash      .CopyTo(shaderHashDataBuffer, shaderSourceBytes.Length + entryPointHash.Length);

                string shaderHash = BitConverter.ToString(sha1.ComputeHash(shaderHashDataBuffer)).Replace("-", "");
                cacheFilePath = Path.Combine(_shaderCacheDirectory, shaderHash + ".cache");
                if (TryLoadCachedBytecode(cacheFilePath, out byte[] bytecode))
                {
                    return new CompilationResult(new ShaderBytecode(bytecode), Result.Ok, string.Empty);
                }
            }

            // NOTE: don't use ShaderBytecode.Compile(string str, ..) because it triggers buggy code in the ProjectedLights plugin that crashes the game.
            IntPtr sourcePtr = Marshal.StringToHGlobalAnsi(shaderSource);
            try
            {
                CompilationResult result = ShaderBytecode.Compile(sourcePtr, shaderSource.Length, entryPoint, profile, ShaderFlags.OptimizationLevel3, EffectFlags.None, defines, include);
                if (result.ResultCode == Result.Ok && cacheFilePath != null)
                {
                    CacheBytecode(cacheFilePath, result.Bytecode.Data);
                }
                return result;
            }
            finally
            {
                if (sourcePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(sourcePtr);
                }
            }
        }

        private static bool TryLoadCachedBytecode(string filePath, out byte[]? bytecode)
        {
            if (File.Exists(filePath))
            {
                bytecode = File.ReadAllBytes(filePath);
                return bytecode != null && bytecode.Length > 0;
            }
            bytecode = null;
            return false;
        }

        private static void CacheBytecode(string filePath, byte[] bytecode)
        {
            if (filePath.EndsWith(".cache"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllBytes(filePath, bytecode);
            }
        }

        private static void DeleteCachedBytecode(string filePath)
        {
            // make sure we're deleting a shader cache file
            if (filePath.EndsWith(".cache"))
            {
                File.Delete(filePath);
            }
        }
    }
}
