using System.Runtime.InteropServices;
using SaxonHECSharp.NativeInterop;

namespace SaxonHECSharp
{
    public class XsltProcessor : IDisposable
    {
        private IntPtr _handle;
        private readonly IntPtr _procHandle;
        private readonly IntPtr _thread;
        private bool _disposed;

        internal XsltProcessor(IntPtr procHandle, IntPtr thread)
        {
            _procHandle = procHandle;
            _thread = thread;
            _handle = SaxonNative.createXslt30Processor(_thread);
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create XSLT processor");
            }
        }

        public void CompileStylesheet(string stylesheetFilePath)
        {
            ThrowIfDisposed();
            IntPtr result = SaxonNative.j_compileFromFile(_thread, _handle, stylesheetFilePath, null, 0);
            if (result == IntPtr.Zero)
            {
                IntPtr errorPtr = SaxonNative.j_getErrorMessage(_thread);
                string error = errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
                throw new Exception($"XSLT compilation failed: {error}");
            }
        }

        public bool Transform(string sourceFilePath, string outputPath = null)
        {
            ThrowIfDisposed();

            if (outputPath == null)
            {
                IntPtr result = SaxonNative.j_transformToValue(_thread, _handle, sourceFilePath, null);
                if (result == IntPtr.Zero)
                {
                    IntPtr errorPtr = SaxonNative.j_getErrorMessage(_thread);
                    string error = errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
                    throw new Exception($"XSLT transformation failed: {error}");
                }
                return true;
            }
            else
            {
                IntPtr result = SaxonNative.j_transformToFile(_thread, outputPath, _handle, sourceFilePath, null, null);
                if (result == IntPtr.Zero)
                {
                    IntPtr errorPtr = SaxonNative.j_getErrorMessage(_thread);
                    string error = errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
                    throw new Exception($"XSLT transformation failed: {error}");
                }
                return true;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    // Note: The native library should have a corresponding release function
                    // which we would call here. Add it when available.
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(XsltProcessor));
            }
        }
    }
}
