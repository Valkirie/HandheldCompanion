using System;
using System.Runtime.Serialization;

namespace hidapi
{
    public class HidDeviceInvalidException : Exception
    {
        public HidDeviceInvalidException() : base(
            "The device is invalid. Cannot perform actions on invalid device. Make sure it was opened properly and is still available.")
        {
        }

        public HidDeviceInvalidException(string message) : base(message)
        {
        }

        public HidDeviceInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HidDeviceInvalidException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}