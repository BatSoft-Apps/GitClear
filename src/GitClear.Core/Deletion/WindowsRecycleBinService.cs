using System.Runtime.InteropServices;

namespace GitClear.Core.Deletion;

// The Restore half of this service uses the late-bound Shell.Application COM
// object (the only practical way to move items out of the Recycle Bin back to
// their original locations), so it tolerates the loose COM error surface.
#pragma warning disable CA1031 // COM restore is best-effort; failures degrade to "not restored".

/// <summary>
/// Recycle Bin implementation over the Win32 <c>SHFileOperation</c> shell API,
/// using <c>FOF_ALLOWUNDO</c> so deletions are undoable, with the no-UI flags so
/// the core never pops shell dialogs. Paths are sent in chunks to stay well
/// within any internal limit on the batched path buffer.
/// </summary>
public sealed class WindowsRecycleBinService : IRecycleBinService
{
    private const int ChunkSize = 4096;

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    private const ushort Flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;

    public void Recycle(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        for (var offset = 0; offset < paths.Count; offset += ChunkSize)
        {
            var count = Math.Min(ChunkSize, paths.Count - offset);
            var batch = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                batch.Add(paths[offset + i]);
            }

            RecycleBatch(batch);
        }
    }

    private static void RecycleBatch(List<string> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        // pFrom is a list of NUL-separated paths; the marshaller appends the
        // final terminating NUL, giving the required double-NUL termination.
        var from = string.Join('\0', batch) + '\0';

        var operation = new ShFileOpStruct
        {
            wFunc = FO_DELETE,
            pFrom = from,
            fFlags = Flags,
        };

        var result = SHFileOperation(ref operation);
        if (result != 0)
        {
            throw new DeletionException(
                $"Moving files to the Recycle Bin failed (shell error 0x{result:X}).");
        }

        if (operation.fAnyOperationsAborted != 0)
        {
            throw new DeletionException("The Recycle Bin operation was aborted.");
        }
    }

    // ssfBITBUCKET — the Recycle Bin special folder.
    private const int RecycleBinFolder = 10;

    public int Restore(IReadOnlyCollection<string> originalPaths)
    {
        ArgumentNullException.ThrowIfNull(originalPaths);
        if (originalPaths.Count == 0)
        {
            return 0;
        }

        var wanted = new HashSet<string>(originalPaths, StringComparer.OrdinalIgnoreCase);

        // Shell.Application COM is STA-affine; run it on a dedicated STA thread.
        return RunOnStaThread(() => RestoreCore(wanted));
    }

    private static int RestoreCore(HashSet<string> wanted)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
        {
            return 0;
        }

        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            dynamic recycleBin = shell!.NameSpace(RecycleBinFolder);
            if (recycleBin is null)
            {
                return 0;
            }

            // Restoring mutates the collection, so match first, then restore.
            var matches = new List<dynamic>();
            foreach (dynamic item in recycleBin.Items())
            {
                var original = TryGetOriginalPath(item);
                if (original is not null && wanted.Contains(original))
                {
                    matches.Add(item);
                }
            }

            var restored = 0;
            foreach (var item in matches)
            {
                if (InvokeRestoreVerb(item))
                {
                    restored++;
                }
            }

            return restored;
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string? TryGetOriginalPath(dynamic item)
    {
        try
        {
            // System.Recycle.DeletedFrom = the original folder (locale-independent).
            string? folder = item.ExtendedProperty("System.Recycle.DeletedFrom");
            string? name = item.Name;
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return Path.Combine(folder, name);
        }
        catch
        {
            return null;
        }
    }

    private static bool InvokeRestoreVerb(dynamic item)
    {
        try
        {
            foreach (dynamic verb in item.Verbs())
            {
                string name = ((string)verb.Name).Replace("&", string.Empty);
                if (name.Equals("Restore", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Undelete", StringComparison.OrdinalIgnoreCase))
                {
                    verb.DoIt();
                    return true;
                }
            }
        }
        catch
        {
            // Verb enumeration/invoke failed for this item — treat as not restored.
        }

        return false;
    }

    private static int RunOnStaThread(Func<int> action)
    {
        var result = 0;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw new DeletionException("Restoring from the Recycle Bin failed.", error);
        }

        return result;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }
}
