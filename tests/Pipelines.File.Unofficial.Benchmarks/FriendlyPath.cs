using System;

namespace Pipelines.File.Unofficial.Benchmarks
{
    public struct FriendlyPath
    {
        public FriendlyPath(string path) : this()
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }

        public string Path { get; }

        public string Name { get; }

        public static implicit operator string(FriendlyPath friendly)
        {
            return friendly.Path;
        }

        public override string ToString()
        {
            return Name.Substring(Name.IndexOf("_", StringComparison.Ordinal));
        }
    }
}