using System;
using System.Collections.Generic;
using System.Text;
using Pipelines.File.Unofficial.Benchmarks;
using TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Pipelines.File.Unofficial.Tests.Benchmarks
{
    public class ReadingBenchmarkTests
    {
        private const ulong Last = 524287L;

        private readonly ITestOutputHelper _output;
        private readonly ReadingBenchmarks _readingBenchmarks;

        public ReadingBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
            _readingBenchmarks = new ReadingBenchmarks()
            {
                BufferSize = 4096,
                File = new FriendlyPath(Fixtures.IncrementingInt64_4)
            };
        }

        [Fact()]
        public void TestFileStreamSync()
        {
            var result = _readingBenchmarks.FileStreamSync();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }

        [Fact()]
        public void TestFileStreamSyncMemoryPool()
        {
            var result = _readingBenchmarks.FileStreamSyncMemoryPool();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }

        [Fact]
        public async void TestAsyncRead()
        {
            var result = await _readingBenchmarks.FileStreamAsync();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }

        [Fact]
        public async void TestAsyncReadMemoryPool()
        {
            var result = await _readingBenchmarks.FileStreamAsyncMemoryPool();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }

        [Fact]
        public async void TestPipelineNative()
        {
            var result = await _readingBenchmarks.PipelineNative();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }

        [Fact]
        public async void TestPipelineAdapter()
        {
            var result = await _readingBenchmarks.PipelineAdapter();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }
        [Fact]
        public async void TestPipelineAdapter2()
        {
            var result = await _readingBenchmarks.PipelineAdapter2();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }
        [Fact]
        public async void TestPipelineAdapter3()
        {
            var result = await _readingBenchmarks.PipelineAdapter3();

            
            Assert.Equal(Last, result);
            _output.WriteLine(result.ToString());
        }
    }
}
