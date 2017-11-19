using System;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;

namespace mpv
{
    public partial class Form1 : Form
    {
        private const int MpvFormatString = 1;
        private IntPtr _libMpvDll;
        private IntPtr _mpvHandle;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr MpvCreate();
        private MpvCreate _mpvCreate;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvInitialize(IntPtr mpvHandle);
        private MpvInitialize _mpvInitialize;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvCommand(IntPtr mpvHandle, IntPtr strings);
        private MpvCommand _mpvCommand;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvCommandNode(IntPtr mpvHandle, IntPtr utf8Strings, IntPtr result);
        private MpvCommandNode _mpvCommandNode;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvTerminateDestroy(IntPtr mpvHandle);
        private MpvTerminateDestroy _mpvTerminateDestroy;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvSetOption(IntPtr mpvHandle, byte[] name, int format, ref long data);
        private MpvSetOption _mpvSetOption;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvSetOptionString(IntPtr mpvHandle, byte[] name, byte[] value);
        private MpvSetOptionString _mpvSetOptionString;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvGetPropertystring(IntPtr mpvHandle, byte[] name, int format, ref IntPtr data);
        private MpvGetPropertystring _mpvGetPropertyString;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvSetProperty(IntPtr mpvHandle, byte[] name, int format, ref byte[] data);
        private MpvSetProperty _mpvSetProperty;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MpvFree(IntPtr data);
        private MpvFree _mpvFree;

        public Form1()
        {
            InitializeComponent();
        }

        private object GetDllType(Type type, string name)
        {
            IntPtr address = GetProcAddress(_libMpvDll, name);
            if (address != IntPtr.Zero)
                return Marshal.GetDelegateForFunctionPointer(address, type);
            return null;
        }

        private void LoadMpvDynamic()
        {
            _libMpvDll = LoadLibrary("mpv-1.dll"); // The dll is included in the DEV builds by lachs0r: https://mpv.srsfckn.biz/
            _mpvCreate = (MpvCreate)GetDllType(typeof(MpvCreate), "mpv_create");
            _mpvInitialize = (MpvInitialize)GetDllType(typeof(MpvInitialize), "mpv_initialize");
            _mpvTerminateDestroy = (MpvTerminateDestroy)GetDllType(typeof(MpvTerminateDestroy), "mpv_terminate_destroy");
            _mpvCommand = (MpvCommand)GetDllType(typeof(MpvCommand), "mpv_command");
            _mpvCommandNode = (MpvCommandNode)GetDllType(typeof(MpvCommandNode), "mpv_command_node");
            _mpvSetOption = (MpvSetOption)GetDllType(typeof(MpvSetOption), "mpv_set_option");
            _mpvSetOptionString = (MpvSetOptionString)GetDllType(typeof(MpvSetOptionString), "mpv_set_option_string");
            _mpvGetPropertyString = (MpvGetPropertystring)GetDllType(typeof(MpvGetPropertystring), "mpv_get_property");
            _mpvSetProperty = (MpvSetProperty)GetDllType(typeof(MpvSetProperty), "mpv_set_property");
            _mpvFree = (MpvFree)GetDllType(typeof(MpvFree), "mpv_free");
        }

        public void Pause()
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            var bytes = GetUtf8Bytes("yes");
            _mpvSetProperty(_mpvHandle, GetUtf8Bytes("pause"), MpvFormatString, ref bytes);
        }

        private void Play()
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            var bytes = GetUtf8Bytes("no");
            _mpvSetProperty(_mpvHandle, GetUtf8Bytes("pause"), MpvFormatString, ref bytes);
        }

        public bool IsPaused()
        {
            if (_mpvHandle == IntPtr.Zero)
                return true;

            var lpBuffer = IntPtr.Zero;
            _mpvGetPropertyString(_mpvHandle, GetUtf8Bytes("pause"), MpvFormatString, ref lpBuffer);
            var isPaused = Marshal.PtrToStringAnsi(lpBuffer) == "yes";
            _mpvFree(lpBuffer);
            return isPaused;
        }

        public void SetTime(double value)
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            DoMpvCommand("seek", value.ToString(CultureInfo.InvariantCulture), "absolute");
        }

        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        public static IntPtr AllocateUtf8IntPtrArrayWithSentinel(string[] arr, out IntPtr[] byteArrayPointers)
        {
            int numberOfStrings = arr.Length + 1; // add extra element for extra null pointer last (sentinel)
            byteArrayPointers = new IntPtr[numberOfStrings];
            IntPtr rootPointer = Marshal.AllocCoTaskMem(IntPtr.Size * numberOfStrings);
            for (int index = 0; index < arr.Length; index++)
            {
                var bytes = GetUtf8Bytes(arr[index]);
                IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                byteArrayPointers[index] = unmanagedPointer;
            }
            Marshal.Copy(byteArrayPointers, 0, rootPointer, numberOfStrings);
            return rootPointer;
        }

        private void DoMpvCommand(params string[] args)
        {
            IntPtr[] byteArrayPointers;
            var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out byteArrayPointers);
            _mpvCommand(_mpvHandle, mainPtr);
            foreach (var ptr in byteArrayPointers)
            {
                Marshal.FreeHGlobal(ptr);
            }
            Marshal.FreeHGlobal(mainPtr);
        }

        private void buttonPlay_Click(object sender, EventArgs e)
        {
            if (_mpvHandle != IntPtr.Zero)
                _mpvTerminateDestroy(_mpvHandle);

            LoadMpvDynamic();
            if (_libMpvDll == IntPtr.Zero)
                return;

            _mpvHandle = _mpvCreate.Invoke();
            if (_mpvHandle == IntPtr.Zero)
                return;

            _mpvInitialize.Invoke(_mpvHandle);
            _mpvSetOptionString(_mpvHandle, GetUtf8Bytes("keep-open"), GetUtf8Bytes("always"));
//            _mpvSetOptionString(_mpvHandle, GetUtf8Bytes("vo"), GetUtf8Bytes("direct3d"));
            int mpvFormatInt64 = 4;
            var windowId = pictureBox1.Handle.ToInt64();
            _mpvSetOption(_mpvHandle, GetUtf8Bytes("wid"), mpvFormatInt64, ref windowId);
            DoMpvCommand("loadfile", textBoxVideoSampleFileName.Text);
        }

        private void buttonPlayPause_Click(object sender, EventArgs e)
        {
            if (IsPaused())
                Play();
            else
                Pause();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Pause();
            SetTime(0);
        }

        private void buttonLoadVideo_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = String.Empty;
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                textBoxVideoSampleFileName.Text = openFileDialog1.FileName;
                buttonPlay_Click(null, null);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_mpvHandle != IntPtr.Zero)
                _mpvTerminateDestroy(_mpvHandle);
        }

        public static byte[] ReadAllBytesShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var index = 0;
                var fileLength = fs.Length;
                if (fileLength > int.MaxValue)
                    throw new IOException("File too long");
                var count = (int)fileLength;
                var bytes = new byte[count];
                while (count > 0)
                {
                    var n = fs.Read(bytes, index, count);
                    if (n == 0)
                        throw new InvalidOperationException("End of file reached before expected");
                    index += n;
                    count -= n;
                }
                return bytes;
            }
        }

        bool _logEnabled = false;

        private void buttonScreenshotRaw_Click(object sender, EventArgs e)
        {
            var logFileName = "mpv-log.txt";
            if (!_logEnabled)
            {
                _mpvSetOptionString(_mpvHandle, GetUtf8Bytes("log-file"), GetUtf8Bytes(logFileName));
                _logEnabled = true;
            }

            if (_mpvHandle == IntPtr.Zero)
                return;

            var input = MakeMpvNodeArary("screenshot-raw", "video");
            IntPtr inputPtr = Marshal.AllocHGlobal(Marshal.SizeOf(input));
            Marshal.StructureToPtr(input, inputPtr, false);

            MpvNode ouput = new MpvNode
            {
                Format = MpvFormat.NodeMap
            };
            IntPtr ouputPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(ouput));
            Marshal.StructureToPtr(ouput, ouputPtr, false);

            var res = _mpvCommandNode(_mpvHandle, inputPtr, ouputPtr); // mpv_command_node       
            var log = string.Empty;
            if (File.Exists(logFileName))
            {
                log = Encoding.UTF8.GetString(ReadAllBytesShared(logFileName));
            }
            MessageBox.Show("Return code: " + res + Environment.NewLine + log);
        } 

        public enum MpvFormat
        {
            None = 0,
            String = 1,
            OsdString = 2,
            Flag = 3,
            Int64 = 4,
            Double = 5,
            Node = 6,
            NodeArray = 7,
            NodeMap = 8,
            ByteArray = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MpvNodeList
        {
            public int Number;
            public IntPtr Values;
            public IntPtr Keys;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MpvNode
        {
            public IntPtr Ptr; // mpv node list
            public MpvFormat Format;
        }

        private MpvNode MakeMpvNodeArary(params string[] args)
        {

            //    new MpvNode  -- see https://raw.githubusercontent.com/jaseg/python-mpv/master/mpv.py
            //    {
            //        Format = MPV_NODE_ARRAY,
            //        List = new MpvNodeList
            //        {
            //            .num = len(l),
            //            .keys = NULL,
            //            .values = struct mpv_node[len(l)] {
            //                { .format = MPV_NODE_STRING, .u.string = l[0] },
            //                { .format = MPV_NODE_STRING, .u.string = l[1] },
            //                ...
            //            }
            //        }
            //    }

            // build nodes for Values in NodeList
            var parameters = new IntPtr[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var bytes = GetUtf8Bytes(args[i]);
                IntPtr stringPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, stringPointer, bytes.Length);
                var node = new MpvNode { Format = MpvFormat.String, Ptr = stringPointer };
                IntPtr nodePtr = Marshal.AllocHGlobal(Marshal.SizeOf(node));
                Marshal.StructureToPtr(node, nodePtr, false);
                parameters[i] = nodePtr;
            }
            IntPtr rootPointer = Marshal.AllocHGlobal(IntPtr.Size * parameters.Length);
            Marshal.Copy(parameters, 0, rootPointer, args.Length);

            var nodeList = new MpvNodeList()
            {
                Number = args.Length,
                Keys = IntPtr.Zero,
                Values = rootPointer
            };
            IntPtr nodeArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf(nodeList));
            Marshal.StructureToPtr(nodeList, nodeArrayPtr, false);

            return new MpvNode
            {
                Format = MpvFormat.NodeArray,
                Ptr = nodeArrayPtr,
            };
        }

    }
}
