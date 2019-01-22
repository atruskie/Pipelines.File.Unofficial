# extraction of fixtures

## Windows

- Ensure `bzip2` is installed (I got mine from [MinGW](http://www.mingw.org/))

Decompresses all files in the folder with the `.xz` extension, keeping the original compressed files.
```
unxz -k -f -T 0 *.xz
```

# compression of fixtures

Compresses all fixtures in the current folder with the `.bin` extension, keeping the original uncompressed files.
```
xz -0 -k -f -e -T 0 *.bin
```

# Generation of fixtures

```powershell
 $limit = 1GB / 8; $file = [System.IO.File]::Open("$pwd\incrementing_int64.bin", [System.IO.FileAccess]::Write, [System.IO.FileMode]::Create); $counter = 0; while($counter -lt $limit) { $file.Write([BitConverter]::GetBytes([long]$counter),0,8); $counter++ } $file.Close();
```