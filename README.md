# Pipelines.File.Unofficial

This was going to be an _unofficial_ implementation of file based pipeline primitives.

However, as I learnt more about pipelines and the code that exists already I 
stopped to benchmark my ideas. As a result of the benchmarks, detailed
below, I've decided there is no need for dedicated file-based pipelines because
stream adapters are already efficient enough.

Thus, this repo is now a benchmarking report rather than a library.

**UPDATE** Better code/benchmarks as 
suggested by @feO2x in [#1](https://github.com/atruskie/Pipelines.File.Unofficial/issues/1)
suggest no changes to recommendations.

**DISCLAIMER: THIS CODE IS EXPERIMENTAL, I AM NOT AN EXPERT, THERE MAY BE MISTAKES**

## TL;DR:

(Full results and conclusion at bottom of this document)

- The native pipeline file reader I tested was very memory efficient but also very slow
  - Results only calculated for Windows
  - Native code is tricky to get right - there are lots of gotchas
    - Windows doesn't even do async reads until the buffer gets to 64000 bytes
- When `FileOptions.SequentialScan | FileOptions.Asynchronous` are set performance
    universally gets worse (except for the native pipeline for which the option does not apply)
- FileStream (or stream) pipeline adapters (like [Nerdbank.Streams](https://github.com/AArnott/Nerdbank.Streams/blob/c56c016772bce81521ccb79a7130bd4105df9faa/src/Nerdbank.Streams/PipeExtensions.cs)):
  - **are recommended** if you're concerned about memory usage
  - Are slower than the FileStream sync and async APIs
- All pipeline adapters and async methods are slower than plain old FileStream sync reads
  - This is true whether or not `FileOptions.Asynchronous` is set
  - Though if you need non-blocking I/O then straight read performance is arguably not your goal
- Use a large buffer size (e.g. `65536` bytes or greater) for best reading performance

You can read more about file performance for ordinary reading scenarios:
- In @feO2x's [InsightsOnFiles](https://github.com/feO2x/InsightsOnFiles)
- Add at the .NET performance repo https://github.com/dotnet/performance/issues/402

And look out for the stream adapters in .NET <https://github.com/dotnet/corefx/issues/27246>.

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
Win32 implementation of a file `PipeReader`. 

- Initial implementation: https://github.com/dotnet/corefxlab/commit/df543b2c8ef193a9e04f93cd424b91c821aecb99#diff-54667d4e0d8ffdddf979ebbfe5681938
- Last commit: https://github.com/dotnet/corefxlab/commit/3bd0ffdef64b4510b408200cf4281d24b5742abd#diff-54667d4e0d8ffdddf979ebbfe5681938
- Removal: https://github.com/dotnet/corefxlab/pull/2244, https://github.com/dotnet/corefxlab/pull/2244/commits/abba6a7e8faf530e5e2c8216d30ec01c73594e8c

As per the commits above it was an experiment that was abandoned.
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

``` ini
BenchmarkDotNet=v0.11.4, OS=Windows 10.0.17763.379 (1809/October2018Update/Redstone5)
Intel Core i7-8550U CPU 1.80GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100-preview-009812
  [Host]     : .NET Core 3.0.0-preview-27122-01 (CoreCLR 4.6.27121.03, CoreFX 4.7.18.57103), 64bit RyuJIT
  DefaultJob : .NET Core 3.0.0-preview-27122-01 (CoreCLR 4.6.27121.03, CoreFX 4.7.18.57103), 64bit RyuJIT
```

### Methods

  - _FileStreamSync_: traditional _FileStream_ sync reading of bytes
  - _FileStreamAsync_: traditional _FileStream_ async reading of bytes
  - _PipelineNative_: This is David Fowl's Win32 Native file pipeline implementation
  - _PipelineAdapter_: My own minimal FileStream adapter
  - _PipelineAdapter2_: This is the pipeline stream adapter from Marc Gravell's Pipelines.Sockets.Unofficial
  - _PipelineAdapter3_: This is the pipeline stream adapter from Andrew Arnott's Nerdbank.Streams

## Parameters

  - _File_: one of two files, both containing incrementing little-endian ulongs, 4 MiB and 128 MiB, respectively
  - _BufferSize_: the amount requested to read each iteration, in bytes. The NT API doesn't even do
    asynchronous reads until the buffer is at least 64k
  - _FileOptionsString_: 
    - One of two options either
      1. _Async|Sequential_ which maps to `FileOptions.SequentialScan | FileOptions.Asynchronous`
      2. Or _Sequential_ which maps to `FileOptions.SequentialScan`
    - Note: the `FileOptions` parameter has no effect on the PipelineNative method

Benchmarks with issues:
  ReadingBenchmarks.PipelineNative: DefaultJob [BufferSize=262144, FileOptionsString=Sequential, File=_int64_4.bin]


| Method             | BufferSize | FileOptionsString    | File               |               Mean |             Error |             StdDev |             Median |    Ratio |  RatioSD |
|--------------------|------------|----------------------|--------------------|-------------------:|------------------:|-------------------:|-------------------:|---------:|---------:|
| **FileStreamSync** | **2048**   | **Async_Sequential** | **_int64_128.bin** | **2,492,490.4 us** | **49,381.891 us** | **103,078.395 us** | **2,484,851.9 us** | **1.00** | **0.00** |
| FileStreamAsync    | 2048       | Async_Sequential     | _int64_128.bin     |     3,073,318.6 us |     23,842.492 us |      22,302.281 us |     3,070,711.6 us |     1.19 |     0.04 |
| PipelineNative     | 2048       | Async_Sequential     | _int64_128.bin     |     2,048,997.6 us |     40,581.643 us |      37,960.094 us |     2,056,381.8 us |     0.79 |     0.04 |
| PipelineAdapter    | 2048       | Async_Sequential     | _int64_128.bin     |     6,371,385.2 us |    120,317.677 us |     112,545.230 us |     6,335,406.4 us |     2.46 |     0.09 |
| PipelineAdapter2   | 2048       | Async_Sequential     | _int64_128.bin     |     5,921,651.7 us |    110,608.936 us |     118,350.322 us |     5,961,165.5 us |     2.29 |     0.10 |
| PipelineAdapter3   | 2048       | Async_Sequential     | _int64_128.bin     |     5,891,550.8 us |    103,403.848 us |      96,724.023 us |     5,865,670.6 us |     2.27 |     0.06 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **2048**   | **Async_Sequential** | **_int64_4.bin**   |   **118,926.0 us** |  **2,363.099 us** |   **5,616.154 us** |   **119,123.5 us** | **1.00** | **0.00** |
| FileStreamAsync    | 2048       | Async_Sequential     | _int64_4.bin       |       157,997.0 us |      3,150.255 us |       6,363.670 us |       156,766.6 us |     1.33 |     0.08 |
| PipelineNative     | 2048       | Async_Sequential     | _int64_4.bin       |        85,264.7 us |      1,055.834 us |         935.969 us |        85,297.3 us |     0.72 |     0.03 |
| PipelineAdapter    | 2048       | Async_Sequential     | _int64_4.bin       |       192,037.5 us |      3,780.542 us |       5,658.538 us |       191,862.1 us |     1.61 |     0.08 |
| PipelineAdapter2   | 2048       | Async_Sequential     | _int64_4.bin       |       171,697.7 us |      5,379.095 us |       7,540.740 us |       168,766.8 us |     1.44 |     0.09 |
| PipelineAdapter3   | 2048       | Async_Sequential     | _int64_4.bin       |       170,498.4 us |      2,375.639 us |       2,105.942 us |       169,753.7 us |     1.43 |     0.05 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **2048**   | **Sequential**       | **_int64_128.bin** |   **407,573.8 us** |  **8,111.464 us** |  **23,789.519 us** |   **410,795.2 us** | **1.00** | **0.00** |
| FileStreamAsync    | 2048       | Sequential           | _int64_128.bin     |       700,128.1 us |     11,053.114 us |      10,339.090 us |       701,807.1 us |     1.92 |     0.06 |
| PipelineNative     | 2048       | Sequential           | _int64_128.bin     |     2,598,343.7 us |     58,979.089 us |     172,975.459 us |     2,632,924.2 us |     6.40 |     0.63 |
| PipelineAdapter    | 2048       | Sequential           | _int64_128.bin     |       938,454.1 us |     18,597.987 us |      40,430.494 us |       926,631.3 us |     2.37 |     0.25 |
| PipelineAdapter2   | 2048       | Sequential           | _int64_128.bin     |       933,697.6 us |     17,432.140 us |      15,453.138 us |       935,698.9 us |     2.56 |     0.08 |
| PipelineAdapter3   | 2048       | Sequential           | _int64_128.bin     |       960,720.5 us |     18,741.101 us |      20,830.684 us |       956,470.0 us |     2.62 |     0.09 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **2048**   | **Sequential**       | **_int64_4.bin**   |    **10,417.1 us** |    **130.667 us** |     **115.833 us** |    **10,404.4 us** | **1.00** | **0.00** |
| FileStreamAsync    | 2048       | Sequential           | _int64_4.bin       |        17,132.3 us |        331.649 us |         431.237 us |        17,056.4 us |     1.65 |     0.05 |
| PipelineNative     | 2048       | Sequential           | _int64_4.bin       |        73,874.5 us |      1,245.929 us |       1,165.443 us |        74,309.0 us |     7.10 |     0.11 |
| PipelineAdapter    | 2048       | Sequential           | _int64_4.bin       |        28,189.6 us |        409.901 us |         383.422 us |        28,190.7 us |     2.71 |     0.05 |
| PipelineAdapter2   | 2048       | Sequential           | _int64_4.bin       |        32,127.6 us |      1,708.423 us |       5,010.510 us |        31,835.9 us |     2.94 |     0.25 |
| PipelineAdapter3   | 2048       | Sequential           | _int64_4.bin       |        24,774.5 us |        445.964 us |         417.155 us |        24,723.8 us |     2.38 |     0.06 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **4096**   | **Async_Sequential** | **_int64_128.bin** | **1,278,629.9 us** | **62,328.532 us** |  **95,182.251 us** | **1,250,891.7 us** | **1.00** | **0.00** |
| FileStreamAsync    | 4096       | Async_Sequential     | _int64_128.bin     |     1,578,638.5 us |     12,990.924 us |      12,151.719 us |     1,577,514.6 us |     1.21 |     0.11 |
| PipelineNative     | 4096       | Async_Sequential     | _int64_128.bin     |     1,057,353.7 us |     17,952.148 us |      16,792.450 us |     1,057,315.5 us |     0.81 |     0.07 |
| PipelineAdapter    | 4096       | Async_Sequential     | _int64_128.bin     |     1,930,152.5 us |     13,891.394 us |      12,994.018 us |     1,930,945.2 us |     1.47 |     0.13 |
| PipelineAdapter2   | 4096       | Async_Sequential     | _int64_128.bin     |     1,927,733.7 us |     19,186.169 us |      17,946.755 us |     1,928,996.6 us |     1.47 |     0.13 |
| PipelineAdapter3   | 4096       | Async_Sequential     | _int64_128.bin     |     1,923,680.7 us |     14,865.262 us |      13,904.976 us |     1,924,283.6 us |     1.47 |     0.13 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **4096**   | **Async_Sequential** | **_int64_4.bin**   |    **39,033.1 us** |    **650.927 us** |     **608.878 us** |    **39,096.3 us** | **1.00** | **0.00** |
| FileStreamAsync    | 4096       | Async_Sequential     | _int64_4.bin       |        48,869.6 us |        689.265 us |         644.738 us |        48,938.4 us |     1.25 |     0.02 |
| PipelineNative     | 4096       | Async_Sequential     | _int64_4.bin       |        32,571.3 us |        650.169 us |         695.674 us |        32,445.5 us |     0.83 |     0.02 |
| PipelineAdapter    | 4096       | Async_Sequential     | _int64_4.bin       |        61,439.5 us |        771.306 us |         721.480 us |        61,449.4 us |     1.57 |     0.04 |
| PipelineAdapter2   | 4096       | Async_Sequential     | _int64_4.bin       |        82,726.5 us |      2,279.805 us |       6,686.272 us |        84,728.0 us |     1.84 |     0.33 |
| PipelineAdapter3   | 4096       | Async_Sequential     | _int64_4.bin       |        83,967.0 us |      1,063.115 us |         994.439 us |        83,789.5 us |     2.15 |     0.04 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **4096**   | **Sequential**       | **_int64_128.bin** |   **157,013.5 us** |  **1,748.052 us** |   **1,635.129 us** |   **157,320.6 us** | **1.00** | **0.00** |
| FileStreamAsync    | 4096       | Sequential           | _int64_128.bin     |       274,289.4 us |      3,447.316 us |       3,224.621 us |       274,686.1 us |     1.75 |     0.03 |
| PipelineNative     | 4096       | Sequential           | _int64_128.bin     |     1,054,020.7 us |     16,780.576 us |      15,696.561 us |     1,054,086.3 us |     6.71 |     0.12 |
| PipelineAdapter    | 4096       | Sequential           | _int64_128.bin     |       443,630.0 us |      8,732.901 us |      10,395.898 us |       442,736.6 us |     2.81 |     0.07 |
| PipelineAdapter2   | 4096       | Sequential           | _int64_128.bin     |       437,548.9 us |      8,501.127 us |      12,192.062 us |       433,129.9 us |     2.81 |     0.08 |
| PipelineAdapter3   | 4096       | Sequential           | _int64_128.bin     |       422,289.6 us |      8,418.052 us |       9,356.643 us |       424,202.1 us |     2.68 |     0.06 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **4096**   | **Sequential**       | **_int64_4.bin**   |     **4,802.8 us** |     **91.701 us** |     **112.617 us** |     **4,799.6 us** | **1.00** | **0.00** |
| FileStreamAsync    | 4096       | Sequential           | _int64_4.bin       |         8,628.0 us |        100.480 us |          93.989 us |         8,609.6 us |     1.80 |     0.05 |
| PipelineNative     | 4096       | Sequential           | _int64_4.bin       |        34,379.9 us |        650.878 us |         668.404 us |        34,550.8 us |     7.19 |     0.19 |
| PipelineAdapter    | 4096       | Sequential           | _int64_4.bin       |        12,856.8 us |        247.623 us |         231.627 us |        12,831.7 us |     2.69 |     0.08 |
| PipelineAdapter2   | 4096       | Sequential           | _int64_4.bin       |        13,259.4 us |        122.545 us |         114.629 us |        13,253.8 us |     2.77 |     0.08 |
| PipelineAdapter3   | 4096       | Sequential           | _int64_4.bin       |        13,176.9 us |        163.307 us |         152.757 us |        13,177.7 us |     2.75 |     0.06 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **16384**  | **Async_Sequential** | **_int64_128.bin** |   **335,609.9 us** |  **6,041.110 us** |   **5,650.858 us** |   **334,384.8 us** | **1.00** | **0.00** |
| FileStreamAsync    | 16384      | Async_Sequential     | _int64_128.bin     |       408,429.5 us |      8,100.258 us |       7,955.537 us |       408,335.9 us |     1.22 |     0.04 |
| PipelineNative     | 16384      | Async_Sequential     | _int64_128.bin     |       303,897.3 us |      5,029.804 us |       4,458.790 us |       303,986.3 us |     0.91 |     0.02 |
| PipelineAdapter    | 16384      | Async_Sequential     | _int64_128.bin     |       515,902.3 us |      8,013.775 us |       7,496.090 us |       515,574.2 us |     1.54 |     0.04 |
| PipelineAdapter2   | 16384      | Async_Sequential     | _int64_128.bin     |       500,876.2 us |      8,904.030 us |       8,328.835 us |       504,404.7 us |     1.49 |     0.04 |
| PipelineAdapter3   | 16384      | Async_Sequential     | _int64_128.bin     |       505,080.4 us |      5,977.826 us |       5,591.662 us |       505,011.8 us |     1.51 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **16384**  | **Async_Sequential** | **_int64_4.bin**   |    **10,362.0 us** |    **110.252 us** |     **103.130 us** |    **10,369.3 us** | **1.00** | **0.00** |
| FileStreamAsync    | 16384      | Async_Sequential     | _int64_4.bin       |        13,322.5 us |        175.760 us |         164.406 us |        13,347.3 us |     1.29 |     0.02 |
| PipelineNative     | 16384      | Async_Sequential     | _int64_4.bin       |         9,356.7 us |        178.654 us |         167.113 us |         9,352.9 us |     0.90 |     0.02 |
| PipelineAdapter    | 16384      | Async_Sequential     | _int64_4.bin       |        16,566.3 us |        305.305 us |         285.583 us |        16,603.9 us |     1.60 |     0.03 |
| PipelineAdapter2   | 16384      | Async_Sequential     | _int64_4.bin       |        16,367.0 us |        286.569 us |         268.057 us |        16,382.6 us |     1.58 |     0.02 |
| PipelineAdapter3   | 16384      | Async_Sequential     | _int64_4.bin       |        16,035.1 us |        264.944 us |         247.829 us |        16,075.7 us |     1.55 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **16384**  | **Sequential**       | **_int64_128.bin** |    **56,581.1 us** |    **547.899 us** |     **512.505 us** |    **56,362.0 us** | **1.00** | **0.00** |
| FileStreamAsync    | 16384      | Sequential           | _int64_128.bin     |        97,691.5 us |      1,743.049 us |       1,630.449 us |        98,156.9 us |     1.73 |     0.03 |
| PipelineNative     | 16384      | Sequential           | _int64_128.bin     |       303,071.5 us |      6,000.008 us |       9,688.895 us |       301,179.5 us |     5.26 |     0.17 |
| PipelineAdapter    | 16384      | Sequential           | _int64_128.bin     |       155,320.0 us |      2,760.812 us |       2,582.465 us |       155,346.2 us |     2.75 |     0.05 |
| PipelineAdapter2   | 16384      | Sequential           | _int64_128.bin     |       153,646.0 us |      2,993.394 us |       2,800.023 us |       154,104.4 us |     2.72 |     0.06 |
| PipelineAdapter3   | 16384      | Sequential           | _int64_128.bin     |       154,569.5 us |      1,723.009 us |       1,611.704 us |       154,835.1 us |     2.73 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **16384**  | **Sequential**       | **_int64_4.bin**   |     **1,672.6 us** |      **9.147 us** |       **8.557 us** |     **1,675.0 us** | **1.00** | **0.00** |
| FileStreamAsync    | 16384      | Sequential           | _int64_4.bin       |         3,160.0 us |         61.814 us |          88.652 us |         3,178.5 us |     1.89 |     0.05 |
| PipelineNative     | 16384      | Sequential           | _int64_4.bin       |        10,864.9 us |        212.976 us |         394.765 us |        10,932.3 us |     6.35 |     0.32 |
| PipelineAdapter    | 16384      | Sequential           | _int64_4.bin       |         6,241.1 us |        121.372 us |         124.640 us |         6,264.2 us |     3.72 |     0.08 |
| PipelineAdapter2   | 16384      | Sequential           | _int64_4.bin       |         6,257.6 us |        124.254 us |         143.091 us |         6,255.2 us |     3.76 |     0.08 |
| PipelineAdapter3   | 16384      | Sequential           | _int64_4.bin       |         5,063.0 us |        141.425 us |         301.388 us |         4,976.9 us |     3.19 |     0.30 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **65536**  | **Async_Sequential** | **_int64_128.bin** |   **107,556.4 us** |  **1,178.186 us** |   **1,102.076 us** |   **107,306.1 us** | **1.00** | **0.00** |
| FileStreamAsync    | 65536      | Async_Sequential     | _int64_128.bin     |       125,562.2 us |      2,229.077 us |       2,085.080 us |       126,091.6 us |     1.17 |     0.02 |
| PipelineNative     | 65536      | Async_Sequential     | _int64_128.bin     |       107,382.2 us |      2,068.785 us |       1,935.142 us |       107,561.3 us |     1.00 |     0.01 |
| PipelineAdapter    | 65536      | Async_Sequential     | _int64_128.bin     |       203,740.4 us |      2,006.881 us |       1,877.238 us |       203,551.3 us |     1.89 |     0.03 |
| PipelineAdapter2   | 65536      | Async_Sequential     | _int64_128.bin     |       199,475.8 us |      3,782.558 us |       3,714.978 us |       198,790.2 us |     1.86 |     0.04 |
| PipelineAdapter3   | 65536      | Async_Sequential     | _int64_128.bin     |       201,418.5 us |      3,681.838 us |       3,443.993 us |       200,802.6 us |     1.87 |     0.04 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **65536**  | **Async_Sequential** | **_int64_4.bin**   |     **3,332.9 us** |     **26.959 us** |      **25.217 us** |     **3,333.1 us** | **1.00** | **0.00** |
| FileStreamAsync    | 65536      | Async_Sequential     | _int64_4.bin       |         4,207.7 us |         58.846 us |          55.044 us |         4,204.6 us |     1.26 |     0.02 |
| PipelineNative     | 65536      | Async_Sequential     | _int64_4.bin       |         3,425.3 us |         45.455 us |          42.519 us |         3,433.1 us |     1.03 |     0.02 |
| PipelineAdapter    | 65536      | Async_Sequential     | _int64_4.bin       |         6,382.1 us |        111.426 us |         104.228 us |         6,351.3 us |     1.91 |     0.03 |
| PipelineAdapter2   | 65536      | Async_Sequential     | _int64_4.bin       |         6,331.2 us |         47.550 us |          44.479 us |         6,325.7 us |     1.90 |     0.02 |
| PipelineAdapter3   | 65536      | Async_Sequential     | _int64_4.bin       |         6,270.0 us |         68.747 us |          64.306 us |         6,267.5 us |     1.88 |     0.02 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **65536**  | **Sequential**       | **_int64_128.bin** |    **29,076.4 us** |    **134.628 us** |     **112.421 us** |    **29,085.0 us** | **1.00** | **0.00** |
| FileStreamAsync    | 65536      | Sequential           | _int64_128.bin     |        47,373.0 us |        849.843 us |         794.943 us |        47,550.3 us |     1.63 |     0.03 |
| PipelineNative     | 65536      | Sequential           | _int64_128.bin     |       106,500.6 us |      2,066.380 us |       2,029.461 us |       106,882.6 us |     3.66 |     0.08 |
| PipelineAdapter    | 65536      | Sequential           | _int64_128.bin     |        77,113.4 us |      1,195.812 us |       1,118.563 us |        77,053.9 us |     2.66 |     0.04 |
| PipelineAdapter2   | 65536      | Sequential           | _int64_128.bin     |        73,043.4 us |        959.639 us |         897.647 us |        72,822.9 us |     2.51 |     0.03 |
| PipelineAdapter3   | 65536      | Sequential           | _int64_128.bin     |        74,624.9 us |        976.903 us |         913.796 us |        74,161.4 us |     2.56 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **65536**  | **Sequential**       | **_int64_4.bin**   |       **918.9 us** |      **6.207 us** |       **5.502 us** |       **918.6 us** | **1.00** | **0.00** |
| FileStreamAsync    | 65536      | Sequential           | _int64_4.bin       |         1,591.3 us |         30.752 us |          41.053 us |         1,578.1 us |     1.76 |     0.05 |
| PipelineNative     | 65536      | Sequential           | _int64_4.bin       |         3,433.9 us |         49.741 us |          46.528 us |         3,433.3 us |     3.74 |     0.06 |
| PipelineAdapter    | 65536      | Sequential           | _int64_4.bin       |         2,290.6 us |         44.908 us |          51.716 us |         2,289.9 us |     2.49 |     0.06 |
| PipelineAdapter2   | 65536      | Sequential           | _int64_4.bin       |         2,333.9 us |         38.928 us |          34.509 us |         2,336.3 us |     2.54 |     0.04 |
| PipelineAdapter3   | 65536      | Sequential           | _int64_4.bin       |         2,298.0 us |         31.527 us |          29.491 us |         2,302.2 us |     2.50 |     0.04 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **262144** | **Async_Sequential** | **_int64_128.bin** |    **44,936.0 us** |    **640.730 us** |     **599.339 us** |    **45,053.7 us** | **1.00** | **0.00** |
| FileStreamAsync    | 262144     | Async_Sequential     | _int64_128.bin     |        49,876.7 us |        429.915 us |         402.143 us |        49,724.2 us |     1.11 |     0.01 |
| PipelineNative     | 262144     | Async_Sequential     | _int64_128.bin     |        44,884.0 us |        581.473 us |         543.910 us |        45,024.9 us |     1.00 |     0.02 |
| PipelineAdapter    | 262144     | Async_Sequential     | _int64_128.bin     |        68,550.8 us |      1,325.400 us |       1,526.332 us |        68,285.8 us |     1.53 |     0.04 |
| PipelineAdapter2   | 262144     | Async_Sequential     | _int64_128.bin     |        70,488.0 us |      1,079.301 us |       1,009.579 us |        70,341.7 us |     1.57 |     0.02 |
| PipelineAdapter3   | 262144     | Async_Sequential     | _int64_128.bin     |        70,533.9 us |        616.385 us |         546.409 us |        70,480.2 us |     1.57 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **262144** | **Async_Sequential** | **_int64_4.bin**   |     **1,580.2 us** |     **19.631 us** |      **18.363 us** |     **1,586.0 us** | **1.00** | **0.00** |
| FileStreamAsync    | 262144     | Async_Sequential     | _int64_4.bin       |         1,941.5 us |         30.358 us |          28.397 us |         1,951.5 us |     1.23 |     0.02 |
| PipelineNative     | 262144     | Async_Sequential     | _int64_4.bin       |         1,476.3 us |         28.015 us |          26.205 us |         1,472.3 us |     0.93 |     0.02 |
| PipelineAdapter    | 262144     | Async_Sequential     | _int64_4.bin       |         2,323.9 us |         29.659 us |          27.743 us |         2,334.8 us |     1.47 |     0.02 |
| PipelineAdapter2   | 262144     | Async_Sequential     | _int64_4.bin       |         2,295.6 us |         29.636 us |          27.721 us |         2,293.6 us |     1.45 |     0.03 |
| PipelineAdapter3   | 262144     | Async_Sequential     | _int64_4.bin       |         2,294.7 us |         30.288 us |          28.332 us |         2,294.6 us |     1.45 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **262144** | **Sequential**       | **_int64_128.bin** |    **24,367.6 us** |    **227.903 us** |     **202.030 us** |    **24,320.1 us** | **1.00** | **0.00** |
| FileStreamAsync    | 262144     | Sequential           | _int64_128.bin     |        33,114.1 us |        436.462 us |         386.912 us |        33,193.4 us |     1.36 |     0.02 |
| PipelineNative     | 262144     | Sequential           | _int64_128.bin     |        45,332.8 us |        570.905 us |         534.025 us |        45,355.9 us |     1.86 |     0.03 |
| PipelineAdapter    | 262144     | Sequential           | _int64_128.bin     |        47,459.6 us |        374.590 us |         350.392 us |        47,355.0 us |     1.95 |     0.02 |
| PipelineAdapter2   | 262144     | Sequential           | _int64_128.bin     |        47,878.4 us |        635.916 us |         594.836 us |        47,769.4 us |     1.96 |     0.03 |
| PipelineAdapter3   | 262144     | Sequential           | _int64_128.bin     |        48,205.8 us |        618.606 us |         578.644 us |        48,063.5 us |     1.98 |     0.03 |
|                    |            |                      |                    |                    |                   |                    |                    |          |          |
| **FileStreamSync** | **262144** | **Sequential**       | **_int64_4.bin**   |       **876.6 us** |      **9.510 us** |       **8.896 us** |       **875.8 us** | **1.00** | **0.00** |
| FileStreamAsync    | 262144     | Sequential           | _int64_4.bin       |         1,269.4 us |         17.702 us |          16.559 us |         1,272.8 us |     1.45 |     0.03 |
| PipelineNative     | 262144     | Sequential           | _int64_4.bin       |                 NA |                NA |                 NA |                 NA |        ? |        ? |
| PipelineAdapter    | 262144     | Sequential           | _int64_4.bin       |         1,540.1 us |         10.484 us |           9.806 us |         1,544.3 us |     1.76 |     0.02 |
| PipelineAdapter2   | 262144     | Sequential           | _int64_4.bin       |         1,583.2 us |         22.778 us |          21.307 us |         1,586.5 us |     1.81 |     0.03 |
| PipelineAdapter3   | 262144     | Sequential           | _int64_4.bin       |         1,557.6 us |         14.485 us |          13.549 us |         1,557.3 us |     1.78 |     0.03 |


| Method             | BufferSize | FileOptionsString    | File               |   Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
|--------------------|------------|----------------------|--------------------|--------------:|------------:|------------:|--------------------:|
| **FileStreamSync** | **2048**   | **Async_Sequential** | **_int64_128.bin** | **4000.0000** |       **-** |       **-** |      **20504568 B** |
| FileStreamAsync    | 2048       | Async_Sequential     | _int64_128.bin     |     4000.0000 |           - |           - |              2872 B |
| PipelineNative     | 2048       | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1408 B |
| PipelineAdapter    | 2048       | Async_Sequential     | _int64_128.bin     |     5000.0000 |           - |           - |              2160 B |
| PipelineAdapter2   | 2048       | Async_Sequential     | _int64_128.bin     |     5000.0000 |           - |           - |              1392 B |
| PipelineAdapter3   | 2048       | Async_Sequential     | _int64_128.bin     |     5000.0000 |           - |           - |              1400 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **2048**   | **Async_Sequential** | **_int64_4.bin**   |         **-** |       **-** |       **-** |        **660684 B** |
| FileStreamAsync    | 2048       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              2800 B |
| PipelineNative     | 2048       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 2048       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 2048       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1253 B |
| PipelineAdapter3   | 2048       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **2048**   | **Sequential**       | **_int64_128.bin** |         **-** |       **-** |       **-** |          **2248 B** |
| FileStreamAsync    | 2048       | Sequential           | _int64_128.bin     |     3000.0000 |           - |           - |              2816 B |
| PipelineNative     | 2048       | Sequential           | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 2048       | Sequential           | _int64_128.bin     |     4000.0000 |           - |           - |              1680 B |
| PipelineAdapter2   | 2048       | Sequential           | _int64_128.bin     |     4000.0000 |           - |           - |              1480 B |
| PipelineAdapter3   | 2048       | Sequential           | _int64_128.bin     |     4000.0000 |           - |           - |              1368 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **2048**   | **Sequential**       | **_int64_4.bin**   |         **-** |       **-** |       **-** |          **2248 B** |
| FileStreamAsync    | 2048       | Sequential           | _int64_4.bin       |       93.7500 |           - |           - |              2744 B |
| PipelineNative     | 2048       | Sequential           | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 2048       | Sequential           | _int64_4.bin       |      125.0000 |           - |           - |              1680 B |
| PipelineAdapter2   | 2048       | Sequential           | _int64_4.bin       |      125.0000 |           - |           - |              1178 B |
| PipelineAdapter3   | 2048       | Sequential           | _int64_4.bin       |      125.0000 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **4096**   | **Async_Sequential** | **_int64_128.bin** | **2000.0000** |       **-** |       **-** |      **10287488 B** |
| FileStreamAsync    | 4096       | Async_Sequential     | _int64_128.bin     |     2000.0000 |           - |           - |              4920 B |
| PipelineNative     | 4096       | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1408 B |
| PipelineAdapter    | 4096       | Async_Sequential     | _int64_128.bin     |     2000.0000 |           - |           - |              2160 B |
| PipelineAdapter2   | 4096       | Async_Sequential     | _int64_128.bin     |     2000.0000 |           - |           - |              1392 B |
| PipelineAdapter3   | 4096       | Async_Sequential     | _int64_128.bin     |     2000.0000 |           - |           - |              1400 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **4096**   | **Async_Sequential** | **_int64_4.bin**   |   **71.4286** |       **-** |       **-** |        **325014 B** |
| FileStreamAsync    | 4096       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              4848 B |
| PipelineNative     | 4096       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 4096       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 4096       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1207 B |
| PipelineAdapter3   | 4096       | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **4096**   | **Sequential**       | **_int64_128.bin** |         **-** |       **-** |       **-** |          **4296 B** |
| FileStreamAsync    | 4096       | Sequential           | _int64_128.bin     |     1500.0000 |           - |           - |              4792 B |
| PipelineNative     | 4096       | Sequential           | _int64_128.bin     |             - |           - |           - |              1408 B |
| PipelineAdapter    | 4096       | Sequential           | _int64_128.bin     |     2000.0000 |           - |           - |              1880 B |
| PipelineAdapter2   | 4096       | Sequential           | _int64_128.bin     |     2000.0000 |           - |           - |              1360 B |
| PipelineAdapter3   | 4096       | Sequential           | _int64_128.bin     |     2000.0000 |           - |           - |              1368 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **4096**   | **Sequential**       | **_int64_4.bin**   |         **-** |       **-** |       **-** |          **4296 B** |
| FileStreamAsync    | 4096       | Sequential           | _int64_4.bin       |       46.8750 |           - |           - |              4792 B |
| PipelineNative     | 4096       | Sequential           | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 4096       | Sequential           | _int64_4.bin       |       62.5000 |           - |           - |              1680 B |
| PipelineAdapter2   | 4096       | Sequential           | _int64_4.bin       |       62.5000 |           - |           - |              1168 B |
| PipelineAdapter3   | 4096       | Sequential           | _int64_4.bin       |       62.5000 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **16384**  | **Async_Sequential** | **_int64_128.bin** |         **-** |       **-** |       **-** |       **2592016 B** |
| FileStreamAsync    | 16384      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |             17208 B |
| PipelineNative     | 16384      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1408 B |
| PipelineAdapter    | 16384      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              2160 B |
| PipelineAdapter2   | 16384      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1512 B |
| PipelineAdapter3   | 16384      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1400 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **16384**  | **Async_Sequential** | **_int64_4.bin**   |   **15.6250** |       **-** |       **-** |         **96994 B** |
| FileStreamAsync    | 16384      | Async_Sequential     | _int64_4.bin       |       15.6250 |           - |           - |             17136 B |
| PipelineNative     | 16384      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 16384      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 16384      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1190 B |
| PipelineAdapter3   | 16384      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **16384**  | **Sequential**       | **_int64_128.bin** |         **-** |       **-** |       **-** |         **16584 B** |
| FileStreamAsync    | 16384      | Sequential           | _int64_128.bin     |      400.0000 |           - |           - |             17080 B |
| PipelineNative     | 16384      | Sequential           | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 16384      | Sequential           | _int64_128.bin     |      500.0000 |           - |           - |              1680 B |
| PipelineAdapter2   | 16384      | Sequential           | _int64_128.bin     |      500.0000 |           - |           - |              1256 B |
| PipelineAdapter3   | 16384      | Sequential           | _int64_128.bin     |      500.0000 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **16384**  | **Sequential**       | **_int64_4.bin**   |    **3.9063** |       **-** |       **-** |         **16584 B** |
| FileStreamAsync    | 16384      | Sequential           | _int64_4.bin       |       15.6250 |           - |           - |             17080 B |
| PipelineNative     | 16384      | Sequential           | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 16384      | Sequential           | _int64_4.bin       |       15.6250 |           - |           - |              1680 B |
| PipelineAdapter2   | 16384      | Sequential           | _int64_4.bin       |       15.6250 |           - |           - |              1157 B |
| PipelineAdapter3   | 16384      | Sequential           | _int64_4.bin       |       15.6250 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **65536**  | **Async_Sequential** | **_int64_128.bin** |         **-** |       **-** |       **-** |        **710480 B** |
| FileStreamAsync    | 65536      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |             66288 B |
| PipelineNative     | 65536      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 65536      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 65536      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1253 B |
| PipelineAdapter3   | 65536      | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **65536**  | **Async_Sequential** | **_int64_4.bin**   |   **19.5313** |       **-** |       **-** |         **86180 B** |
| FileStreamAsync    | 65536      | Async_Sequential     | _int64_4.bin       |       15.6250 |           - |           - |             66288 B |
| PipelineNative     | 65536      | Async_Sequential     | _int64_4.bin       |        3.9063 |           - |           - |              1296 B |
| PipelineAdapter    | 65536      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 65536      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1186 B |
| PipelineAdapter3   | 65536      | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **65536**  | **Sequential**       | **_int64_128.bin** |         **-** |       **-** |       **-** |         **65736 B** |
| FileStreamAsync    | 65536      | Sequential           | _int64_128.bin     |       90.9091 |           - |           - |             66232 B |
| PipelineNative     | 65536      | Sequential           | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 65536      | Sequential           | _int64_128.bin     |      142.8571 |           - |           - |              1680 B |
| PipelineAdapter2   | 65536      | Sequential           | _int64_128.bin     |      142.8571 |           - |           - |              1182 B |
| PipelineAdapter3   | 65536      | Sequential           | _int64_128.bin     |      142.8571 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **65536**  | **Sequential**       | **_int64_4.bin**   |   **14.6484** |       **-** |       **-** |         **65736 B** |
| FileStreamAsync    | 65536      | Sequential           | _int64_4.bin       |       19.5313 |           - |           - |             66232 B |
| PipelineNative     | 65536      | Sequential           | _int64_4.bin       |        3.9063 |           - |           - |              1296 B |
| PipelineAdapter    | 65536      | Sequential           | _int64_4.bin       |        3.9063 |           - |           - |              1680 B |
| PipelineAdapter2   | 65536      | Sequential           | _int64_4.bin       |        3.9063 |           - |           - |              1154 B |
| PipelineAdapter3   | 65536      | Sequential           | _int64_4.bin       |        3.9063 |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **262144** | **Async_Sequential** | **_int64_128.bin** |         **-** |       **-** |       **-** |        **423329 B** |
| FileStreamAsync    | 262144     | Async_Sequential     | _int64_128.bin     |             - |           - |           - |            262896 B |
| PipelineNative     | 262144     | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 262144     | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 262144     | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1210 B |
| PipelineAdapter3   | 262144     | Async_Sequential     | _int64_128.bin     |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **262144** | **Async_Sequential** | **_int64_4.bin**   |   **82.0313** | **82.0313** | **82.0313** |        **267745 B** |
| FileStreamAsync    | 262144     | Async_Sequential     | _int64_4.bin       |       82.0313 |     82.0313 |     82.0313 |            262896 B |
| PipelineNative     | 262144     | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1296 B |
| PipelineAdapter    | 262144     | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1960 B |
| PipelineAdapter2   | 262144     | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1185 B |
| PipelineAdapter3   | 262144     | Async_Sequential     | _int64_4.bin       |             - |           - |           - |              1280 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **262144** | **Sequential**       | **_int64_128.bin** |   **62.5000** | **62.5000** | **62.5000** |        **262344 B** |
| FileStreamAsync    | 262144     | Sequential           | _int64_128.bin     |       62.5000 |     62.5000 |     62.5000 |            262840 B |
| PipelineNative     | 262144     | Sequential           | _int64_128.bin     |             - |           - |           - |              1296 B |
| PipelineAdapter    | 262144     | Sequential           | _int64_128.bin     |             - |           - |           - |              1680 B |
| PipelineAdapter2   | 262144     | Sequential           | _int64_128.bin     |             - |           - |           - |              1171 B |
| PipelineAdapter3   | 262144     | Sequential           | _int64_128.bin     |             - |           - |           - |              1248 B |
|                    |            |                      |                    |               |             |             |                     |
| **FileStreamSync** | **262144** | **Sequential**       | **_int64_4.bin**   |   **83.0078** | **83.0078** | **83.0078** |        **262344 B** |
| FileStreamAsync    | 262144     | Sequential           | _int64_4.bin       |       82.0313 |     82.0313 |     82.0313 |            262840 B |
| PipelineNative     | 262144     | Sequential           | _int64_4.bin       |             - |           - |           - |                   - |
| PipelineAdapter    | 262144     | Sequential           | _int64_4.bin       |             - |           - |           - |              1680 B |
| PipelineAdapter2   | 262144     | Sequential           | _int64_4.bin       |             - |           - |           - |              1152 B |
| PipelineAdapter3   | 262144     | Sequential           | _int64_4.bin       |             - |           - |           - |              1248 B |


## Observations

Note: the `FileOptions` parameter has no effect on the PipelineNative method

  - In **every single case** specifying the additional `FileOptions.Asynchronous` flag
    resulted in slower performance than not
    - Where the Buffer, File, and Method are constant
    - Except in the PipelineNative method where this parameter is ignored
    - On average specifying  `FileOptions.Asynchronous` was `3.56` times slower than not
      - In the worst case it was `9.22` times slower
      - In the best case it was `1.45` times slower (File=_int64_128.bin, Buffer=262144, Method=PipelineAdapter2)
  - FileStream's sync methods are _always_ fastest
    - For just `FileOptions.SequentialScan | FileOptions.Asynchronous`
      - PipelineNative performs better (remember `FileOptions` has no effect)
  - FileStream's async methods are always faster than all pipeline methods
    - Except when `FileOptions.SequentialScan | FileOptions.Asynchronous` is set
      - PipelineNative performs better (remember `FileOptions` has no effect)
  - BufferSize has a large affect on performance, decreasing read times as buffer size increases
    - this effect held for all groups (when Method, FileOptions, and FileSize are the same)
  - The FileStream async method is consistently slower than its sync counterpart
    - Though the ratio shrinks from `1.92` times slower at small buffers sizes to only `1.11` times at large buffers
    - This effect is less severe when `FileOptions.SequentialScan | FileOptions.Asynchronous` is set
  - For the native pipeline wrapper
    - If we split the results into three groups:
      1. `FileOptions.SequentialScan`: Results within groups are faster than PipelineNative
      2. PipelineNative sits in the middle
      3. `FileOptions.SequentialScan | FileOptions.Asynchronous`: Results with groups are slower than PipelineNative
    - The native API pipeline performs very poorly (up to `7` times slower than sync) at small buffer sizes when `FileOptions.SequentialScan` is set (Group 1 vs 2)
    - The native API pipeline performs very well (`0.892` times sync) when `FileOptions.SequentialScan | FileOptions.Asynchronous` (Group 3 vs 2)
  - Pipeline adapters (summarised for `FileOptions.SequentialScan` only)
    - The adapters also perform _much_ better than the native pipeline at lower buffer
      - On average `2.582` time slower than FileStreamSync
      - But faster than PipeLineNative which was on average `5.36` times slower the FileStreamSync
    - All of the pipeline adapters have fairly consistent performance compared to each other
    - They also experience less variance across experiemnts
      - the slowest ratio is `3.19` times FileStreamSync and fastest is `1.76` times FileStreamSync (`1.43` ratio variance over tests)
      - this is much less variance than experienced by the native pipeline (`5.33` ratio variance over tests)
  - File size differences:
    - the smaller file regularly is slower per MB than the larger file
      - Mean time taken scaled relative to file size
      - this effect is not seen with the PipelineAdapters (close to equivalent time taken per MB) at all buffer sizes
      - For PipeLineNative
        - this effect is prominent with buffer size `2048`
        - and is non-existent at larger buffer sizes
  - Memory:
    - The traditional file stream methods always allocate at least as much memory as the buffer
    - The FileStreamSync method with the `FileOptions.SequentialScan | FileOptions.Asynchronous` option allocates very large amounts of memory and should be avoided!
      - `20`MB per operation in the worse case!
    - All pipeline methods allocate less memory than non-pipeline methods
    - The native pipeline never requires garbage collections for Gen 0, 1, or 2!
    - Only the traditional file stream methods have objects that survive to Gen 1 and 2, and only at buffer size `262144`
    - The pipeline adapters do require garbage collection for Gen 0 objects:
      - At the smallest buffer size (`2048`), `4000` collections occurred!
      - But after buffer size `16384`, the number of collections are equal or less than other methods, and sometime require no collections also


  ---

  Anthony Truskinger (@atruskie)
