using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class PowerShellCommands : ICommands
    {
        public enum PsExecutionPolicy
        {
            Default = 0,  // do not pass -ExecutionPolicy
            Restricted,
            AllSigned,
            RemoteSigned,
            Unrestricted,
            Bypass
        }

        /// <summary>
        /// Multiline script content (the only user-provided input).
        /// </summary>
        public string ScriptContent { get; set; } = string.Empty;

        public bool PreferPwsh { get; set; } = true;
        public bool NoProfile { get; set; } = true;
        public bool NoLogo { get; set; } = true;
        public bool RunAs { get; set; } = false;

        public PsExecutionPolicy ExecutionPolicy { get; set; } = PsExecutionPolicy.Bypass;
        public ProcessWindowStyle windowStyle { get; set; } = ProcessWindowStyle.Hidden;

        public PowerShellCommands()
        {
            base.commandType = CommandType.PowerShell;

            // todo: localize
            base.Name = "PowerShell";
            base.Description = "Run a PowerShell script from inline content";
            base.Glyph = "\uE756"; // reuse your existing style if needed

            // Prevent repeated launch while holding the hotkey
            base.OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (string.IsNullOrWhiteSpace(ScriptContent))
                return;

            // encode to avoid quoting/escaping issues and to support multiline reliably
            string encoded = EncodeForPowerShell(ScriptContent);

            Task.Run(() =>
            {
                string psExe = PreferPwsh ? "pwsh.exe" : "powershell.exe";
                string psArgs = BuildPowerShellArgs(encoded);

                var startInfo = new ProcessStartInfo
                {
                    FileName = psExe,
                    Arguments = psArgs,
                    WindowStyle = windowStyle,

                    // needed for RunAs; matches your ExecutableCommands approach
                    UseShellExecute = true,
                    Verb = RunAs ? "runas" : string.Empty,
                };

                try
                {
                    using Process process = new() { StartInfo = startInfo };
                    process.Start();
                }
                catch (Win32Exception)
                {
                    // If pwsh.exe isn't available, fallback to Windows PowerShell
                    if (PreferPwsh)
                    {
                        try
                        {
                            startInfo.FileName = "powershell.exe";
                            using Process process = new() { StartInfo = startInfo };
                            process.Start();
                        }
                        catch { /* fail silently */ }
                    }
                }
                catch
                {
                    // fail silently (or replace with LogManager if you prefer)
                }
            });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        private string BuildPowerShellArgs(string encodedCommand)
        {
            var sb = new StringBuilder();

            if (NoLogo) sb.Append("-NoLogo ");
            if (NoProfile) sb.Append("-NoProfile ");

            if (ExecutionPolicy != PsExecutionPolicy.Default)
                sb.Append("-ExecutionPolicy ").Append(ExecutionPolicy).Append(' ');

            sb.Append("-EncodedCommand ").Append(encodedCommand);

            return sb.ToString();
        }

        private static string EncodeForPowerShell(string script)
        {
            // PowerShell expects Base64(UTF-16LE) for -EncodedCommand
            byte[] bytes = Encoding.Unicode.GetBytes(script);
            return Convert.ToBase64String(bytes);
        }

        public override object Clone()
        {
            PowerShellCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown,

                // specific
                ScriptContent = this.ScriptContent,
                PreferPwsh = this.PreferPwsh,
                NoProfile = this.NoProfile,
                NoLogo = this.NoLogo,
                ExecutionPolicy = this.ExecutionPolicy,
                windowStyle = this.windowStyle,
                RunAs = this.RunAs
            };

            return commands;
        }
    }
}
