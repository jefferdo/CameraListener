using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CameraDetector4
{
    // The GUID CLSID must be unique to your app. Create a new GUID if copying this code.
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("A8876357-C572-48AF-BAA7-F6AA7CEB1661"), ComVisible(true)]
    public class MyNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string invokedArgs, NotificationUserInput userInput, string appUserModelId)
        {
            // TODO: Handle activation
        }
    }
}
