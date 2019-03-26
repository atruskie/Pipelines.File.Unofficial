using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Nerdbank.Streams;
using TestFixtures;
using static System.IO.File;

namespace Pipelines.File.Unofficial.Benchmarks
{
    [MemoryDiagnoser]
    [RPlotExporter]
    [CsvExporter()]
    public class ReadingBenchmarks
    {
        //Asynchronous = 1073741824, // 0x40000000
        //SequentialScan = 134217728, // 0x08000000
        private const int AsyncRead = 0x48000000;

        public ReadingBenchmarks()
        {
        }


        [Params(
            //1024,
            2048,
            4096,
            //8192,
            16384,
            //32768,
            65536, // NT Async read calls are only actually async after this buffer size
            //131072,
            262144
            //524288
            )]
        public int BufferSize { get; set; }

        [Params("Async|Sequential", "Sequential")]
        public string FileOptionsString { get; set; }

        // this is some bug with bencmarkdotnet's templater that requires this hack or else we get a CS1513
        public FileOptions FileOptions => FileOptionsString == "Sequential"
            ? FileOptions.SequentialScan
            : FileOptions.SequentialScan | FileOptions.Asynchronous;

        // public field
        [ParamsSource(nameof(FilesToTest))]
        public FriendlyPath File { get; set; }

        // public property
        public IEnumerable<FriendlyPath> FilesToTest => new[] { Fixtures.IncrementingInt64_4, Fixtures.IncrementingInt64_128}.Select(x => new FriendlyPath(x));

        [Benchmark(Baseline = true)]
        public ulong FileStreamSync()
        {
            ulong last = 0;

            using (var fileStream = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize, FileOptions))
            {
                var buffer = new byte[BufferSize];
                int bytesRead;
                do
                {
                    bytesRead = fileStream.Read(buffer, 0, BufferSize);
                    ParseLastNumber(buffer.AsSpan(), ref last);
                } while (bytesRead != 0);

                return last;
            }
        }

        //[Benchmark()]
        public ulong FileStreamSyncMemoryPool()
        {
            ulong last = 0;

            using (var fileStream = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize, FileOptions))
            {
                var memoryOwner = MemoryPool<byte>.Shared.Rent(BufferSize);
                var buffer = memoryOwner.Memory.Span;
                int bytesRead;
                do
                {
                    bytesRead = fileStream.Read(buffer);
                    ParseLastNumber(buffer, ref last);
                } while (bytesRead != 0);

                return last;
            }
        }

        [Benchmark]
        public async Task<ulong> FileStreamAsync()
        {
            ulong last = 0;

            var bufferSize = BufferSize == 1 ? 4096 : BufferSize;

            using (var fileStream = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions))
            {
                var buffer = new byte[bufferSize];

                int bytesRead;
                do
                {
                    bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSize);
                    ParseLastNumber(buffer.AsSpan(), ref last);

                } while (bytesRead != 0);

                return last;
            }
        }

        //[Benchmark]
        public async Task<ulong> FileStreamAsyncMemoryPool()
        {
            ulong last = 0;

            using (var fileStream = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize, FileOptions))
            {
                var memoryOwner = MemoryPool<byte>.Shared.Rent(BufferSize);
                var buffer = memoryOwner.Memory;
                int bytesRead;
                do
                {
                    bytesRead = await fileStream.ReadAsync(buffer);
                    ParseLastNumber(buffer.Span, ref last);

                } while (bytesRead != 0);

                return last;
            }
        }

        [Benchmark()]
        public async Task<ulong> PipelineNative()
        {
            ulong last = 0;

            var filePipeline = FileReader.ReadFile(File, BufferSize);
            ReadResult result = default;
            while(!result.IsCompleted)
            {
                result = await filePipeline.ReadAsync();
                ParseLastNumber(result.Buffer, ref last);
                filePipeline.AdvanceTo(result.Buffer.End);
            } 
           
            return last;
        }


        [Benchmark()]
        public async Task<ulong> PipelineAdapter()
        {
            ulong last = 0;

            using (var reader = FileStreamReader.ReadFile(File, BufferSize, FileOptions))
            {
                var filePipeline = reader.Reader;
                ReadResult result = default;
                while (!result.IsCompleted)
                {
                    result = await filePipeline.ReadAsync();
                    ParseLastNumber(result.Buffer, ref last);
                    filePipeline.AdvanceTo(result.Buffer.End);
                }

                return last;
            }
        }

        [Benchmark()]
        public async Task<ulong> PipelineAdapter2()
        {
            ulong last = 0;

            using (var reader = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize, FileOptions))
            {
                var filePipeline = Sockets.Unofficial.StreamConnection.GetReader(reader, new PipeOptions(minimumSegmentSize: BufferSize));
                ReadResult result = default;
                while (!result.IsCompleted)
                {
                    result = await filePipeline.ReadAsync();
                    ParseLastNumber(result.Buffer, ref last);
                    filePipeline.AdvanceTo(result.Buffer.End);
                }

                return last;
            }
        }

        [Benchmark()]
        public async Task<ulong> PipelineAdapter3()
        {
            ulong last = 0;
            using (var reader = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize, FileOptions))
            {
                var filePipeline = reader.UsePipeReader(sizeHint: BufferSize);
                ReadResult result = default;
                while (!result.IsCompleted)
                {
                    result = await filePipeline.ReadAsync();
                    ParseLastNumber(result.Buffer, ref last);
                    filePipeline.AdvanceTo(result.Buffer.End);
                }

                return last;
            }
        }

        private void ParseLastNumber(ReadOnlySpan<byte> packet, ref ulong value)
        {
            if (packet.Length < 8)
            {
                return;
            }
            // assumes packets are 8 byte aligned...
            // dodgy but good enough for tests
            var last8Bytes = packet.Slice(packet.Length - 8, 8);
            value = BinaryPrimitives
                .ReadUInt64LittleEndian(last8Bytes);
        }

        private void ParseLastNumber(ReadOnlySequence<byte> packet, ref ulong value)
        {
            if (packet.Length < 8)
            {
                return;
            }
            // assumes packets are 8 byte aligned...
            // dodgy but good enough for tests
            var last8Bytes = packet.Slice(packet.Length - 8, 8);
            value = BinaryPrimitives
                .ReadUInt64LittleEndian(last8Bytes.First.Span);
        }
    }
}
