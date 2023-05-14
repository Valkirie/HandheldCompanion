using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace PowerCfg
{
    public struct QueryPossibleSetting
    {
        public string FriendlyName;
    }

    public struct QuerySetting
    {
        public string SettingGuid;
        public string SettingFriendlyName;
        public string SettingAlias;
        public QueryPossibleSetting[] PossibleSettings;
        public int MinimumPossibleSetting;
        public int MaximumPossibleSetting;
        public int PossibleSettingIncrement;
        public string PossibleSettingUnit;
        public int ACSetting;
        public int DCSetting;
    }

    public struct QuerySubGroup
    {
        public string SubGroupGuid;
        public string SubGroupFriendlyName;
        public string SubGroupAlias;
        public List<QuerySetting> Settings;
    }

    public struct QueryValue
    {
        public string PowerSchemeGuid;
        public string PowerSchemeFriendlyName;
        public List<QuerySubGroup> SubGroups;
    }

    public enum ValueIndex
    {
        AC,
        DC
    }

    public class PowerCfgBroker
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsUserAnAdmin();

        public static string[] GetAttributes(string SubGroup, string Settings)
        {
            List<string> output = new List<string>();
            List<string> error = new List<string>();

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"-attributes {SubGroup} {Settings}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.Verb = "runas";
            p.Start();
            while (!p.StandardOutput.EndOfStream)
            {
                string line = p.StandardOutput.ReadLine();
                output.Add(line.Trim());
            }
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                error.Add(line);
            }
            bool exited = p.WaitForExit(5000);
            if (!exited) throw new System.TimeoutException($"Timeout reading {SubGroup} {Settings} attributes: {String.Join(",", error.ToArray())}");
            if (p.ExitCode != 0) throw new System.ArgumentException($"Could not read {SubGroup} {Settings} attribute: {String.Join(",", error.ToArray())}");
            return output.ToArray();
        }

        public static void SetAttribute(string SubGroup, string Settings, string Attribute, bool Enabled)
        {
            if (!IsUserAnAdmin()) throw new System.UnauthorizedAccessException("User must be an administrator to modify attributes");
            List<string> error = new List<string>();

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"-attributes {SubGroup} {Settings} {(Enabled ? '+' : '-')}{Attribute}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.Verb = "runas";
            p.Start();
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                error.Add(line);
            }
            bool exited = p.WaitForExit(5000);
            if (!exited) throw new System.TimeoutException($"Timeout setting {SubGroup} {Settings} attributes: {String.Join(",", error.ToArray())}");
            if (p.ExitCode != 0) throw new System.ArgumentException($"Could not set {SubGroup} {Settings} attribute: {String.Join(",", error.ToArray())}");
        }

        public static QueryValue? Query(string Scheme, string SubGroup, string Settings)
        {
            List<string> output = new List<string>();
            List<string> error = new List<string>();

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"-q {Scheme} {SubGroup} {Settings}".Trim(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.Verb = "runas";
            p.Start();

            QueryValue? qv = null;
            QuerySubGroup? qsg = null;
            QuerySetting? qs = null;
            string lastGuidType = "";
            QueryValue queryValue;
            QuerySubGroup querySubGroup;
            QuerySetting querySetting;
            int possibleSettingIndex = 0;

            Regex queryValueRegex = new Regex("(.+)\\s\\((.+)\\)");

            while (!p.StandardOutput.EndOfStream)
            {
                string line = p.StandardOutput.ReadLine();
                if (line.Trim() == "") continue;
                string[] parsed = line.Split(':');
                string key = parsed[0].Trim();
                string value = parsed[1].Trim();
                Match m;

                switch (key)
                {
                    case "Power Scheme GUID":
                        if (qv.HasValue) throw new System.InvalidOperationException("Duplicate Power Scheme GUID found");
                        m = queryValueRegex.Match(value);
                        queryValue = new QueryValue();
                        queryValue.SubGroups = new List<QuerySubGroup>();
                        queryValue.PowerSchemeGuid = m.Groups[1].Value.Trim();
                        queryValue.PowerSchemeFriendlyName = m.Groups[2].Value.Trim();
                        qv = queryValue;
                        lastGuidType = "PowerScheme";
                        break;
                    case "Subgroup GUID":
                        if (!qv.HasValue) throw new System.InvalidOperationException("No Power Scheme defined");
                        if (qsg.HasValue)
                        {
                            if (qs.HasValue)
                            {
                                querySubGroup = qsg.Value;
                                querySubGroup.Settings.Add(qs.Value);
                                qsg = querySubGroup;
                                qs = null;
                            }
                            queryValue = qv.Value;
                            queryValue.SubGroups.Add(qsg.Value);
                            qv = queryValue;
                        }
                        m = queryValueRegex.Match(value);
                        querySubGroup = new QuerySubGroup();
                        querySubGroup.Settings = new List<QuerySetting>();
                        querySubGroup.SubGroupGuid = m.Groups[1].Value.Trim();
                        querySubGroup.SubGroupFriendlyName = m.Groups[2].Value.Trim();
                        qsg = querySubGroup;
                        lastGuidType = "SubGroup";
                        break;
                    case "Power Setting GUID":
                        if (!qv.HasValue) throw new System.InvalidOperationException("No Power Scheme defined");
                        if (!qsg.HasValue) throw new System.InvalidOperationException("No Subgroup defined");
                        if (qs.HasValue)
                        {
                            querySubGroup = qsg.Value;
                            querySubGroup.Settings.Add(qs.Value);
                            qsg = querySubGroup;
                        }
                        m = queryValueRegex.Match(value);
                        querySetting = new QuerySetting();
                        querySetting.PossibleSettings = new QueryPossibleSetting[0];
                        querySetting.SettingGuid = m.Groups[1].Value.Trim();
                        querySetting.SettingFriendlyName = m.Groups[2].Value.Trim();
                        qs = querySetting;
                        lastGuidType = "PowerSetting";
                        break;
                    case "Possible Setting Index":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        possibleSettingIndex = int.Parse(value);
                        break;
                    case "Possible Setting Friendly Name":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        QueryPossibleSetting qps = new QueryPossibleSetting();
                        qps.FriendlyName = value;
                        querySetting = qs.Value;
                        if (querySetting.PossibleSettings.Length < possibleSettingIndex + 1)
                        {
                            Array.Resize(ref querySetting.PossibleSettings, possibleSettingIndex + 1);
                        }
                        querySetting.PossibleSettings[possibleSettingIndex] = qps;
                        qs = querySetting;
                        break;
                    case "Minimum Possible Setting":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.MinimumPossibleSetting = Convert.ToInt32(value, 16);
                        qs = querySetting;
                        break;
                    case "Maximum Possible Setting":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.MaximumPossibleSetting = Convert.ToInt32(value, 16);
                        qs = querySetting;
                        break;
                    case "Possible Settings increment":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.PossibleSettingIncrement = Convert.ToInt32(value, 16);
                        qs = querySetting;
                        break;
                    case "Possible Settings units":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.PossibleSettingUnit = value;
                        qs = querySetting;
                        break;
                    case "Current AC Power Setting Index":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.ACSetting = Convert.ToInt32(value, 16);
                        qs = querySetting;
                        break;
                    case "Current DC Power Setting Index":
                        if (!qs.HasValue) throw new System.InvalidOperationException("No Power Settings defined");
                        querySetting = qs.Value;
                        querySetting.DCSetting = Convert.ToInt32(value, 16);
                        qs = querySetting;
                        break;
                    case "GUID Alias":
                        switch (lastGuidType)
                        {
                            case "PowerScheme":
                                break;
                            case "SubGroup":
                                if (!qsg.HasValue) throw new System.InvalidOperationException("Subgroup missing");
                                querySubGroup = qsg.Value;
                                querySubGroup.SubGroupAlias = value;
                                qsg = querySubGroup;
                                break;
                            case "PowerSetting":
                                if (!qs.HasValue) throw new System.InvalidOperationException("PowerSetting missing");
                                querySetting = qs.Value;
                                querySetting.SettingAlias = value;
                                qs = querySetting;
                                break;
                        }
                        break;
                }
            }
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                error.Add(line);
            }

            if (qv.HasValue)
            {
                if (qsg.HasValue)
                {
                    if (qs.HasValue)
                    {
                        querySubGroup = qsg.Value;
                        querySubGroup.Settings.Add(qs.Value);
                        qsg = querySubGroup;
                    }
                    queryValue = qv.Value;
                    queryValue.SubGroups.Add(qsg.Value);
                    qv = queryValue;
                }

            }

            return qv;
        }

        public static void SetValueIndex(ValueIndex Index, string Scheme, string SubGroup, string Settings, string Value)
        {
            if (!IsUserAnAdmin()) throw new System.UnauthorizedAccessException("User must be an administrator to modify value indexes");
            List<string> error = new List<string>();

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"{(Index == ValueIndex.AC ? "/SETACVALUEINDEX" : "/SETDCVALUEINDEX")} {Scheme} {SubGroup} {Settings} {Value}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.Verb = "runas";
            p.Start();
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                error.Add(line);
            }
            bool exited = p.WaitForExit(5000);
            if (!exited) throw new System.TimeoutException($"Timeout setting {SubGroup} {Settings} value index: {String.Join(",", error.ToArray())}");
            if (p.ExitCode != 0) throw new System.ArgumentException($"Could not set {SubGroup} {Settings} value index: {String.Join(",", error.ToArray())}");
        }

        public static void SetActive(string Scheme)
        {
            if (!IsUserAnAdmin()) throw new System.UnauthorizedAccessException("User must be an administrator to modify value indexes");
            List<string> error = new List<string>();

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"-s {Scheme}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.Verb = "runas";
            p.Start();
            while (!p.StandardError.EndOfStream)
            {
                string line = p.StandardError.ReadLine();
                error.Add(line);
            }
            bool exited = p.WaitForExit(5000);
            if (!exited) throw new System.TimeoutException($"Timeout setting scheme {Scheme} active: {String.Join(",", error.ToArray())}");
            if (p.ExitCode != 0) throw new System.ArgumentException($"Could not set scheme {Scheme} active: {String.Join(",", error.ToArray())}");
        }

    }
}