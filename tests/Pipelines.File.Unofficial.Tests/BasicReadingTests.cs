using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using TestFixtures;

namespace Pipelines.File.Unofficial.Tests
{
    public class BasicReadingTests
    {

        private readonly ITestOutputHelper output;

        public BasicReadingTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async void TestWin32Native()
        {
            string path = Fixtures.IncrementingUint32_4;

            var pipe = FileReader.ReadFile(path);

            int counter = -1;
            long sum = 0;

            var readResults = new List<(bool Canceled, bool Completed)>();
            ReadResult result;
            do
            {
                result = await pipe.ReadAsync();
                readResults.Add((result.IsCanceled, result.IsCompleted));

                this.output.WriteLine("Buffer length: " + result.Buffer.Length);

                Check(result.Buffer);
                pipe.AdvanceTo(result.Buffer.End);
            } while (!result.IsCompleted);

            Assert.Equal(1_048_575, counter);
            Assert.Equal(549_755_289_600L, sum);

            // check read results. All should be false except for the last completed
            Assert.True(readResults.SkipLast(1).All(x => x.Completed == false && x.Canceled == false));
            var last = readResults.Last();
            Assert.True(last.Completed == true && last.Canceled == false);

            void Check(ReadOnlySequence<byte> buffer)
            {
                var reader = new SequenceReader<byte>(buffer);
                while (!reader.End)
                {
                    counter++;
                    Assert.True(reader.TryReadLittleEndian(out int value));
                    Assert.Equal(counter, value);
                    sum += value;
                }

                this.output.WriteLine($"{counter} values checked");
            }
        }

        [Fact]
        public async void TestCustomFileStreamAdapter()
        {
            string path = Fixtures.IncrementingUint32_4;

            using (var file = FileStreamReader.ReadFile(path))
            {
                var pipe = file.Reader;

                int counter = -1;
                long sum = 0;

                var readResults = new List<(bool Canceled, bool Completed)>();
                ReadResult result;
                do
                {
                    result = await pipe.ReadAsync();
                    readResults.Add((result.IsCanceled, result.IsCompleted));

                    this.output.WriteLine("Buffer length: " + result.Buffer.Length);

                    Check(result.Buffer);
                    pipe.AdvanceTo(result.Buffer.End);
                } while (!result.IsCompleted);

                Assert.Equal(1_048_575, counter);
                Assert.Equal(549_755_289_600L, sum);

                // check read results. All should be false except for the last completed
                Assert.True(readResults.SkipLast(1).All(x => x.Completed == false && x.Canceled == false));
                var last = readResults.Last();
                Assert.True(last.Completed == true && last.Canceled == false);


                void Check(ReadOnlySequence<byte> buffer)
                {
                    var reader = new SequenceReader<byte>(buffer);
                    while (!reader.End)
                    {
                        counter++;
                        Assert.True(reader.TryReadLittleEndian(out int value));
                        Assert.Equal(counter, value);
                        sum += value;
                    }

                    this.output.WriteLine($"{counter} values checked");
                }
            }
        }
    }
}
