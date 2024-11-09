using static Logging;

if (args.Length != 4)
{
    Console.WriteLine("""
     Usage: LBEParser [version] [script-path] [O-level] [g-level]
    """);
    return;
}
if (!int.TryParse(args[2],out int opt)) {
    Console.WriteLine("Received a non-number O-level.");
    return;
}
if (!int.TryParse(args[3], out int dbg)) {
    Console.WriteLine("Received a non-number g-level.");
    return;
}
LuauScript readScript = new LuauScript(args[0],args[1],opt,dbg);
Console.Write(readScript.ConvertSelf());