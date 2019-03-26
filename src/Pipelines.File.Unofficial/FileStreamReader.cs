using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pipelines.File.Unofficial
{
    public class FileStreamReader : IDisposable

    {
        private readonly string _path;
        private readonly PipeWriter _writer;
        private readonly int _bufferSize;
        private readonly FileStream _file;
        private readonly Pipe _pipe;
        private readonly CancellationTokenSource _cancellationToken;

        internal FileStreamReader(string path, int bufferSize = 4096, FileOptions fileOptions = FileOptions.Asynchronous)
        {
            _path = path;
            _pipe = new Pipe();
            _writer = _pipe.Writer;
            _bufferSize = bufferSize;
            _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, fileOptions);
            _cancellationToken = new CancellationTokenSource();

#pragma warning disable 4014 // Purposely firing and forgetting here
            ReadAsync(this);
#pragma warning restore 4014
        }

        public static FileStreamReader ReadFile(string path, int bufferSize = 4069, FileOptions fileOptions = FileOptions.Asynchronous)
        {
            var file = new FileStreamReader(path, bufferSize, fileOptions);
            return file;
        }

        public void Dispose()
        {
            _cancellationToken.Cancel();
            _file?.Dispose();
        }

        public PipeReader Reader => _pipe.Reader;

        private static async Task ReadAsync(FileStreamReader state)
        {
            if (state._cancellationToken.IsCancellationRequested)
            {
                state._writer.Complete();
            }

            Read:
            var buffer = state._writer.GetMemory(state._bufferSize);
            var numBytes = await state._file.ReadAsync(buffer, state._cancellationToken.Token);

            state._writer.Advance(numBytes);

            var awaitable = state._writer.FlushAsync(state._cancellationToken.Token);

            if (numBytes == 0)
            {
                state._writer.Complete();

                // The operation can be disposed when there's nothing more to produce
                state.Dispose();
            }
            else if (awaitable.IsCompleted)
            {
                // No back pressure being applied to continue reading
                goto Read;
            }
            else
            {
                var flushResult = await awaitable;
                if (!flushResult.IsCompleted)
                {
                    goto Read;
                }
                else
                {
                    state._writer.Complete();

                    state.Dispose();
                }
            }
        }
    }
}
