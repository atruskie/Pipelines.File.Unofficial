# Pipelines.File.Unofficial

This was going to be an _unofficial_ implementation of file based pipeline primitives.

However, as I learnt more about pipelines and the code that exists already I 
luckily stopped to benchmark my ideas. As a result of the benchmarks, detailed
below, I've decided there is no need for dedicated file-based pipelines because
stream adapters are already efficient enough.

Thus, this repo is now benchmarking report rather than a library.

**DISCLAIMER: THIS CODE IS EXPERIMENTAL, I AM NOT AN EXPERT, THERE MAY BE MISTAKES**

## TL;DR:

- The native pipeline file reader I tested was very memory efficient but very slow and not worth developing
- FileStream (or stream) pipeline adapters:
  - **are recommended** if you're concerned about memory usage
  - Are slower than the FileStream sync and async APIs
- All pipeline methods, and async methods are slower than plain old FileStream sync reads
- Use a large buffer size (e.g. `65536` bytes or greater) for best reading performance

## Background

System.IO.Pipelines is a new IO concept introduced in .NET Core 2.1.

Pipelines to do several important things that were traditionally tricky:

- buffer management
- backlog control
- thread managment
- and suport for back pressure

Fore more information see these blog posts:

- [System.IO.Pipelines: High performance IO in .NET](https://blogs.msdn.microsoft.com/dotnet/2018/07/09/system-io-pipelines-high-performance-io-in-net/)
- [Pipelines - a guided tour of the new IO API in .NET, part 1](https://blog.marcgravell.com/2018/07/pipe-dreams-part-1.html)

## Problem

The pipeline APIs were released without any connectors. From Marc Gravell's
[blog post](https://blog.marcgravell.com/2018/07/pipe-dreams-part-2.html):

> Here we need a bit of caveat and disclaimer: the pipelines released in .NET
> Core 2.1 do not include any endpoint implementations. Meaning: the Pipe
> machinery is there, but nothing is shipped inside the box that actually
> connects pipes with any other existing systems - like shipping the abstract
> Stream base-type, but without shipping FileStream, NetworkStream, etc. 

Marc thus introduced [Pipelines.Sockets.Unofficial](https://github.com/mgravell/Pipelines.Sockets.Unofficial)
to use in their .NET Redis library.

Since I needed to process large amounts of file-based I/O I decided to see
if I could create my own file-based pipeline connector. Enter
_Pipelines.File.Unoffcial_.

## Implementations

### Win32 Native by David Fowler

When the pipelines library was being developed, David Fowler created a native
Win32 implemntation of a file `PipeReader`. 

- Initial implementation: https://github.com/dotnet/corefxlab/commit/df543b2c8ef193a9e04f93cd424b91c821aecb99#diff-54667d4e0d8ffdddf979ebbfe5681938
- Last commit: https://github.com/dotnet/corefxlab/commit/3bd0ffdef64b4510b408200cf4281d24b5742abd#diff-54667d4e0d8ffdddf979ebbfe5681938
- Removal: https://github.com/dotnet/corefxlab/pull/2244, https://github.com/dotnet/corefxlab/pull/2244/commits/abba6a7e8faf530e5e2c8216d30ec01c73594e8c

As per the commits above it was an experiment that was abondoned.
It would have been very hard to implement native file access for each
platform.

You can find a modified implementation in [./src/Pipelines.File.Unofficial/FileReader.cs](./src/Pipelines.File.Unofficial/FileReader.cs)

### My own FileStream adapter

I created my own naive implementation of a `FileStream` adapter which
rather than bypassing `FileStream` (as done in David Fowler's implentation)
uses the standard stream API.

You can find the implementation in [./src/Pipelines.File.Unofficial/FileStreamReader.cs](./src/Pipelines.File.Unofficial/FileStreamReader.cs)

### Pre-built stream adapters

Realising I was perilously close to inventing some wheels, I added two previous
(and generic) pipeline-stream adapters to the benchmarks:

- [`Sockets.Unofficial.StreamConnection`](https://github.com/mgravell/Pipelines.Sockets.Unofficial/blob/master/src/Pipelines.Sockets.Unofficial/StreamConnection.cs) by Marc Gravell
- [`Nerdbank.Streams.PipeExtensions`](https://github.com/AArnott/Nerdbank.Streams/blob/c56c016772bce81521ccb79a7130bd4105df9faa/src/Nerdbank.Streams/PipeExtensions.cs) by Andrew Arnott

These implementations are in found in their respective nuget packages.

## Results

All results were compiled with [BenchmarkDotNet](https://benchmarkdotnet.org/).

The benchmarking code can be found in [./tests/Pipelines.File.Unofficial.Benchmarks](./tests/Pipelines.File.Unofficial.Benchmarks)
and the full benchmark report has been [committed](./tests/Pipelines.File.Unofficial.Benchmarks/BenchmarkDotNet.Artifacts).

### Summary

```
BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17763.253 (1809/October2018Update/Redstone5)
Intel Core i7-8550U CPU 1.80GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100-preview-009812
[Host]     : .NET Core 3.0.0-preview-27122-01 (CoreCLR 4.6.27121.03, CoreFX 4.7.18.57103), 64bit RyuJIT
DefaultJob : .NET Core 3.0.0-preview-27122-01 (CoreCLR 4.6.27121.03, CoreFX 4.7.18.57103), 64bit RyuJIT
```

### Methods

  - _FileStreamSync_: a traditional stream only reading of bytes
  - _FileStreamAsync_: using the async methods on _FileStream_
  - _PipelineNative_: This is David Fowl's Win32 Native implementation
  - _PipelineAdapter_: My own minimal FileStream adapter
  - _PipelineAdapter2_: This is the pipeline stream adapter from Marc Gravell's Pipelines.Sockets.Unofficial
  - _PipelineAdapter3_: This is the pipeline stream adapter from Andrew Arnott 's Nerdbank.Streams

## Parameters

  - _File_: one of two files, both containing incrementing little-endian ulongs, 4 MiB and 128 MiB 
  - _BufferSize_: the amount requested to read each iteration, in bytes. The NT API doesn't even do
    asynchronous reads until the buffer is at least 64k


Method                    | BufferSize |           File |           Mean |        Error |       StdDev |         Median | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
------------------------- |----------- |--------------- |---------------:|-------------:|-------------:|---------------:|------:|--------:|------------:|------------:|------------:|--------------------:|
FileStreamSync            |       2048 | _int64_128.bin |   164,135.0 us |  2,372.53 us |  2,219.26 us |   164,142.3 us |  1.00 |    0.00 |           - |           - |           - |             6.22 KB |
FileStreamAsync           |       2048 | _int64_128.bin |   433,457.6 us |  7,153.31 us |  6,691.21 us |   431,952.2 us |  2.64 |    0.06 |   3000.0000 |           - |           - |             2.75 KB |
PipelineNative            |       2048 | _int64_128.bin | 2,201,643.0 us | 49,234.38 us | 65,726.49 us | 2,183,382.1 us | 13.56 |    0.55 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |       2048 | _int64_128.bin |   652,158.5 us | 12,905.17 us | 24,553.43 us |   645,153.3 us |  4.05 |    0.18 |   4000.0000 |           - |           - |             1.64 KB |
PipelineAdapter2          |       2048 | _int64_128.bin |   679,061.5 us | 13,410.05 us | 30,811.81 us |   674,324.1 us |  4.03 |    0.15 |   4000.0000 |           - |           - |             1.33 KB |
PipelineAdapter3          |       2048 | _int64_128.bin |   735,703.0 us | 15,485.69 us | 38,276.79 us |   731,461.5 us |  4.55 |    0.32 |   4000.0000 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |       2048 |   _int64_4.bin |     5,347.7 us |    106.69 us |    222.71 us |     5,354.2 us |  1.00 |    0.00 |           - |           - |           - |             6.22 KB |
FileStreamAsync           |       2048 |   _int64_4.bin |    14,913.0 us |    298.06 us |    840.68 us |    14,623.0 us |  2.79 |    0.20 |     93.7500 |           - |           - |             2.68 KB |
PipelineNative            |       2048 |   _int64_4.bin |    83,768.2 us |  1,666.62 us |  4,331.76 us |    82,416.2 us | 15.91 |    1.22 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |       2048 |   _int64_4.bin |    25,482.1 us |    506.80 us |  1,317.25 us |    25,345.2 us |  4.79 |    0.38 |    125.0000 |           - |           - |             1.64 KB |
PipelineAdapter2          |       2048 |   _int64_4.bin |    25,096.5 us |    500.81 us |  1,099.30 us |    25,166.5 us |  4.71 |    0.31 |    125.0000 |           - |           - |             1.15 KB |
PipelineAdapter3          |       2048 |   _int64_4.bin |    23,911.2 us |    474.29 us |    830.69 us |    23,790.9 us |  4.50 |    0.22 |    125.0000 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |       4096 | _int64_128.bin |   191,432.0 us |  3,793.10 us |  8,325.95 us |   191,037.4 us |  1.00 |    0.00 |           - |           - |           - |              4.2 KB |
FileStreamAsync           |       4096 | _int64_128.bin |   329,237.9 us |  8,443.83 us | 23,256.76 us |   324,230.2 us |  1.71 |    0.16 |   1000.0000 |           - |           - |             4.68 KB |
PipelineNative            |       4096 | _int64_128.bin | 1,502,253.4 us | 30,000.37 us | 42,056.33 us | 1,508,425.3 us |  7.89 |    0.54 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |       4096 | _int64_128.bin |   597,720.2 us | 23,434.49 us | 69,097.12 us |   578,717.8 us |  3.35 |    0.36 |   2000.0000 |           - |           - |             1.64 KB |
PipelineAdapter2          |       4096 | _int64_128.bin |   533,298.0 us | 10,591.33 us | 23,248.22 us |   531,606.6 us |  2.79 |    0.14 |   2000.0000 |           - |           - |             1.33 KB |
PipelineAdapter3          |       4096 | _int64_128.bin |   564,661.4 us | 24,169.80 us | 68,565.69 us |   544,382.7 us |  2.84 |    0.23 |   2000.0000 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |       4096 |   _int64_4.bin |     5,559.9 us |    114.15 us |    310.57 us |     5,489.5 us |  1.00 |    0.00 |           - |           - |           - |              4.2 KB |
FileStreamAsync           |       4096 |   _int64_4.bin |    10,293.5 us |    388.02 us |  1,113.29 us |    10,253.9 us |  1.86 |    0.25 |     46.8750 |           - |           - |             4.68 KB |
PipelineNative            |       4096 |   _int64_4.bin |    40,928.2 us |    804.54 us |  1,430.06 us |    40,635.6 us |  7.38 |    0.51 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |       4096 |   _int64_4.bin |    15,073.2 us |    304.29 us |    446.02 us |    15,034.4 us |  2.76 |    0.18 |     62.5000 |           - |           - |             1.64 KB |
PipelineAdapter2          |       4096 |   _int64_4.bin |    15,039.8 us |    213.13 us |    188.94 us |    15,015.9 us |  2.66 |    0.14 |     62.5000 |           - |           - |             1.14 KB |
PipelineAdapter3          |       4096 |   _int64_4.bin |    15,407.1 us |    312.30 us |    601.70 us |    15,302.7 us |  2.76 |    0.19 |     62.5000 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |      16384 | _int64_128.bin |    64,319.0 us |    783.40 us |    694.46 us |    64,266.0 us |  1.00 |    0.00 |           - |           - |           - |             16.2 KB |
FileStreamAsync           |      16384 | _int64_128.bin |   115,156.3 us |  2,296.75 us |  5,277.17 us |   113,630.9 us |  1.85 |    0.10 |           - |           - |           - |            16.68 KB |
PipelineNative            |      16384 | _int64_128.bin |   368,722.0 us |  7,226.93 us |  6,760.07 us |   368,904.8 us |  5.73 |    0.13 |           - |           - |           - |             1.38 KB |
PipelineAdapter           |      16384 | _int64_128.bin |   203,809.3 us |  4,063.27 us | 11,054.43 us |   203,944.0 us |  3.14 |    0.17 |           - |           - |           - |             1.64 KB |
PipelineAdapter2          |      16384 | _int64_128.bin |   205,713.8 us |  3,997.32 us |  6,223.34 us |   206,091.0 us |  3.20 |    0.11 |           - |           - |           - |             1.33 KB |
PipelineAdapter3          |      16384 | _int64_128.bin |   207,712.0 us |  4,091.29 us |  8,629.92 us |   208,080.8 us |  3.18 |    0.19 |           - |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |      16384 |   _int64_4.bin |     1,888.1 us |     24.13 us |     22.57 us |     1,888.7 us |  1.00 |    0.00 |      3.9063 |           - |           - |             16.2 KB |
FileStreamAsync           |      16384 |   _int64_4.bin |     3,356.4 us |     68.56 us |     64.13 us |     3,339.4 us |  1.78 |    0.05 |     15.6250 |           - |           - |            16.68 KB |
PipelineNative            |      16384 |   _int64_4.bin |    11,092.3 us |    210.79 us |    225.54 us |    11,105.1 us |  5.88 |    0.14 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |      16384 |   _int64_4.bin |     5,688.6 us |    108.80 us |    101.77 us |     5,697.1 us |  3.01 |    0.06 |     15.6250 |           - |           - |             1.64 KB |
PipelineAdapter2          |      16384 |   _int64_4.bin |     5,641.9 us |     78.42 us |     73.36 us |     5,641.6 us |  2.99 |    0.04 |     15.6250 |           - |           - |             1.13 KB |
PipelineAdapter3          |      16384 |   _int64_4.bin |     5,398.6 us |     60.53 us |     56.62 us |     5,420.6 us |  2.86 |    0.05 |     15.6250 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |      65536 | _int64_128.bin |    36,635.6 us |    727.88 us |  1,349.18 us |    36,221.6 us |  1.00 |    0.00 |           - |           - |           - |             64.2 KB |
FileStreamAsync           |      65536 | _int64_128.bin |    56,314.2 us |    566.92 us |    530.29 us |    56,083.9 us |  1.55 |    0.06 |    100.0000 |           - |           - |            64.68 KB |
PipelineNative            |      65536 | _int64_128.bin |   131,199.4 us |  1,936.36 us |  1,811.27 us |   131,181.4 us |  3.62 |    0.13 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |      65536 | _int64_128.bin |    99,425.7 us |  1,354.97 us |  1,267.44 us |    99,852.0 us |  2.75 |    0.13 |           - |           - |           - |             1.64 KB |
PipelineAdapter2          |      65536 | _int64_128.bin |    97,561.3 us |  1,907.65 us |  2,969.98 us |    96,684.9 us |  2.66 |    0.15 |           - |           - |           - |             1.16 KB |
PipelineAdapter3          |      65536 | _int64_128.bin |    97,185.9 us |  1,861.80 us |  2,354.58 us |    96,711.1 us |  2.64 |    0.15 |           - |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |      65536 |   _int64_4.bin |       944.9 us |     16.87 us |     14.95 us |       945.1 us |  1.00 |    0.00 |     13.6719 |           - |           - |             64.2 KB |
FileStreamAsync           |      65536 |   _int64_4.bin |     1,656.4 us |     33.05 us |     38.06 us |     1,653.7 us |  1.75 |    0.05 |     19.5313 |           - |           - |            64.68 KB |
PipelineNative            |      65536 |   _int64_4.bin |     3,970.0 us |     61.79 us |     54.78 us |     3,959.5 us |  4.20 |    0.09 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |      65536 |   _int64_4.bin |     2,675.3 us |     48.86 us |     45.70 us |     2,667.0 us |  2.83 |    0.07 |      3.9063 |           - |           - |             1.64 KB |
PipelineAdapter2          |      65536 |   _int64_4.bin |     2,816.3 us |     55.41 us |     51.83 us |     2,796.1 us |  2.98 |    0.06 |      3.9063 |           - |           - |             1.13 KB |
PipelineAdapter3          |      65536 |   _int64_4.bin |     2,789.9 us |     43.53 us |     40.72 us |     2,800.8 us |  2.95 |    0.05 |      3.9063 |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |     262144 | _int64_128.bin |    29,470.8 us |    299.95 us |    265.90 us |    29,489.8 us |  1.00 |    0.00 |     62.5000 |     62.5000 |     62.5000 |            256.2 KB |
FileStreamAsync           |     262144 | _int64_128.bin |    39,609.6 us |    616.06 us |    546.12 us |    39,692.2 us |  1.34 |    0.02 |     76.9231 |     76.9231 |     76.9231 |           256.68 KB |
PipelineNative            |     262144 | _int64_128.bin |    54,860.2 us |  1,072.44 us |  1,317.05 us |    54,701.7 us |  1.87 |    0.06 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |     262144 | _int64_128.bin |    60,198.2 us |    740.67 us |    692.82 us |    60,035.4 us |  2.04 |    0.03 |           - |           - |           - |             1.64 KB |
PipelineAdapter2          |     262144 | _int64_128.bin |    59,829.9 us |    978.17 us |    914.98 us |    59,671.2 us |  2.03 |    0.03 |           - |           - |           - |             1.15 KB |
PipelineAdapter3          |     262144 | _int64_128.bin |    59,663.9 us |  1,135.12 us |  1,307.21 us |    59,394.8 us |  2.03 |    0.06 |           - |           - |           - |             1.22 KB |
-                         |            |                |                |              |              |                |       |         |             |             |             |                     |
FileStreamSync            |     262144 |   _int64_4.bin |       899.3 us |     15.59 us |     13.82 us |       898.8 us |  1.00 |    0.00 |     82.0313 |     82.0313 |     82.0313 |            256.2 KB |
FileStreamAsync           |     262144 |   _int64_4.bin |     1,306.0 us |     21.89 us |     19.40 us |     1,309.2 us |  1.45 |    0.03 |     82.0313 |     82.0313 |     82.0313 |           256.68 KB |
PipelineNative            |     262144 |   _int64_4.bin |     1,608.8 us |     31.73 us |     61.89 us |     1,594.7 us |  1.85 |    0.06 |           - |           - |           - |             1.27 KB |
PipelineAdapter           |     262144 |   _int64_4.bin |     1,672.0 us |     20.91 us |     17.46 us |     1,674.8 us |  1.86 |    0.04 |           - |           - |           - |             1.64 KB |
PipelineAdapter2          |     262144 |   _int64_4.bin |     1,741.0 us |     31.78 us |     26.54 us |     1,750.9 us |  1.93 |    0.03 |           - |           - |           - |             1.13 KB |
PipelineAdapter3          |     262144 |   _int64_4.bin |     1,737.9 us |     34.40 us |     38.23 us |     1,743.1 us |  1.93 |    0.06 |           - |           - |           - |             1.22 KB |


## Observations

- FileStream's sync methods are _always_ fastest
- FileStream's async methods are always faster than all pipeline methods
- BufferSize has a large affect on performance, decreasing read times as buffer size increases
- The FileStream async method is consistently slower than its sync counterpart
  - Though the ratio shrinks from `2.64` times slower at small buffers sizes to only `1.34` times at large buffers
- For the native pipeline wrapper
  - The native API pipeline performs very poorly (up to `15` times slower than sync) at small buffer sizes
  - At larger buffer sizes, the native API is comparable to FileStream's async method
  - At larger buffer sizes, the native API is as fast as the FileStream adapters
- Pipeline adapters
  - The adapters also perform _much_ better thant the native pipeline at lower buffer sizes
  - All of the pipeline adapters have fairly consistent performance compared to each other
  - They also is they experience less variance across experiemnts
    - the slowest ratio is `~4.7` times sync and `1.93` times sync (`2.77` ratio variance over tests)
    - this is much less variance than experienced by the native pipeline (`14.06` ratio variance over tests)
  - Until BufferSize reaches `262144` all pipeline adapters  are faster than the native pipeline
- File size differences:
  - the smaller file regularly is almost always slower than the larger file
- Memory:
  - The traditional file stream methods always allocateat least as much memory as the buffer
  - All pipeline methods allocate less memory than non-pipeline methods
  - The native pipeline never requires garbage collections for Gen 0, 1, or 2!
  - Only the traditional file stream methods have objects that survive to Gen 1 and 2, and only at buffer sizes above `65536`
  - The pipeline adapters do require garbage collection for Gen 0 objects:
    - At the smallest buffer size (`2048`), `4000` collections occurred!
    - But after buffer size `16384`, the number of collections are equal or less than other methods


  ---

  Anthony Truskinger (@atruskie)
