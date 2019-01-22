using System.IO;
// ReSharper disable InconsistentNaming

namespace TestFixtures
{
    public static partial class Fixtures
    {

        public static string IncrementingUint32_4 { get; } = J("incrementing_uint32_4.bin");
        public static string IncrementingInt64_512 { get; } = J("incrementing_int64_512.bin");
        public static string IncrementingInt64_128 { get; } = J("incrementing_int64_128.bin");
        public static string IncrementingInt64_4 { get; } = J("incrementing_int64_4.bin");

        private static string J(string path) => Path.Combine(FixturesPath, path);
    }
}