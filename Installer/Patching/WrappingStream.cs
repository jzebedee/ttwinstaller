/*
Copyright 2007-2013 Logos Bible Software

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaleOfTwoWastelands.Patching
{
    /// <summary>
    /// A <see cref="Stream"/> that wraps another stream. One major feature of <see cref="WrappingStream"/> is that it does not dispose the
    /// underlying stream when it is disposed if disposeUnderlying = false is used; this is useful when using classes such as <see cref="BinaryReader"/> and
    /// <see cref="System.Security.Cryptography.CryptoStream"/> that take ownership of the stream passed to their constructors.
    /// </summary>
    /// <remarks>See <a href="http://code.logos.com/blog/2009/05/wrappingstream_implementation.html">WrappingStream Implementation</a>.</remarks>
    public class WrappingStream : Stream
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WrappingStream"/> class.
        /// </summary>
        /// <param name="streamBase">The wrapped stream.</param>
        /// <param name="disposeUnderlying">Use true if the wrapped stream should be disposed when this stream is disposed.</param>
        public WrappingStream(Stream streamBase, bool disposeUnderlying)
        {
            // check parameters
            if (streamBase == null)
                throw new ArgumentNullException("streamBase");

            m_streamBase = streamBase;
            m_disposeUnderlying = disposeUnderlying;
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</returns>
        public override bool CanRead
        {
            get { return m_streamBase == null ? false : m_streamBase.CanRead; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</returns>
        public override bool CanSeek
        {
            get { return m_streamBase == null ? false : m_streamBase.CanSeek; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</returns>
        public override bool CanWrite
        {
            get { return m_streamBase == null ? false : m_streamBase.CanWrite; }
        }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length
        {
            get { ThrowIfDisposed(); return m_streamBase.Length; }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public override long Position
        {
            get { ThrowIfDisposed(); return m_streamBase.Position; }
            set { ThrowIfDisposed(); m_streamBase.Position = value; }
        }

        /// <summary>
        /// Begins an asynchronous read operation.
        /// </summary>
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            return m_streamBase.BeginRead(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous write operation.
        /// </summary>
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            return m_streamBase.BeginWrite(buffer, offset, count, callback, state);
        }

        /// <summary>
        /// Waits for the pending asynchronous read to complete.
        /// </summary>
        public override int EndRead(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            return m_streamBase.EndRead(asyncResult);
        }

        /// <summary>
        /// Ends an asynchronous write operation.
        /// </summary>
        public override void EndWrite(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            m_streamBase.EndWrite(asyncResult);
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
            ThrowIfDisposed();
            m_streamBase.Flush();
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position
        /// within the stream by the number of bytes read.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            return m_streamBase.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
        /// </summary>
        public override int ReadByte()
        {
            ThrowIfDisposed();
            return m_streamBase.ReadByte();
        }

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            return m_streamBase.Seek(offset, origin);
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            m_streamBase.SetLength(value);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position
        /// within this stream by the number of bytes written.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            m_streamBase.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
        /// </summary>
        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();
            m_streamBase.WriteByte(value);
        }

        /// <summary>
        /// Gets the wrapped stream.
        /// </summary>
        /// <value>The wrapped stream.</value>
        protected Stream WrappedStream
        {
            get { return m_streamBase; }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="WrappingStream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                // doesn't close the base stream, but just prevents access to it through this WrappingStream
                if (disposing)
                {
                    if (m_streamBase != null && m_disposeUnderlying)
                        m_streamBase.Dispose();
                    m_streamBase = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ThrowIfDisposed()
        {
            // throws an ObjectDisposedException if this object has been disposed
            if (m_streamBase == null)
                throw new ObjectDisposedException(GetType().Name);
        }

        Stream m_streamBase;
        readonly bool m_disposeUnderlying;
    }
}
