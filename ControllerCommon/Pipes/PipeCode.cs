namespace ControllerCommon.Pipes
{
    public enum PipeCode
    {
        SERVER_PING = 0,                    // Sent to client during initialization
                                            // args: ...

        CLIENT_PROFILE = 1,                 // Sent to server to switch profiles
                                            // args: process id, process path

        SERVER_TOAST = 2,                   // Sent to client to display toast notification.
                                            // args: title, message

        CLIENT_CURSOR = 3,                  // Sent to server when mouse click is up
                                            // args: cursor x, cursor Y

        SERVER_SETTINGS = 6,                // Sent to client during initialization
                                            // args: ...

        CLIENT_INPUT = 7,                   // Sent to server to request a specific gamepad input
                                            // args: ...

        CLIENT_SETTINGS = 8,                // Sent to server to update settings
                                            // args: ...

        CLIENT_CONTROLLER_CONNECT = 9,      // Sent to server to share current controller details

        CLIENT_CONTROLLER_DISCONNECT = 11,  // Sent to server to warn current controller was disconnected

        CLIENT_CONSOLE = 12,                // Sent from client to client to pass parameters
                                            // args: string[] parameters

        CLIENT_PROCESS = 13,                // Sent to server each time a new process is in the foreground.

        SERVER_SENSOR = 14,                 // Sent to client to share sensor values
                                            // args: ...

        CLIENT_NAVIGATED = 15,              // Sent to server to share current navigated page
                                            // args: ...

        CLIENT_OVERLAY = 16,                // Sent to server to share current overlay status
                                            // args: ...

        SERVER_VIBRATION = 17,              // Sent to client to notify a vibration feedback arrived
                                            // args: ...

        CLIENT_MOVEMENTS = 18,              // Sent to server to inform on controller/device movements

        CLIENT_CLEARINDEX = 19,             // Sent to server to clear all hidden controllers
                                            // args: ...
    }
}
