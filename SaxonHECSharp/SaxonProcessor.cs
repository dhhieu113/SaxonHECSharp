using SaxonHECSharp.NativeInterop;

namespace SaxonHECSharp
{
    public class SaxonProcessor : IDisposable
    {
        private IntPtr _handle;
        private IntPtr _isolate;
        private IntPtr _thread;
        private bool _disposed;

        public SaxonProcessor()
        {
            var result = SaxonNative.graal_create_isolate(IntPtr.Zero, out _isolate, out _thread);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to create Graal isolate");
            }

            _handle = SaxonNative.createSaxonProcessor(_thread, 1); // 1 indicates licensed
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to initialize Saxon processor");
            }
        }

        public XsltProcessor CreateXsltProcessor()
        {
            ThrowIfDisposed();
            return new XsltProcessor(_handle, _thread);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_thread != IntPtr.Zero)
                {
                    SaxonNative.j_gc(_thread);
                }
                _disposed = true;
                _handle = IntPtr.Zero;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SaxonProcessor));
            }
        }
    }
}
