namespace Bukit.Importing.Seed;

public sealed record ImportSeedOptions(
    string ProjectRoot,
    string SeedDirectory,
    string OutputDirectory,
    bool Force);
