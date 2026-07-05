namespace Bukit.Importing;

public static class ImportSeedService
{
    public static ImportSeedResult Import(string inputDir, string outputDir, bool force)
    {
        if (string.IsNullOrWhiteSpace(inputDir))
            throw new ImportException(ImportErrorKind.UserInput, "缺少必填参数: <seed-dir>");
        if (!Directory.Exists(inputDir))
            throw new ImportException(ImportErrorKind.UserInput, $"seed 目录不存在: {inputDir}");
        if (string.IsNullOrWhiteSpace(outputDir))
            throw new ImportException(ImportErrorKind.UserInput, "缺少必填选项: --output <content-dir>");

        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var writtenFiles = ImportSeedContentWriter.WriteMarkdown(outputDir, records, force);
        return new ImportSeedResult
        {
            InputDir = inputDir,
            OutputDir = outputDir,
            RecordsRead = records.Count,
            FilesWritten = writtenFiles.Count,
            WrittenFiles = writtenFiles
        };
    }
}
