namespace Bukit.Importing.Seed;

public interface IImportSeedService
{
    ImportSeedResult Import(ImportSeedOptions options);
}
