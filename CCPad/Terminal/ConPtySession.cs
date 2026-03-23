using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static CCPad.Terminal.PseudoConsoleApi;

namespace CCPad.Terminal
{
    internal class ConPtySession : IDisposable
    {
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        private IntPtr _hPC = IntPtr.Zero;
        private IntPtr _hProcess = IntPtr.Zero;
        private IntPtr _hThread = IntPtr.Zero;
        private IntPtr _hPipeInWrite = IntPtr.Zero;
        private IntPtr _hPipeOutRead = IntPtr.Zero;
        private IntPtr _attributeList = IntPtr.Zero;
        private volatile bool _disposed;

        public event Action<byte[]>? OutputReceived;
        public event Action? Exited;

        public int Cols { get; private set; }
        public int Rows { get; private set; }

        public static ConPtySession Start(string command, int cols, int rows, string? workingDir = null)
        {
            var session = new ConPtySession { Cols = cols, Rows = rows };
            session.Launch(command, cols, rows, workingDir);
            return session;
        }

        private void Launch(string command, int cols, int rows, string? workingDir = null)
        {
            if (!CreatePipe(out var hPipeInRead, out _hPipeInWrite, IntPtr.Zero, 0))
                throw new InvalidOperationException($"CreatePipe(stdin) failed: {Marshal.GetLastWin32Error()}");

            if (!CreatePipe(out _hPipeOutRead, out var hPipeOutWrite, IntPtr.Zero, 0))
                throw new InvalidOperationException($"CreatePipe(stdout) failed: {Marshal.GetLastWin32Error()}");

            int hr = CreatePseudoConsole(
                new COORD { X = (short)cols, Y = (short)rows },
                hPipeInRead, hPipeOutWrite, 0, out _hPC);

            if (hr != 0)
                throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");

            // These ends are now owned by the PTY
            CloseHandle(hPipeInRead);
            CloseHandle(hPipeOutWrite);

            var siEx = BuildStartupInfo();

            string dir = workingDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            IntPtr envBlock = BuildUtf8EnvironmentBlock();
            try
            {
            bool ok = CreateProcess(
                null, command,
                IntPtr.Zero, IntPtr.Zero, false,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                envBlock, dir,
                ref siEx, out var pi);

            if (!ok)
                throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");

            _hProcess = pi.hProcess;
            _hThread = pi.hThread;
            }
            finally
            {
                Marshal.FreeHGlobal(envBlock);
            }

            Task.Factory.StartNew(ReadOutput, TaskCreationOptions.LongRunning);
        }

        private STARTUPINFOEX BuildStartupInfo()
        {
            IntPtr size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

            _attributeList = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref size))
                throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

            // Pass the HPCON handle DIRECTLY — not a pointer to it.
            // This matches the official Microsoft MiniTerm sample.
            if (!UpdateProcThreadAttribute(
                    _attributeList, 0,
                    PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    _hPC, (IntPtr)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero))
                throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

            return new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() },
                lpAttributeList = _attributeList
            };
        }

        private void ReadOutput()
        {
            var buffer = new byte[4096];
            try
            {
                while (!_disposed)
                {
                    bool ok = ReadFile(_hPipeOutRead, buffer, buffer.Length, out int n, IntPtr.Zero);
                    if (!ok || n == 0) break;

                    var chunk = new byte[n];
                    Array.Copy(buffer, chunk, n);
                    OutputReceived?.Invoke(chunk);
                }
            }
            catch { }
            finally
            {
                if (!_disposed)
                    Exited?.Invoke();
            }
        }

        public void WriteInput(string text)
        {
            if (_hPipeInWrite == IntPtr.Zero || _disposed) return;
            var bytes = Encoding.UTF8.GetBytes(text);
            WriteFile(_hPipeInWrite, bytes, bytes.Length, out _, IntPtr.Zero);
        }

        public void Resize(int cols, int rows)
        {
            if (_hPC == IntPtr.Zero || _disposed) return;
            Cols = cols;
            Rows = rows;
            ResizePseudoConsole(_hPC, new COORD { X = (short)cols, Y = (short)rows });
        }

        /// <summary>
        /// Build a Unicode environment block that inherits the current process's
        /// environment and forces UTF-8 locale variables.
        /// </summary>
        private static IntPtr BuildUtf8EnvironmentBlock()
        {
            var env = Environment.GetEnvironmentVariables();
            // Force UTF-8 locale for child processes (git, node, bash, etc.)
            env["LANG"] = "en_US.UTF-8";
            env["LC_ALL"] = "en_US.UTF-8";
            env["PYTHONUTF8"] = "1";
            env["PYTHONIOENCODING"] = "utf-8";

            // Environment block: sorted KEY=VALUE\0 pairs, terminated by extra \0
            var keys = new string[env.Count];
            env.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            foreach (var key in keys)
            {
                sb.Append(key).Append('=').Append(env[key]).Append('\0');
            }
            sb.Append('\0'); // double-null terminator

            var bytes = Encoding.Unicode.GetBytes(sb.ToString());
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_hPC != IntPtr.Zero) { ClosePseudoConsole(_hPC); _hPC = IntPtr.Zero; }
            if (_hProcess != IntPtr.Zero) { CloseHandle(_hProcess); _hProcess = IntPtr.Zero; }
            if (_hThread != IntPtr.Zero) { CloseHandle(_hThread); _hThread = IntPtr.Zero; }
            if (_hPipeInWrite != IntPtr.Zero) { CloseHandle(_hPipeInWrite); _hPipeInWrite = IntPtr.Zero; }
            if (_hPipeOutRead != IntPtr.Zero) { CloseHandle(_hPipeOutRead); _hPipeOutRead = IntPtr.Zero; }
            if (_attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(_attributeList);
                Marshal.FreeHGlobal(_attributeList);
                _attributeList = IntPtr.Zero;
            }
        }
    }
}
