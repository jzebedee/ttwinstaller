﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ICSharpCode.SharpZipLib.Checksums;

namespace TaleOfTwoWastelands
{
    class FinalizedLogTraceListener : TextWriterTraceListener
    {
        readonly Crc32 _checksum = new Crc32();

        public FinalizedLogTraceListener(string path)
            : base(path)
        {
        }

        private void UpdateChecksum(string s)
        {
            var messageBytes = Writer.Encoding.GetBytes(s);
            _checksum.Update(messageBytes);
        }

        public override bool IsThreadSafe
        {
            get
            {
                return false;
            }
        }

        public override void Write(string message)
        {
            UpdateChecksum(message);
            base.Write(message);
        }

        public override void WriteLine(string message)
        {
            UpdateChecksum(message);
            base.WriteLine(message);
        }

        public override void Close()
        {
            base.WriteLine(_checksum.Value.ToString("X"));
            base.Close();
        }
    }
}
