// Reproduced from https://github.com/dotnet/corefxlab/blob/3bd0ffdef64b4510b408200cf4281d24b5742abd/src/System.IO.Pipelines.File/FileReader.cs
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Pipelines.File.Unofficial
{
    public class FileReader : IDisposable
    {
        private readonly PipeWriter _writer;
        private readonly int _bufferSize;
        private ReadOperation _readOperation;

        internal FileReader(PipeWriter writer, int bufferSize)
        {
            _writer = writer;
            _bufferSize = bufferSize;
        }

        public static PipeReader ReadFile(string path, int bufferSize = 4069)
        {
            var pipe = new Pipe();
            var file = new FileReader(pipe.Writer, bufferSize);
            file.OpenReadFile(path);
            
            return pipe.Reader;
        }

        // Win32 file impl
        // TODO: Other platforms
        private void OpenReadFile(string path)
        {
            var fileHandle = CreateFile(path, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, EFileAttributes.Overlapped, IntPtr.Zero);

            _readOperation = new ReadOperation(fileHandle, _bufferSize, _writer);

            
            Task.Factory.StartNew(state =>
            {
                ((ReadOperation)state).Read();
            },
            _readOperation);
        }

        private static unsafe void IOCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            var state = ThreadPoolBoundHandle.GetNativeOverlappedState(pOverlapped);
            var operation = (ReadOperation)state;

            operation.ThreadPoolBoundHandle.FreeNativeOverlapped(operation.Overlapped);

            operation.Offset += (int)numBytes;
            
            operation.Writer.Advance((int)numBytes);
            var awaitable = operation.Writer.FlushAsync();

            if (numBytes == 0)
            {
                operation.Writer.Complete();

                // The operation can be disposed when there's nothing more to produce
                operation.Dispose();
            }
            else if (awaitable.IsCompleted)
            {
                // No back pressure being applied to continue reading
                operation.Read();
            }
            else
            {
                var ignore = Continue(awaitable, operation);
            }
        }

        private static async Task Continue(ValueTask<FlushResult> awaitable, ReadOperation operation)
        {
            // Keep reading once we get the completion
            var flushResult = await awaitable;
            if (!flushResult.IsCompleted)
            {
                operation.Read();
            }
            else
            {
                operation.Writer.Complete();

                operation.Dispose();
            }
        }

        private class ReadOperation : IDisposable
        {
            private readonly int _bufferSize;

            public unsafe ReadOperation(SafeFileHandle fileHandle, int bufferSize, PipeWriter writer)
            {
                _bufferSize = bufferSize;
                FileHandle = fileHandle;
                PreAllocatedOverlapped = new PreAllocatedOverlapped(IOCallback, this, null); ;
                ThreadPoolBoundHandle = ThreadPoolBoundHandle.BindHandle(fileHandle);
                Writer = writer;
            }

            public SafeFileHandle FileHandle { get; }

            public PreAllocatedOverlapped PreAllocatedOverlapped { get; }

            public ThreadPoolBoundHandle ThreadPoolBoundHandle { get; }

            public unsafe NativeOverlapped* Overlapped { get; private set; }

            public PipeWriter Writer { get; }

            public int Offset { get; set; }

            public unsafe void Read()
            {
                
                var buffer = Writer.GetMemory(_bufferSize);
                var count = buffer.Length;
                int r;
                var overlapped = ThreadPoolBoundHandle.AllocateNativeOverlapped(PreAllocatedOverlapped);
                overlapped->OffsetLow = Offset;

                Overlapped = overlapped;

                fixed (byte* source = &MemoryMarshal.GetReference(buffer.Span))
                {
                    r = ReadFile(FileHandle, (IntPtr) source, count, IntPtr.Zero, overlapped);
                }

                // TODO: Error handling
                if (r == 0)
                {

                }
                // 997
                // Note The GetLastError code ERROR_IO_PENDING is not a failure; it designates the read operation
                // is pending completion asynchronously. For more information, see Remarks.
                // https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/nf-fileapi-readfile
                    int hr = Marshal.GetLastWin32Error();
                    if (hr != 997)
                    {
                        Writer.Complete(Marshal.GetExceptionForHR(hr));
                    }
                
            }

            public void Dispose()
            {
                FileHandle.Dispose();

                ThreadPoolBoundHandle.Dispose();

                PreAllocatedOverlapped.Dispose();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern unsafe int ReadFile(
            SafeFileHandle hFile,      // handle to file
            IntPtr pBuffer,        // data buffer, should be fixed
            int numberOfBytesToRead,  // number of bytes to read
            IntPtr pNumberOfBytesRead,  // number of bytes read, should be null (IntPtr.Zero) for async operation
            NativeOverlapped* lpOverlapped // should be fixed, if not IntPtr.Zero
        );

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] EFileAttributes flags,
            IntPtr template
        );


        [Flags]
        private enum EFileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        public void Dispose()
        {
            _readOperation.Dispose();
        }
    }
}