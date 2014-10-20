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
using System.IO;
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

        private const int LZMA_DICTSIZE_ULTRA = 1024 * 1024 * 64; //64MiB, 7z 'Ultra'

        private static void SetCompressionLevel()
        {
            SevenZipCompressor.LzmaDictionarySize = LZMA_DICTSIZE_ULTRA;
        }

        public static Stream GetEncodingStream(Stream stream, long signature, bool output)
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
                    //TOFIX: this does not guarantee wrapping
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
            Stream controlStream, diffStream, extraStream;
            var newSize = CreatePatchStreams(pPatch, patchLength, out controlStream, out diffStream, out extraStream);

            // prepare to read three parts of the patch in parallel
            ApplyInternal(newSize, new UnmanagedMemoryStream(pInput, length), controlStream, diffStream, extraStream, output);
        }

        public static unsafe void Apply(Stream input, byte* pPatch, long patchLength, Stream output)
        {
            Stream controlStream, diffStream, extraStream;
            var newSize = CreatePatchStreams(pPatch, patchLength, out controlStream, out diffStream, out extraStream);

            // prepare to read three parts of the patch in parallel
            ApplyInternal(newSize, input, controlStream, diffStream, extraStream, output);
        }

        static unsafe long CreatePatchStreams(byte* pPatch, long patchLength, out Stream ctrl, out Stream diff, out Stream extra)
        {
            Func<long, long, Stream> openPatchStream = (u_offset, u_length) =>
                new UnmanagedMemoryStream(pPatch + u_offset, u_length > 0 ? u_length : patchLength - u_offset);

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

                fixed (byte* pHead = header)
                {
                    // check for appropriate magic
                    signature = ReadInt64(pHead);

                    // read lengths from header
                    controlLength = ReadInt64(pHead + 8);
                    diffLength = ReadInt64(pHead + 16);
                    newSize = ReadInt64(pHead + 24);
                }

                if (controlLength < 0 || diffLength < 0 || newSize < 0)
                    throw new InvalidOperationException("Corrupt patch.");
            }

            // prepare to read three parts of the patch in parallel
            Stream
                compressedControlStream = openPatchStream(HEADER_SIZE, controlLength),
                compressedDiffStream = openPatchStream(HEADER_SIZE + controlLength, diffLength),
                compressedExtraStream = openPatchStream(HEADER_SIZE + controlLength + diffLength, -1);

            // decompress each part (to read it)
            ctrl = GetEncodingStream(compressedControlStream, signature, false);
            diff = GetEncodingStream(compressedDiffStream, signature, false);
            extra = GetEncodingStream(compressedExtraStream, signature, false);

            return newSize;
        }

        static void ApplyInternal(long newSize, Stream input, Stream ctrl, Stream diff, Stream extra, Stream output)
        {
            long addSize, copySize, seekAmount;

            using (ctrl)
            using (diff)
            using (extra)
            using (BinaryReader
                diffReader = new BinaryReader(diff),
                extraReader = new BinaryReader(extra))
                while (output.Position < newSize)
                {
                    //read control data
                    //set of triples (x,y,z) meaning
                    // add x bytes from oldfile to x bytes from the diff block;
                    // copy y bytes from the extra block;
                    // seek forwards in oldfile by z bytes;
                    addSize = ReadInt64(ctrl);
                    copySize = ReadInt64(ctrl);
                    seekAmount = ReadInt64(ctrl);

                    // sanity-check
                    if (output.Position + addSize > newSize)
                        throw new InvalidOperationException("Corrupt patch.");

                    {
                        // read diff string
                        var newData = diffReader.ReadBytes((int)addSize);

                        // add old data to diff string
                        var availableInputBytes = (int)Math.Min(addSize, input.Length - input.Position);
                        for (int i = 0; i < availableInputBytes; i++)
                            newData[i] += (byte)input.ReadByte();

                        output.Write(newData, 0, (int)addSize);
                        //input.Seek(addSize, SeekOrigin.Current);
                    }

                    // sanity-check
                    if (output.Position + copySize > newSize)
                        throw new InvalidOperationException("Corrupt patch.");

                    // read extra string
                    {
                        var newData = extraReader.ReadBytes((int)copySize);
                        output.Write(newData, 0, (int)copySize);
                    }

                    // adjust position
                    input.Seek(seekAmount, SeekOrigin.Current);
                }
        }

        public static unsafe long ReadInt64(byte* pb)
        {
            long y = pb[7] & 0x7F;
            y <<= 8; y += pb[6];
            y <<= 8; y += pb[5];
            y <<= 8; y += pb[4];
            y <<= 8; y += pb[3];
            y <<= 8; y += pb[2];
            y <<= 8; y += pb[1];
            y <<= 8; y += pb[0];

            return (pb[7] & 0x80) != 0 ? -y : y;
        }

        public static long ReadInt64(Stream ps)
        {
            var buf = new byte[sizeof(long)];
            if (ps.Read(buf, 0, sizeof(long)) != sizeof(long))
                throw new InvalidOperationException("Could not read long from stream");

            unsafe
            {
                fixed (byte* pb = buf)
                    return ReadInt64(pb);
            }
        }
    }
}
