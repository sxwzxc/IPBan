using System;
using System.IO;

namespace DigitalRuby.IPBanCore;

/// <summary>
/// A temp file
/// </summary>
public sealed class TempFile : IDisposable
{
    /// <summary>
    /// Gets the full path of the temp directory used for storing temporary files.
    /// </summary>
    public static string TempDirectory { get; }

    static TempFile()
    {
        TempDirectory = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location) + "_TempFiles";
        TempDirectory = Path.Combine(OSUtility.TempFolder, TempDirectory);
        DeleteTempDirectory();
        Directory.CreateDirectory(TempDirectory);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => DeleteTempDirectory();
    }

    /// <summary>
    /// Constructor. Creates the file name but does not create the file itself.
    /// </summary>
    /// <param name="name">Full path of file name (null to generate one)</param>
    public TempFile(string name = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Path.Combine(TempDirectory, Path.GetRandomFileName());
        }
        FullName = name;
    }

    /// <summary>
    /// Finalizer (calls Dispose)
    /// </summary>
    ~TempFile()
    {
        Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            ExtensionMethods.FileDeleteWithRetry(FullName);
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FullName;
    }

    /// <summary>
    /// Implicit conversion to string (full path)
    /// </summary>
    /// <param name="tempFile">Temp file</param>
    public static implicit operator string(TempFile tempFile)
    {
        return tempFile.FullName;
    }

    /// <summary>
    /// Deletes the TempDirectory.
    /// </summary>
    private static void DeleteTempDirectory()
    {
        if (Directory.Exists(TempDirectory))
        {
            try
            {
                Directory.Delete(TempDirectory, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Full path to the temp file
    /// </summary>
    public string FullName { get; }
}
