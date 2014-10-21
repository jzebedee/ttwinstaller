using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Checksums;

namespace TaleOfTwoWastelands
{
    sealed class FinalizedTextWriter : TextWriter
    {
        private readonly Crc32 _checksum = new Crc32();
        private readonly int _checksumSize;

        private readonly FileStream _stream;

        public FinalizedTextWriter(string path)
        {
            _stream = File.OpenWrite(path);
            _checksumSize = Encoding.GetByteCount(Checksum);
        }

        private string Checksum
        {
            get { return _checksum.Value.ToString("X8"); }
        }

        private byte[] UpdateChecksum(char[] s)
        {
            var messageBytes = Encoding.GetBytes(s);
            _checksum.Update(messageBytes);

            return messageBytes;
        }

        private bool _writingChecksum;
        public override void Write(char[] buffer, int index, int count)
        {
            lock (_checksum)
            {
                if (_writingChecksum)
                {
                    var bufBytes = Encoding.GetBytes(buffer);
                    _stream.Write(bufBytes, 0, bufBytes.Length);
                    return;
                }

                if (_stream.Position >= _checksumSize)
                    _stream.Seek(-_checksumSize, SeekOrigin.Current);

                var msgBytes = UpdateChecksum(buffer);
                _stream.Write(msgBytes, 0, msgBytes.Length);

                try
                {
                    _writingChecksum = true;
                    Write(Checksum);
                }
                finally
                {
                    _writingChecksum = false;
                }
            }
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
