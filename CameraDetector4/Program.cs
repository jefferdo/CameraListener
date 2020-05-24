﻿using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using Windows.UI.Notifications;

namespace CameraDetector4
{
    internal class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static void Main(string[] args)
        {
            DesktopNotificationManagerCompat.RegisterAumidAndComServer<MyNotificationActivator>("WindowsNotifications.CameraDetector");
            DesktopNotificationManagerCompat.RegisterActivator<MyNotificationActivator>();


            var ids = GetCameraIds();
            var machineName = System.Environment.MachineName;
            var ipAddress = getIP();
            var cameraNames = new List<string>();
            var url = args.Length > 0 ? args[0] : "";
            SendNotification();
            List<KeyValuePair<string, Process>> procs = Win32Processes.GetProcessesLockingFile("svchost,atmgr", ids);//"svchost,zoom"
            foreach (var proc in procs)
            {
                Console.WriteLine($"{proc.Key},{proc.Value.ProcessName}");
                cameraNames.Add(proc.Key);
            }

            if (cameraNames.Count > 0)
            {
                try
                {
                    SendNotification();
                }
                catch (Exception ex) { }

                object data = new { MachineName = machineName, IPAddress = ipAddress, CameraNames = string.Join(",", cameraNames), TimeStamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") };

                try
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        using (WebClient wc = new WebClient())
                        {
                            var postData = new JavaScriptSerializer().Serialize(data);
                            wc.UploadData(new Uri(url), "POST", Encoding.ASCII.GetBytes(postData));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                    log.Error(new JavaScriptSerializer().Serialize(ex));
                    Environment.Exit(1);
                }
                log.Info($"Active Camera(s) Found - {new JavaScriptSerializer().Serialize(data)}");
            }
            Environment.Exit(0);
        }
        #region GetCameraIds
        public static byte[] ObjectToByteArray(Object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        internal static string getIP()
        {
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
            return localIP;
        }

        internal static List<string[]> GetCameraIds()
        {
            using (PowerShell PowerShellInstance = PowerShell.Create())
            {
                // this script has a sleep in it to simulate a long running script
                //PowerShellInstance.AddScript("start-sleep -s 7; get-service");
                PowerShellInstance.AddScript("$a = (Get-PnpDevice -Class 'camera' -Status ok | Get-PnpDeviceProperty -KeyName {\"DEVPKEY_Device_PDOName\", \"DEVPKEY_Device_DeviceDesc\"}).data -join ','");
                PowerShellInstance.AddScript("$b = (Get-PnpDevice -Class 'image' -Status ok | Get-PnpDeviceProperty -KeyName {\"DEVPKEY_Device_PDOName\", \"DEVPKEY_Device_DeviceDesc\"}).data -join ','");
                PowerShellInstance.AddScript("@($a, $b)");

                // invoke execution on the pipeline (collecting output)
                Collection<PSObject> PSOutput = PowerShellInstance.Invoke();
                var ids = new List<string[]>();
                // loop through each output object item
                foreach (PSObject outputItem in PSOutput)
                {
                    // if null object was dumped to the pipeline during the script then a null
                    // object may be present here. check for null to prevent potential NRE.
                    if (outputItem != null && outputItem.BaseObject.ToString().Length != 0)
                    {
                        //TODO: do something with the output item
                        ids.Add(outputItem.BaseObject.ToString().Split(','));
                    }
                }

                return ids;
            }
        }

        internal class Win32API
        {
            [DllImport("ntdll.dll")]
            public static extern int NtQueryObject(IntPtr ObjectHandle, int
                ObjectInformationClass, IntPtr ObjectInformation, int ObjectInformationLength,
                ref int returnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

            [DllImport("ntdll.dll")]
            public static extern uint NtQuerySystemInformation(int
                SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength,
                ref int returnLength);

            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll")]
            public static extern int CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
               ushort hSourceHandle, IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
               uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentProcess();

            public enum ObjectInformationClass : int
            {
                ObjectBasicInformation = 0,
                ObjectNameInformation = 1,
                ObjectTypeInformation = 2,
                ObjectAllTypesInformation = 3,
                ObjectHandleInformation = 4
            }

            [Flags]
            public enum ProcessAccessFlags : uint
            {
                All = 0x001F0FFF,
                Terminate = 0x00000001,
                CreateThread = 0x00000002,
                VMOperation = 0x00000008,
                VMRead = 0x00000010,
                VMWrite = 0x00000020,
                DupHandle = 0x00000040,
                SetInformation = 0x00000200,
                QueryInformation = 0x00000400,
                Synchronize = 0x00100000
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct OBJECT_BASIC_INFORMATION
            { // Information Class 0
                public int Attributes;
                public int GrantedAccess;
                public int HandleCount;
                public int PointerCount;
                public int PagedPoolUsage;
                public int NonPagedPoolUsage;
                public int Reserved1;
                public int Reserved2;
                public int Reserved3;
                public int NameInformationLength;
                public int TypeInformationLength;
                public int SecurityDescriptorLength;
                public System.Runtime.InteropServices.ComTypes.FILETIME CreateTime;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct OBJECT_TYPE_INFORMATION
            { // Information Class 2
                public UNICODE_STRING Name;
                public int ObjectCount;
                public int HandleCount;
                public int Reserved1;
                public int Reserved2;
                public int Reserved3;
                public int Reserved4;
                public int PeakObjectCount;
                public int PeakHandleCount;
                public int Reserved5;
                public int Reserved6;
                public int Reserved7;
                public int Reserved8;
                public int InvalidAttributes;
                public GENERIC_MAPPING GenericMapping;
                public int ValidAccess;
                public byte Unknown;
                public byte MaintainHandleDatabase;
                public int PoolType;
                public int PagedPoolUsage;
                public int NonPagedPoolUsage;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct OBJECT_NAME_INFORMATION
            { // Information Class 1
                public UNICODE_STRING Name;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct UNICODE_STRING
            {
                public ushort Length;
                public ushort MaximumLength;
                public IntPtr Buffer;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct GENERIC_MAPPING
            {
                public int GenericRead;
                public int GenericWrite;
                public int GenericExecute;
                public int GenericAll;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SYSTEM_HANDLE_INFORMATION
            { // Information Class 16
                public int ProcessID;
                public byte ObjectTypeNumber;
                public byte Flags; // 0x01 = PROTECT_FROM_CLOSE, 0x02 = INHERIT
                public ushort Handle;
                public int Object_Pointer;
                public UInt32 GrantedAccess;
            }

            public const int MAX_PATH = 260;
            public const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
            public const int DUPLICATE_SAME_ACCESS = 0x2;
        }

        public class Win32Processes
        {
            /// <summary>
            /// Return a list of processes that hold on the given file.
            /// </summary>
            public static List<KeyValuePair<string, Process>> GetProcessesLockingFile(string processName, List<string[]> filePaths)
            {
                var procs = new List<KeyValuePair<string, Process>>();
                var processes = new List<Process>();
                if (!string.IsNullOrEmpty(processName))
                {
                    foreach (var name in processName.Split(','))
                    {
                        processes.AddRange(Process.GetProcessesByName(name).ToList());
                    }
                }
                else
                {
                    processes = Process.GetProcesses().ToList();
                }
                foreach (var process in processes)
                {
                    var files = GetFilesLockedBy(process);
                    foreach (var filep in filePaths)
                    {
                        if (files.Exists(f => f.Equals(filep[0], StringComparison.InvariantCultureIgnoreCase)))
                        {
                            Console.WriteLine("=======START========");
                            Console.WriteLine($"FOUND MATCH - {process.ProcessName} - {process.Id}");
                            procs.Add(new KeyValuePair<string, Process>(filep[1], process));
                            Console.WriteLine("========END========");
                        }
                    }
                }
                return procs;
            }

            /// <summary>
            /// Return a list of file locks held by the process.
            /// </summary>
            public static List<string> GetFilesLockedBy(Process process)
            {
                var outp = new List<string>();

                ThreadStart ts = delegate
                {
                    try
                    {
                        outp = UnsafeGetFilesLockedBy(process);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                };

                try
                {
                    var t = new Thread(ts);
                    t.Start();
                    if (!t.Join(250))
                    {
                        try
                        {
                            t.Abort();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                return outp;
            }

            private static List<string> UnsafeGetFilesLockedBy(Process process)
            {
                var handles = GetHandles(process);
                var files = new List<string>();

                for (int i = 0; i < handles.Count; i++)
                {
                    var handle = handles[i];
                    var file = GetFilePath(handle, process);
                    //Console.WriteLine(file);
                    if (file != null) files.Add(file);
                }

                //foreach (var handle in handles)
                //{
                //    var file = GetFilePath(handle, process);
                //    Console.WriteLine(file);
                //    if (file != null) files.Add(file);
                //}

                return files;
                /*try
                {
                    var handles = GetHandles(process);
                    var files = new List<string>();

                    foreach (var handle in handles)
                    {
                        var file = GetFilePath(handle, process);
                        if (file != null) files.Add(file);
                    }

                    return files;
                }
                catch
                {
                    return new List<string>();
                }*/
            }

            private const int CNST_SYSTEM_HANDLE_INFORMATION = 16;
            private const uint STATUS_INFO_LENGTH_MISMATCH = 0xc0000004;

            private static string GetFilePath(Win32API.SYSTEM_HANDLE_INFORMATION sYSTEM_HANDLE_INFORMATION, Process process)
            {
                IntPtr m_ipProcessHwnd = Win32API.OpenProcess(Win32API.ProcessAccessFlags.All, false, process.Id);
                IntPtr ipHandle = IntPtr.Zero;
                var objBasic = new Win32API.OBJECT_BASIC_INFORMATION();
                IntPtr ipBasic = IntPtr.Zero;
                var objObjectType = new Win32API.OBJECT_TYPE_INFORMATION();
                IntPtr ipObjectType = IntPtr.Zero;
                var objObjectName = new Win32API.OBJECT_NAME_INFORMATION();
                IntPtr ipObjectName = IntPtr.Zero;
                string strObjectTypeName = "";
                string strObjectName = "";
                int nLength = 0;
                int nReturn = 0;
                IntPtr ipTemp = IntPtr.Zero;

                if (!Win32API.DuplicateHandle(m_ipProcessHwnd, sYSTEM_HANDLE_INFORMATION.Handle, Win32API.GetCurrentProcess(), out ipHandle, 0, false, Win32API.DUPLICATE_SAME_ACCESS))
                    return null;

                ipBasic = Marshal.AllocHGlobal(Marshal.SizeOf(objBasic));
                Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectBasicInformation, ipBasic, Marshal.SizeOf(objBasic), ref nLength);
                objBasic = (Win32API.OBJECT_BASIC_INFORMATION)Marshal.PtrToStructure(ipBasic, objBasic.GetType());
                Marshal.FreeHGlobal(ipBasic);

                ipObjectType = Marshal.AllocHGlobal(objBasic.TypeInformationLength);
                nLength = objBasic.TypeInformationLength;
                while ((uint)(nReturn = Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectTypeInformation, ipObjectType, nLength, ref nLength)) == Win32API.STATUS_INFO_LENGTH_MISMATCH)
                {
                    Marshal.FreeHGlobal(ipObjectType);
                    ipObjectType = Marshal.AllocHGlobal(nLength);
                }

                objObjectType = (Win32API.OBJECT_TYPE_INFORMATION)Marshal.PtrToStructure(ipObjectType, objObjectType.GetType());
                if (Is64Bits())
                {
                    ipTemp = new IntPtr(Convert.ToInt64(objObjectType.Name.Buffer.ToString(), 10) >> 32);
                }
                else
                {
                    ipTemp = objObjectType.Name.Buffer;
                }

                strObjectTypeName = Marshal.PtrToStringUni(ipTemp, objObjectType.Name.Length >> 1);
                Marshal.FreeHGlobal(ipObjectType);
                if (strObjectTypeName != "File")
                    return null;

                nLength = objBasic.NameInformationLength;

                ipObjectName = Marshal.AllocHGlobal(nLength);
                while ((uint)(nReturn = Win32API.NtQueryObject(ipHandle, (int)Win32API.ObjectInformationClass.ObjectNameInformation, ipObjectName, nLength, ref nLength)) == Win32API.STATUS_INFO_LENGTH_MISMATCH)
                {
                    Marshal.FreeHGlobal(ipObjectName);
                    ipObjectName = Marshal.AllocHGlobal(nLength);
                }
                objObjectName = (Win32API.OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(ipObjectName, objObjectName.GetType());
                //
                if (Is64Bits())
                {
                    ipTemp = new IntPtr(Convert.ToInt64(objObjectName.Name.Buffer.ToString(), 10) >> 32);
                }
                else
                {
                    ipTemp = objObjectName.Name.Buffer;
                }

                if (ipTemp != IntPtr.Zero)
                {
                    if (nLength < 0)
                    {
                        return null;
                    }
                    byte[] baTemp = new byte[nLength];
                    try
                    {
                        Marshal.Copy(ipTemp, baTemp, 0, nLength);

                        strObjectName = Marshal.PtrToStringUni(Is64Bits() ? new IntPtr(ipTemp.ToInt64()) : new IntPtr(ipTemp.ToInt32()));
                    }
                    catch (Exception)    //AccessViolationException)
                    {
                        return null;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ipObjectName);
                        Win32API.CloseHandle(ipHandle);
                    }
                }

                string path = GetRegularFileNameFromDevice(strObjectName);
                try
                {
                    return path;
                }
                catch
                {
                    return null;
                }
            }

            private static string GetRegularFileNameFromDevice(string strRawName)
            {
                string strFileName = strRawName;
                foreach (string strDrivePath in Environment.GetLogicalDrives())
                {
                    StringBuilder sbTargetPath = new StringBuilder(Win32API.MAX_PATH);
                    if (Win32API.QueryDosDevice(strDrivePath.Substring(0, 2), sbTargetPath, Win32API.MAX_PATH) == 0)
                    {
                        return strRawName;
                    }
                    string strTargetPath = sbTargetPath.ToString();
                    if (strFileName.StartsWith(strTargetPath))
                    {
                        strFileName = strFileName.Replace(strTargetPath, strDrivePath.Substring(0, 2));
                        break;
                    }
                }
                return strFileName;
            }

            private static List<Win32API.SYSTEM_HANDLE_INFORMATION> GetHandles(Process process)
            {
                uint nStatus;
                int nHandleInfoSize = 0x10000;
                IntPtr ipHandlePointer = Marshal.AllocHGlobal(nHandleInfoSize);
                int nLength = 0;
                IntPtr ipHandle = IntPtr.Zero;

                while ((nStatus = Win32API.NtQuerySystemInformation(CNST_SYSTEM_HANDLE_INFORMATION, ipHandlePointer, nHandleInfoSize, ref nLength)) == STATUS_INFO_LENGTH_MISMATCH)
                {
                    nHandleInfoSize = nLength;
                    Marshal.FreeHGlobal(ipHandlePointer);
                    ipHandlePointer = Marshal.AllocHGlobal(nLength);
                }

                byte[] baTemp = new byte[nLength];
                Marshal.Copy(ipHandlePointer, baTemp, 0, nLength);

                long lHandleCount = 0;
                if (Is64Bits())
                {
                    lHandleCount = Marshal.ReadInt64(ipHandlePointer);
                    ipHandle = new IntPtr(ipHandlePointer.ToInt64() + 8);
                }
                else
                {
                    lHandleCount = Marshal.ReadInt32(ipHandlePointer);
                    ipHandle = new IntPtr(ipHandlePointer.ToInt32() + 4);
                }

                Win32API.SYSTEM_HANDLE_INFORMATION shHandle;
                List<Win32API.SYSTEM_HANDLE_INFORMATION> lstHandles = new List<Win32API.SYSTEM_HANDLE_INFORMATION>();

                for (long lIndex = 0; lIndex < lHandleCount; lIndex++)
                {
                    shHandle = new Win32API.SYSTEM_HANDLE_INFORMATION();
                    if (Is64Bits())
                    {
                        shHandle = (Win32API.SYSTEM_HANDLE_INFORMATION)Marshal.PtrToStructure(ipHandle, shHandle.GetType());
                        ipHandle = new IntPtr(ipHandle.ToInt64() + Marshal.SizeOf(shHandle) + 8);
                    }
                    else
                    {
                        ipHandle = new IntPtr(ipHandle.ToInt64() + Marshal.SizeOf(shHandle));
                        shHandle = (Win32API.SYSTEM_HANDLE_INFORMATION)Marshal.PtrToStructure(ipHandle, shHandle.GetType());
                    }
                    if (shHandle.ProcessID != process.Id) continue;
                    lstHandles.Add(shHandle);
                }
                return lstHandles;
            }

            private static bool Is64Bits()
            {
                return Marshal.SizeOf(typeof(IntPtr)) == 8 ? true : false;
            }
        }

        #endregion

        private static void SendNotification()
        {
            string title = "Your Camera is active";
            string content = "If the camera got activated without your actions, please check for malware.";
            //string image = "https://picsum.photos/364/202?image=883";
            int conversationId = 5;

            // Construct the toast content
            ToastContent toastContent = new ToastContent()
            {
                // Arguments when the user taps body of toast
                Launch = new QueryString()
                {
                    { "action", "viewConversation" },
                    { "conversationId", conversationId.ToString() }

                }.ToString(),

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },

                            new AdaptiveText()
                            {
                                Text = content
                            }

                            //,new AdaptiveImage()
                            //{
                            //    // Non-Desktop Bridge apps cannot use HTTP images, so
                            //    // we download and reference the image locally
                            //    Source = await DownloadImageToDisk(image)
                            //}
                        }

                        //,AppLogoOverride = new ToastGenericAppLogo()
                        //{
                        //    Source = await DownloadImageToDisk("https://unsplash.it/64?image=1005"),
                        //    HintCrop = ToastGenericAppLogoCrop.Circle
                        //}
                    }
                }

                //Actions = new ToastActionsCustom()
                //{
                //    Inputs =
                //    {
                //        new ToastTextBox("tbReply")
                //        {
                //            PlaceholderContent = "Type a response"
                //        }
                //    },

                //    Buttons =
                //    {
                //        // Note that there's no reason to specify background activation, since our COM
                //        // activator decides whether to process in background or launch foreground window
                //        new ToastButton("Reply", new QueryString()
                //        {
                //            { "action", "reply" },
                //            { "conversationId", conversationId.ToString() }

                //        }.ToString()),

                //        new ToastButton("Like", new QueryString()
                //        {
                //            { "action", "like" },
                //            { "conversationId", conversationId.ToString() }

                //        }.ToString())

                //        //new ToastButton("View", new QueryString()
                //        //{
                //        //    { "action", "viewImage" },
                //        //    { "imageUrl", image }

                //        //}.ToString())
                //    }
                //}
            };

            // Make sure to use Windows.Data.Xml.Dom
            var doc = new XmlDocument();
            doc.LoadXml(toastContent.GetContent());
            Windows.Data.Xml.Dom.XmlDocument x = new Windows.Data.Xml.Dom.XmlDocument();
            x.LoadXml(toastContent.GetContent());
            // And create the toast notification
            var toast = new ToastNotification(x);

            // And then show it
            DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }
    }
}