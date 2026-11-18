using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace RubbingBalls.Accounts
{
    internal sealed class RememberedLogin : IDisposable
    {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        public static bool Supported => true;
        private const uint Generic = 1, LocalMachine = 2;
        private const int MaxBlobBytes = 2560, NotFound = 1168;
        private readonly string target, lockFile;
        private FileStream lease;

        [StructLayout(LayoutKind.Sequential)]
        private struct Credential
        {
            public uint flags, type;
            public IntPtr target, comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
            public uint size;
            public IntPtr blob;
            public uint persist, count;
            public IntPtr attributes, alias, username;
        }

        [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("Advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool CredWriteW(ref Credential value, uint flags);
        [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredDeleteW(string target, uint type, uint flags);
        [DllImport("Advapi32.dll", ExactSpelling = true)]
        private static extern void CredFree(IntPtr data);

        public RememberedLogin(string project)
        {
            target = "RubbingBalls/Supabase/" + project;
            lockFile = Path.Combine(Application.persistentDataPath, "account-" + project + ".lock");
        }

        private bool Acquire()
        {
            if (lease != null) return true;
            try { lease = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public string Read()
        {
            if (!Acquire()) return null;
            if (CredReadW(target, Generic, 0, out var ptr))
            {
                try
                {
                    var value = Marshal.PtrToStructure<Credential>(ptr);
                    if (value.size > 0 && value.size <= MaxBlobBytes && value.blob != IntPtr.Zero)
                    {
                        var bytes = new byte[(int)value.size];
                        Marshal.Copy(value.blob, bytes, 0, bytes.Length);
                        try { return Encoding.UTF8.GetString(bytes); }
                        finally { Array.Clear(bytes, 0, bytes.Length); }
                    }
                }
                finally { CredFree(ptr); }
            }
            Dispose();
            return null;
        }

        public bool Save(string token)
        {
            if (!Acquire()) return false;
            var bytes = Encoding.UTF8.GetBytes(token);
            try
            {
                if (bytes.Length > MaxBlobBytes) return false;
                var value = new Credential { type = Generic, persist = LocalMachine, size = (uint)bytes.Length };
                try
                {
                    value.target = Marshal.StringToCoTaskMemUni(target);
                    value.blob = Marshal.AllocCoTaskMem(bytes.Length);
                    Marshal.Copy(bytes, 0, value.blob, bytes.Length);
                    return CredWriteW(ref value, 0);
                }
                finally
                {
                    if (value.blob != IntPtr.Zero)
                    {
                        Array.Clear(bytes, 0, bytes.Length);
                        Marshal.Copy(bytes, 0, value.blob, bytes.Length);
                        Marshal.FreeCoTaskMem(value.blob);
                    }
                    Marshal.FreeCoTaskMem(value.target);
                }
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }

        public bool DeleteOwned()
        {
            if (lease == null) return true;
            bool deleted = CredDeleteW(target, Generic, 0) || Marshal.GetLastWin32Error() == NotFound;
            Dispose();
            return deleted;
        }

        public void Dispose() { lease?.Dispose(); lease = null; }
#else
        public static bool Supported => false;
        public RememberedLogin(string project) { }
        public string Read() => null;
        public bool Save(string token) => false;
        public bool DeleteOwned() => true;
        public void Dispose() { }
#endif
    }
}
