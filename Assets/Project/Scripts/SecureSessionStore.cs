using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class SecureSessionStore
{
    private const string FileName = "nakama-session.bin";

    public static bool TrySave(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            byte[] protectedBytes;

            if (IsSupported)
            {
                protectedBytes = Protect(System.Text.Encoding.UTF8.GetBytes(token));
            }
            else
            {
                protectedBytes = System.Text.Encoding.UTF8.GetBytes(token);
                ApplyFallbackFilePermissions(GetPath());
            }

            File.WriteAllBytes(GetPath(), protectedBytes);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SecureSessionStore] Save failed: {exception.Message}");
            return false;
        }
    }

    public static bool TryLoad(out string token)
    {
        token = null;

        if (!File.Exists(GetPath()))
            return false;

        try
        {
            byte[] bytes = File.ReadAllBytes(GetPath());

            if (IsSupported)
            {
                bytes = Unprotect(bytes);
            }

            token = System.Text.Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(token))
            {
                Delete();
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SecureSessionStore] Load failed; session will be discarded: {exception.Message}");
            Delete();
            return false;
        }
    }

    public static void Delete()
    {
        try
        {
            var path = GetPath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SecureSessionStore] Delete failed: {exception.Message}");
        }
    }

    private static bool IsSupported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        true;
#else
        false;
#endif

    private static string GetPath() => Path.Combine(Application.persistentDataPath, FileName);

    private static void ApplyFallbackFilePermissions(string path)
    {
        try
        {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists)
                {
                    UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    File.SetUnixFileMode(path, mode);
                }
            }
#endif
        }
        catch
        {
            // Ignore permission issues; the session is still restored in a fallback path.
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static byte[] Protect(byte[] value)
    {
        return CryptProtectData(value, "Railbound Nakama session");
    }

    private static byte[] Unprotect(byte[] value)
    {
        return CryptUnprotectData(value);
    }

    private static byte[] CryptProtectData(byte[] value, string description)
    {
        var input = new DataBlob(value);
        var output = new DataBlob();
        var descriptionPointer = Marshal.StringToHGlobalUni(description);
        try
        {
            if (!CryptProtectData(ref input, descriptionPointer, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
                throw new InvalidOperationException($"CryptProtectData failed with error {Marshal.GetLastWin32Error()}.");
            return output.ToArray();
        }
        finally
        {
            input.Dispose();
            output.Dispose();
            Marshal.FreeHGlobal(descriptionPointer);
        }
    }

    private static byte[] CryptUnprotectData(byte[] value)
    {
        var input = new DataBlob(value);
        var output = new DataBlob();
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
                throw new InvalidOperationException($"CryptUnprotectData failed with error {Marshal.GetLastWin32Error()}.");
            return output.ToArray();
        }
        finally
        {
            input.Dispose();
            output.Dispose();
        }
    }

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        ref DataBlob dataOut);

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr entropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        ref DataBlob dataOut);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob : IDisposable
    {
        public int cbData;
        public IntPtr pbData;

        public DataBlob(byte[] value)
        {
            cbData = value?.Length ?? 0;
            pbData = cbData > 0 ? Marshal.AllocHGlobal(cbData) : IntPtr.Zero;
            if (cbData > 0)
                Marshal.Copy(value, 0, pbData, cbData);
        }

        public byte[] ToArray()
        {
            if (cbData <= 0 || pbData == IntPtr.Zero)
                return Array.Empty<byte>();
            var value = new byte[cbData];
            Marshal.Copy(pbData, value, 0, cbData);
            return value;
        }

        public void Dispose()
        {
            if (pbData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pbData);
                pbData = IntPtr.Zero;
                cbData = 0;
            }
        }
    }
#else
    private static byte[] Protect(byte[] value)
    {
        throw new PlatformNotSupportedException("OS-protected session storage is only enabled for Windows PC builds.");
    }

    private static byte[] Unprotect(byte[] value)
    {
        throw new PlatformNotSupportedException("OS-protected session storage is only enabled for Windows PC builds.");
    }
#endif
}
