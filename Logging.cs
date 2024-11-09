#pragma warning disable CS1696,CS8602,IL2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class Logging
{
    private static string GetCallerClass()
    {
        var methodInfo = new StackTrace().GetFrame(2).GetMethod();
        var className = methodInfo.ReflectedType.Name;
        return className;
    }

    public static void debug(params object[] writes)
    {
#if DEBUG
        string total = "["+ GetCallerClass() + "::DEBUG] ";
        foreach (object i in writes)
        {
            total += i.ToString();
        }
        Console.WriteLine(total);
#endif
    }

    public static void print(params object[] writes)
    {
#if DEBUG
        string total = "[" + GetCallerClass() + "::PRINT] ";
        foreach (object i in writes)
        {
            total += i.ToString();
        }
        Console.WriteLine(total);
#endif
    }

    public static void warn(params object[] writes)
    {
#if DEBUG
        string total = "[" + GetCallerClass() + "::WARN] ";
        foreach (object i in writes)
        {
            total += i.ToString();
        }
        Console.WriteLine(total);
#endif
    }

    public static void error(params object[] writes)
    {
        string total = "[" + GetCallerClass() + "::ERROR] ";
        foreach (object i in writes)
        {
            total += i.ToString();
        }
        throw new Exception(total);
    }
}