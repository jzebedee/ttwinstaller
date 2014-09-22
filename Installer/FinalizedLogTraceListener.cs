using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ICSharpCode.SharpZipLib.Checksums;
using System.IO;

namespace TaleOfTwoWastelands
{
    class FinalizedLogTraceListener : TextWriterTraceListener
    {
        readonly Crc32 _checksum = new Crc32();
        readonly Stream _logStream;

        public FinalizedLogTraceListener(string path)
            : this(File.OpenWrite(path))
        {
        }
        FinalizedLogTraceListener(Stream stream)
            : base(stream)
        {
            _logStream = stream;
            //TraceOutputOptions |= TraceOptions.DateTime | TraceOptions.Callstack;
        }

        private void UpdateChecksum(string s)
        {
            var messageBytes = Writer.Encoding.GetBytes(s);
            _checksum.Update(messageBytes);
        }

        private void WriteChecksum()
        {
            base.Write(_checksum.Value.ToString("X16"));
        }

        private void GoBack()
        {
            if (_logStream.Position > 16)
                _logStream.Seek(-16, SeekOrigin.Current);
        }

        private string Timestamp(string msg)
        {
            return string.Format("[{0:u}] {1}", DateTime.UtcNow, msg);
        }

        public override bool IsThreadSafe
        {
            get
            {
                return false;
            }
        }

        public override void WriteLine(string message)
        {
            message = Timestamp(message);
            UpdateChecksum(message);
            GoBack();
            base.WriteLine(message);
            WriteChecksum();
        }
    }
}
