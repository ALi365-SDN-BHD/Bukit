internal static class TestCleanup
{
    public static void DeleteDirectory(string path, bool recursive = true)
    {
        try
        {
            Directory.Delete(path, recursive);
        }
        catch (DirectoryNotFoundException ex)
        {
            IgnoreCleanupFailure(ex);
        }
        catch (IOException ex)
        {
            IgnoreCleanupFailure(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            IgnoreCleanupFailure(ex);
        }
    }

    public static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (FileNotFoundException ex)
        {
            IgnoreCleanupFailure(ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            IgnoreCleanupFailure(ex);
        }
        catch (IOException ex)
        {
            IgnoreCleanupFailure(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            IgnoreCleanupFailure(ex);
        }
    }

    private static void IgnoreCleanupFailure(Exception ex)
    {
        GC.KeepAlive(ex);
    }
}
