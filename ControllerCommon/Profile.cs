using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Force.Crc32;
using Serilog.Core;
using static ControllerCommon.Utils;

namespace ControllerCommon
{
    public enum ProfileErrorCode
    {
        None = 0,
        MissingExecutable = 1,
        MissingPath = 2
    }

    [Serializable]
    public class Profile
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; }               // if true, can see through the HidHide cloak
        public bool legacy { get; set; }                    // not yet implemented
        public bool use_wrapper { get; set; }               // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;        // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;    // accelerometer multiplicator (remove me)

        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }

        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(Object sender);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(Object sender);

        public Profile(string name, string path)
        {
            this.name = name;
            this.path = path;
            this.fullpath = path;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
