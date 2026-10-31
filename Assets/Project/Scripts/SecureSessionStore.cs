using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class SecureSessionStore
{
    private const string FileName = "nakama-session.bin";

    public static bool TrySave(string token)
    {
        if (string.IsNullOrEmpty(token) || !IsSupported)
            return false;

        try
        {
            var protectedBytes = Protect(System.Text.Encoding.UTF8.GetBytes(token));
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
        if (!IsSupported || !File.Exists(GetPath()))
            return false;

        try
        {
            var bytes = Unprotect(File.ReadAllBytes(GetPath()));
            token = System.Text.Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SecureSessionStore] Load failed; secure session will be discarded: {exception.Message}");
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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static byte[] Protect(byte[] value) => CryptProtectData(value, "Railbound Nakama session");
    private static byte[] Unprotect(byte[] value) => CryptUnprotectData(value);

    private static byte[] CryptProtectData(byte[] value, string description)
    {
        using var input = new DataBlob(value);
        using var output = new DataBlob();
        var descriptionPointer = Marshal.StringToHGlobalUni(description);
        
        try
        {
            if (!CryptProtectData(ref input, descriptionPointer, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
                throw new InvalidOperationException($"CryptProtectData failed with error {Marshal.GetLastWin32Error()}.");
            return output.ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(descriptionPointer);
        }
    }

    private static byte[] CryptUnprotectData(byte[] value)
    {
        using var input = new DataBlob(value);
        using var output = new DataBlob();
        
        if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref output))
            throw new InvalidOperationException($"CryptUnprotectData failed with error {Marshal.GetLastWin32Error()}.");
            
        return output.ToArray();
    }

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

    [DllImport("Crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

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
            {
                try { Marshal.Copy(value, 0, pbData, cbData); }
                catch { Dispose(); throw; }
            }
        }

        public byte[] ToArray()
        {
            if (cbData <= 0 || pbData == IntPtr.Zero) return Array.Empty<byte>();
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
    // Graceful fallback for non-Windows platforms
    private static byte[] Protect(byte[] value) => value;
    private static byte[] Unprotect(byte[] value) => value;
#endif
}
