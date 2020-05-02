using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CameraListener
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Get the current process.
            Process currentProcess = Process.GetCurrentProcess();

            // Get all processes running on the local computer.
            Process[] localAll = Process.GetProcesses();

            // Get all instances of Notepad running on the local computer.
            // This will return an empty array if notepad isn't running.
            Process[] localByName = Process.GetProcessesByName("notepad");

            // Get a process on the local computer, using the process id.
            // This will throw an exception if there is no such process.
            Process localById = Process.GetProcessById(1234);

            // Get processes running on a remote computer. Note that this
            // and all the following calls will timeout and throw an exception
            // if "myComputer" and 169.0.0.0 do not exist on your local network.

            // Get all processes on a remote computer.
            Process[] remoteAll = Process.GetProcesses("myComputer");

            // Get all instances of Notepad running on the specific computer, using machine name.
            Process[] remoteByName = Process.GetProcessesByName("notepad", "myComputer");

            // Get all instances of Notepad running on the specific computer, using IP address.
            Process[] ipByName = Process.GetProcessesByName("notepad", "169.0.0.0");

        }

        protected override void OnStop()
        {
        }
    }
}
