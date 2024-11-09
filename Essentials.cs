using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Essentials
{
    public static byte[] ExecProc(string executablePath, string arguments, out string stderr)
    {
        stderr = "";
        var processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();
            using (var memoryStream = new MemoryStream())
            {
                using (var reader = process.StandardOutput.BaseStream)
                {
                    reader.CopyTo(memoryStream);
                }
                using (var errorReader = new StreamReader(process.StandardError.BaseStream))
                {
                    stderr = errorReader.ReadToEnd();
                }
                process.WaitForExit();
                return memoryStream.ToArray();
            }
        }
    }

    public static string GetArguments(Proto p)
    {
        string args = "";
        bool first = true;
        for(int i = 0; i < p.numparams; i++)
        {
            if (!first)
                args += ", ";
            args += "v" + i;
            first = false;
        }
        if (p.is_vararg == 1)
        {
            args += (first ? "..." : ", ...");
        }
        return args;
    }
}
