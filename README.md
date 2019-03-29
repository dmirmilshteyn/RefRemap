# RefRemap

This is a tool to remap assembly references from any .NET assembly/executable. It scans the IL of the input assembly for anything that references one (or more) of the source assembly names, then rewrites it to reference the target assembly. It is expected that corresponding types/methods are available in the target assembly, with identical namespaces.

One application of this tool is when a set of assemblies are merged, while there are additional third-party assemblies that reference the original, unmerged, assemblies. This tool helps to rewrite those third-party assemblies to reference the newly merged assemblies.

Example:

`[AssemblyA]MethodCall()` will be remapped to `[MergedAssembly]MethodCall()`.

## Installation

1) Download the project, then run `dotnet pack` to create a NuGet package.
2) `dotnet tool install RefRemap --global --add-source /path/to/built/nuget/package/directory`

## Usage

```
Usage:  [arguments] [options]

Arguments:
  assembly  The path to the assembly to be edited.

Options:
  -?|--help    Show help information
  -s|--source  Source assembly name to be remapped.
  -t|--target  Target assembly path.
  -o|--output  Output path where the generated assembly will be written to.
```

Example:

`dotnet refremap /path/to/assembly --source AssemblyA --source AssemblyB --target /path/to/merged/assembly --output /path/to/rewritten/assembly`