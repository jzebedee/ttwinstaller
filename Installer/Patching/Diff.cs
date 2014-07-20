#define PATCH
#define CREATE
/*
    The original bsdiff.c source code (http://www.daemonology.net/bsdiff/) is
    distributed under the following license:

    Copyright 2003-2005 Colin Percival
    All rights reserved

    Redistribution and use in source and binary forms, with or without
    modification, are permitted providing that the following conditions 
    are met:
    1. Redistributions of source code must retain the above copyright
        notice, this list of conditions and the following disclaimer.
    2. Redistributions in binary form must reproduce the above copyright
        notice, this list of conditions and the following disclaimer in the
        documentation and/or other materials provided with the distribution.

    THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
    IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
    WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
    ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
    DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
    DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
    OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
    HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
    STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
    IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
    POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.BZip2;
using SevenZip;

namespace TaleOfTwoWastelands.Patching
{
    public static class Diff
    {
        public const long
            SIG_BSDIFF40 = 0x3034464649445342,
            SIG_LZDIFF41 = 0x3134464649445a4c,
            SIG_NONONONO = 0x4f4e4f4e4f4e4f4e;
        public const int HEADER_SIZE = 32;
        public const int BUFFER_SIZE = 0x100000;

        private const int LZMA_DICTSIZE_ULTRA = 1024 * 1024 * 64; //64MiB, 7z 'Ultra'

        private static void SetCompressionLevel()
        {
            SevenZipCompressor.LzmaDictionarySize = LZMA_DICTSIZE_ULTRA;
        }

        private static Stream GetEncodingStream(Stream stream, long signature, bool output)
        {
            switch (signature)
            {
                case SIG_LZDIFF41:
                    if (output)
                    {
                        SetCompressionLevel();
                        return new LzmaEncodeStream(stream);
                    }
                    else
                        return new LzmaDecodeStream(stream);
                case SIG_NONONONO:
                    return stream;
                case SIG_BSDIFF40:
                    if (output)
                        return new BZip2OutputStream(stream) { IsStreamOwner = false };
                    else
                        return new BZip2InputStream(stream);
                default:
                    throw new ArgumentException("unknown encoding type");
            }
        }

        /// <summary>
        /// Used only in PatchMaker
        /// </summary>
        internal static unsafe byte[] ConvertPatch(byte* pBz2, long length, long inputSig, long outputSig)
        {
            if (inputSig == outputSig)
                throw new ArgumentException("output must be different from input");

            Func<long, long, Stream> openPatchStream = (u_offset, u_length) =>
                new UnmanagedMemoryStream(pBz2 + u_offset, u_length > 0 ? u_length : length - u_offset);

            /*
             File format:
                 0	    8	"BSDIFF40"
                 8	    8	X
                 16	    8	Y
                 24	    8	sizeof(newfile)
                 32      X	bzip2(control block)
                 32+X	Y	bzip2(diff block)
                 32+X+Y	???	bzip2(extra block)
             with control block a set of triples (x,y,z) meaning "add x bytes
             from oldfile to x bytes from the diff block; copy y bytes from the
             extra block; seek forwards in oldfile by z bytes".
             */

            byte[] header = new byte[HEADER_SIZE];
            using (var inputStream = openPatchStream(0, HEADER_SIZE))
                inputStream.Read(header, 0, HEADER_SIZE);

            // check for appropriate magic
            long signature = ReadInt64(header, 0);
            if (signature != inputSig)
                throw new InvalidOperationException("Corrupt patch.");

            // read lengths from header
            var controlLength = ReadInt64(header, sizeof(long));
            var diffLength = ReadInt64(header, sizeof(long) * 2);
            var newSize = ReadInt64(header, sizeof(long) * 3);
            if (controlLength < 0 || diffLength < 0 || newSize < 0)
                throw new InvalidOperationException("Corrupt patch.");

            byte[] outControlBytes, outDiffBytes, outExtraBytes;

            using (MemoryStream
                msControlStream = new MemoryStream(),
                msDiffStream = new MemoryStream(),
                msExtraStream = new MemoryStream())
            {
                using (Stream
                    inControlStream = openPatchStream(HEADER_SIZE, controlLength),
                    inDiffStream = openPatchStream(HEADER_SIZE + controlLength, diffLength),
                    inExtraStream = openPatchStream(HEADER_SIZE + controlLength + diffLength, -1),

                    bz2ControlStream = GetEncodingStream(inControlStream, inputSig, false),
                    bz2DiffStream = GetEncodingStream(inDiffStream, inputSig, false),
                    bz2ExtraStream = GetEncodingStream(inExtraStream, inputSig, false),

                    lzmaControlStream = GetEncodingStream(msControlStream, outputSig, true),
                    lzmaDiffStream = GetEncodingStream(msDiffStream, outputSig, true),
                    lzmaExtraStream = GetEncodingStream(msExtraStream, outputSig, true))
                {
                    bz2ControlStream.CopyTo(lzmaControlStream);
                    bz2DiffStream.CopyTo(lzmaDiffStream);
                    bz2ExtraStream.CopyTo(lzmaExtraStream);
                }

                outControlBytes = msControlStream.ToArray();
                outDiffBytes = msDiffStream.ToArray();
                outExtraBytes = msExtraStream.ToArray();
            }

            using (var msOut = new MemoryStream())
            {
                var buf = new byte[8];

                //
                WriteInt64(outputSig, buf, 0);
                msOut.Write(buf, 0, buf.Length);

                //
                WriteInt64(outControlBytes.LongLength, buf, 0);
                msOut.Write(buf, 0, buf.Length);

                WriteInt64(outDiffBytes.LongLength, buf, 0);
                msOut.Write(buf, 0, buf.Length);

                WriteInt64(newSize, buf, 0);
                msOut.Write(buf, 0, buf.Length);

                //
                Debug.Assert(outControlBytes.Length == outControlBytes.LongLength);
                msOut.Write(outControlBytes, 0, outControlBytes.Length);

                Debug.Assert(outDiffBytes.Length == outDiffBytes.LongLength);
                msOut.Write(outDiffBytes, 0, outDiffBytes.Length);

                Debug.Assert(outExtraBytes.Length == outExtraBytes.LongLength);
                msOut.Write(outExtraBytes, 0, outExtraBytes.Length);

                return msOut.ToArray();
            }
        }

#if CREATE
        /// <summary>
        /// Creates a binary patch (in <a href="http://www.daemonology.net/bsdiff/">bsdiff</a> format) that can be used
        /// (by <see cref="Apply"/>) to transform <paramref name="oldData"/> into <paramref name="newData"/>.
        /// </summary>
        /// <param name="oldData">The original binary data.</param>
        /// <param name="newData">The new binary data.</param>
        /// <param name="output">A <see cref="Stream"/> to which the patch will be written.</param>
        public static unsafe void Create(byte[] oldBuf, byte[] newBuf, long signature, Stream output)
        {
            // check arguments
            if (oldBuf == null)
                throw new ArgumentNullException("oldData");
            if (newBuf == null)
                throw new ArgumentNullException("newData");
            if (output == null)
                throw new ArgumentNullException("output");
            if (!output.CanSeek)
                throw new ArgumentException("Output stream must be seekable.", "output");
            if (!output.CanWrite)
                throw new ArgumentException("Output stream must be writable.", "output");

            /* Header is
                0	8	 "BSDIFF40"
                8	8	length of bzip2ed ctrl block
                16	8	length of bzip2ed diff block
                24	8	length of new file */
            /* File is
                0	32	Header
                32	??	Bzip2ed ctrl block
                ??	??	Bzip2ed diff block
                ??	??	Bzip2ed extra block */
            byte[] header = new byte[HEADER_SIZE];
            WriteInt64(signature, header, 0);
            WriteInt64(newBuf.LongLength, header, 24);

            long startPosition = output.Position;
            output.Write(header, 0, header.Length);

            fixed (byte* oldData = oldBuf)
            fixed (byte* newData = newBuf)
            {
                var bufI = new int[oldBuf.Length];
                SAIS.sufsort(oldBuf, bufI, oldBuf.Length);

                byte[] db = new byte[newBuf.Length];
                byte[] eb = new byte[newBuf.Length];

                int dblen = 0;
                int eblen = 0;

                using (var controlStream = GetEncodingStream(output, signature, true))
                {
                    // compute the differences, writing ctrl as we go
                    int scan = 0;
                    int pos = 0;
                    int len = 0;
                    int lastscan = 0;
                    int lastpos = 0;
                    int lastoffset = 0;
                    while (scan < newBuf.Length)
                    {
                        int oldscore = 0;

                        for (int scsc = scan += len; scan < newBuf.Length; scan++)
                        {
                            fixed (int* I = bufI)
                                len = Search(I, oldData, oldBuf.Length, newData, newBuf.Length, scan, 0, oldBuf.Length, out pos);

                            for (; scsc < scan + len; scsc++)
                            {
                                if ((scsc + lastoffset < oldBuf.Length) && (oldData[scsc + lastoffset] == newData[scsc]))
                                    oldscore++;
                            }

                            if ((len == oldscore && len != 0) || (len > oldscore + 8))
                                break;

                            if ((scan + lastoffset < oldBuf.Length) && (oldData[scan + lastoffset] == newData[scan]))
                                oldscore--;
                        }

                        if (len != oldscore || scan == newBuf.Length)
                        {
                            int s = 0;
                            int sf = 0;
                            int lenf = 0;
                            for (int i = 0; (lastscan + i < scan) && (lastpos + i < oldBuf.Length); )
                            {
                                if (oldData[lastpos + i] == newData[lastscan + i])
                                    s++;
                                i++;
                                if (s * 2 - i > sf * 2 - lenf)
                                {
                                    sf = s;
                                    lenf = i;
                                }
                            }

                            int lenb = 0;
                            if (scan < newBuf.Length)
                            {
                                s = 0;
                                int sb = 0;
                                for (int i = 1; (scan >= lastscan + i) && (pos >= i); i++)
                                {
                                    if (oldData[pos - i] == newData[scan - i])
                                        s++;
                                    if (s * 2 - i > sb * 2 - lenb)
                                    {
                                        sb = s;
                                        lenb = i;
                                    }
                                }
                            }

                            if (lastscan + lenf > scan - lenb)
                            {
                                int overlap = (lastscan + lenf) - (scan - lenb);
                                s = 0;
                                int ss = 0;
                                int lens = 0;
                                for (int i = 0; i < overlap; i++)
                                {
                                    if (newData[lastscan + lenf - overlap + i] == oldData[lastpos + lenf - overlap + i])
                                        s++;
                                    if (newData[scan - lenb + i] == oldData[pos - lenb + i])
                                        s--;
                                    if (s > ss)
                                    {
                                        ss = s;
                                        lens = i + 1;
                                    }
                                }

                                lenf += lens - overlap;
                                lenb -= lens;
                            }

                            for (int i = 0; i < lenf; i++)
                                db[dblen + i] = (byte)(newData[lastscan + i] - oldData[lastpos + i]);
                            for (int i = 0; i < (scan - lenb) - (lastscan + lenf); i++)
                                eb[eblen + i] = newData[lastscan + lenf + i];

                            dblen += lenf;
                            eblen += (scan - lenb) - (lastscan + lenf);

                            byte[] buf = new byte[8];
                            WriteInt64(lenf, buf, 0);
                            controlStream.Write(buf, 0, 8);

                            WriteInt64((scan - lenb) - (lastscan + lenf), buf, 0);
                            controlStream.Write(buf, 0, 8);

                            WriteInt64((pos - lenb) - (lastpos + lenf), buf, 0);
                            controlStream.Write(buf, 0, 8);

                            lastscan = scan - lenb;
                            lastpos = pos - lenb;
                            lastoffset = pos - scan;
                        }
                    }
                }

                // compute size of compressed ctrl data
                long controlEndPosition = output.Position;
                WriteInt64(controlEndPosition - startPosition - HEADER_SIZE, header, 8);

                // write compressed diff data
                using (var diffStream = GetEncodingStream(output, signature, true))
                    diffStream.Write(db, 0, dblen);

                // compute size of compressed diff data
                long diffEndPosition = output.Position;
                WriteInt64(diffEndPosition - controlEndPosition, header, 16);

                // write compressed extra data
                using (var extraStream = GetEncodingStream(output, signature, true))
                    extraStream.Write(eb, 0, eblen);

                // seek to the beginning, write the header, then seek back to end
                long endPosition = output.Position;
                output.Position = startPosition;
                output.Write(header, 0, header.Length);
                output.Position = endPosition;
            }
        }


        private static unsafe int CompareBytes(byte* left, int leftLength, byte* right, int rightLength)
        {
            int diff = 0;
            for (int i = 0; i < leftLength && i < rightLength; i++)
            {
                diff = left[i] - right[i];
                if (diff != 0)
                    break;
            }
            return diff;
        }

        private static unsafe int MatchLength(byte* oldData, int oldLength, byte* newData, int newLength)
        {
            int i;
            for (i = 0; i < oldLength && i < newLength; i++)
            {
                if (oldData[i] != newData[i])
                    break;
            }

            return i;
        }

        private static unsafe int Search(int* I, byte* oldData, int oldLength, byte* newData, int newLength, int newOffset, int start, int end, out int pos)
        {
            if (end - start < 2)
            {
                int startLength = MatchLength((oldData + I[start]), oldLength, (newData + newOffset), newLength);
                int endLength = MatchLength((oldData + I[end]), oldLength, (newData + newOffset), newLength);

                if (startLength > endLength)
                {
                    pos = I[start];
                    return startLength;
                }
                else
                {
                    pos = I[end];
                    return endLength;
                }
            }
            else
            {
                int midPoint = start + (end - start) / 2;
                return CompareBytes((oldData + I[midPoint]), oldLength, (newData + newOffset), newLength) < 0 ?
                    Search(I, oldData, oldLength, newData, newLength, newOffset, midPoint, end, out pos) :
                    Search(I, oldData, oldLength, newData, newLength, newOffset, start, midPoint, out pos);
            }
        }

        private static unsafe void WriteInt64(long value, byte[] buf, int offset)
        {
            long y = value < 0 ? -value : value;

            fixed (byte* pb = &buf[offset])
            {
                pb[0] = (byte)(y % 256);
                y -= pb[0];
                y = y / 256; pb[1] = (byte)(y % 256); y -= pb[1];
                y = y / 256; pb[2] = (byte)(y % 256); y -= pb[2];
                y = y / 256; pb[3] = (byte)(y % 256); y -= pb[3];
                y = y / 256; pb[4] = (byte)(y % 256); y -= pb[4];
                y = y / 256; pb[5] = (byte)(y % 256); y -= pb[5];
                y = y / 256; pb[6] = (byte)(y % 256); y -= pb[6];
                y = y / 256; pb[7] = (byte)(y % 256);

                if (value < 0)
                    pb[7] |= 0x80;
            }
        }
#endif

#if PATCH
        /// <summary>
        /// Applies a binary patch a quasi-(in <a href="http://www.daemonology.net/bsdiff/">bsdiff</a> format) to the data in
        /// <paramref name="input"/> and writes the results of patching to <paramref name="output"/>.
        /// </summary>
        /// <param name="input">A <see cref="Stream"/> containing the input data.</param>
        /// <param name="openPatchStream">A func that can open a <see cref="Stream"/> positioned at the start of the patch data.
        /// This stream must support reading and seeking, and <paramref name="openPatchStream"/> must allow multiple streams on
        /// the patch to be opened concurrently.</param>
        /// <param name="output">A <see cref="Stream"/> to which the patched data is written.</param>
        public static unsafe void Apply(byte* pInput, long length, byte* pPatch, long patchLength, Stream output)
        {
            Func<long, long, Stream> openPatchStream = (u_offset, u_length) =>
                new UnmanagedMemoryStream(pPatch + u_offset, u_length > 0 ? u_length : patchLength - u_offset);

            // check arguments
            if (pInput == null)
                throw new ArgumentNullException("input");
            if (openPatchStream == null)
                throw new ArgumentNullException("openPatchStream");
            if (output == null)
                throw new ArgumentNullException("output");

            /*
            File format:
                0	    8	"BSDIFF40"
                8	    8	X
                16	    8	Y
                24	    8	sizeof(newfile)
                32      X	bzip2(control block)
                32+X	Y	bzip2(diff block)
                32+X+Y	???	bzip2(extra block)
            with control block a set of triples (x,y,z) meaning "add x bytes
            from oldfile to x bytes from the diff block; copy y bytes from the
            extra block; seek forwards in oldfile by z bytes".
            */

            // read header
            long controlLength, diffLength, newSize, signature;
            using (Stream patchStream = openPatchStream(0, HEADER_SIZE))
            {
                // check patch stream capabilities
                if (!patchStream.CanRead)
                    throw new ArgumentException("Patch stream must be readable.", "openPatchStream");
                if (!patchStream.CanSeek)
                    throw new ArgumentException("Patch stream must be seekable.", "openPatchStream");

                byte[] header = new byte[HEADER_SIZE];
                patchStream.Read(header, 0, HEADER_SIZE);

                // check for appropriate magic
                signature = ReadInt64(header, 0);

                // read lengths from header
                controlLength = ReadInt64(header, 8);
                diffLength = ReadInt64(header, 16);
                newSize = ReadInt64(header, 24);
                if (controlLength < 0 || diffLength < 0 || newSize < 0)
                    throw new InvalidOperationException("Corrupt patch.");
            }

            // preallocate buffers for reading and writing
            var newData = new byte[BUFFER_SIZE];

            // prepare to read three parts of the patch in parallel
            using (Stream
                compressedControlStream = openPatchStream(HEADER_SIZE, controlLength),
                compressedDiffStream = openPatchStream(HEADER_SIZE + controlLength, diffLength),
                compressedExtraStream = openPatchStream(HEADER_SIZE + controlLength + diffLength, -1),

                 // decompress each part (to read it)
                controlStream = GetEncodingStream(compressedControlStream, signature, false),
                diffStream = GetEncodingStream(compressedDiffStream, signature, false),
                extraStream = GetEncodingStream(compressedExtraStream, signature, false))
            {
                long[] control = new long[3];
                byte[] buffer = new byte[8];

                int oldPosition = 0;
                int newPosition = 0;
                while (newPosition < newSize)
                {
                    // read control data
                    for (int i = 0; i < 3; i++)
                    {
                        controlStream.Read(buffer, 0, 8);
                        control[i] = ReadInt64(buffer, 0);

                        Debug.Assert((int)control[i] == control[i]);
                    }

                    // sanity-check
                    if (newPosition + control[0] > newSize)
                        throw new InvalidOperationException("Corrupt patch.");

                    int bytesToCopy = (int)control[0];
                    while (bytesToCopy > 0)
                    {
                        int actualBytesToCopy = Math.Min(bytesToCopy, BUFFER_SIZE);

                        // read diff string
                        diffStream.Read(newData, 0, actualBytesToCopy);

                        // add old data to diff string
                        int availableInputBytes = Math.Min(actualBytesToCopy, (int)(length - oldPosition));
                        for (int i = 0; i < availableInputBytes; i++)
                            fixed (byte* pN = newData)
                                pN[i] += pInput[oldPosition + i];

                        output.Write(newData, 0, actualBytesToCopy);

                        // adjust counters
                        newPosition += actualBytesToCopy;
                        oldPosition += actualBytesToCopy;
                        bytesToCopy -= actualBytesToCopy;

                        if (bytesToCopy > 0)
                            Debug.Assert(actualBytesToCopy == availableInputBytes);
                    }

                    // sanity-check
                    if (newPosition + control[1] > newSize)
                        throw new InvalidOperationException("Corrupt patch.");

                    // read extra string
                    bytesToCopy = (int)control[1];
                    while (bytesToCopy > 0)
                    {
                        int actualBytesToCopy = Math.Min(bytesToCopy, BUFFER_SIZE);

                        extraStream.Read(newData, 0, actualBytesToCopy);
                        output.Write(newData, 0, actualBytesToCopy);

                        newPosition += actualBytesToCopy;
                        bytesToCopy -= actualBytesToCopy;
                    }

                    // adjust position
                    oldPosition = (int)(oldPosition + control[2]);
                }
            }
        }
#endif

        private static unsafe long ReadInt64(byte[] buf, int offset)
        {
            long y;

            fixed (byte* pb = &buf[offset])
            {
                y = pb[7] & 0x7F;
                for (int i = 6; i >= 0; i--)
                {
                    y = y << 8;
                    y += pb[i];
                }

                y = (pb[7] & 0x80) != 0 ? -y : y;
            }

            return y;
        }
    }
}
